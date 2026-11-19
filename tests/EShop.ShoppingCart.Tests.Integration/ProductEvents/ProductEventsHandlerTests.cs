using System.Text.Json;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.ProductApi.Contracts;
using EShop.ShoppingCart.ProductEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.ShoppingCart.Tests.Integration.ProductEvents;

// The handler alone on the test database, inside a transaction with SaveChanges, as MessageConsumer runs it.
// The consumer's mechanics (inbox, retries) are tested in EShop.Messaging.Tests.Integration.
public class ProductEventsHandlerTests : IAsyncLifetime
{
    private const int ProductId = 7;

    private readonly ShoppingCartDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public ProductEventsHandlerTests(ShoppingCartDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Changed_UnknownProduct_InsertsCopy()
    {
        var result = await HandleAsync(Changed(revision: 1, name: "Chai", price: 18));

        result.ShouldBe(HandleResult.Processed);
        var copy = (await CopyAsync()).ShouldNotBeNull();
        copy.ProductName.ShouldBe("Chai");
        copy.CategoryId.ShouldBe(1);
        copy.UnitPrice.ShouldBe(18m);
        copy.UnitsInStock.ShouldBe((short)39);
        copy.Discontinued.ShouldBeFalse();
        copy.Deleted.ShouldBeFalse();
        copy.Revision.ShouldBe(1);
    }

    [Fact]
    public async Task Changed_NewerRevision_ReplacesCopy()
    {
        await HandleAsync(Changed(revision: 1, name: "Chai", price: 18));

        await HandleAsync(Changed(revision: 2, name: "Chai Latte", price: 20));

        var copy = (await CopyAsync()).ShouldNotBeNull();
        copy.ProductName.ShouldBe("Chai Latte");
        copy.UnitPrice.ShouldBe(20m);
        copy.Revision.ShouldBe(2);
    }

    [Theory]
    [InlineData(5)] // a duplicate or a republish
    [InlineData(4)] // delivered late
    public async Task Changed_SameOrOlderRevision_KeepsCopy(long revision)
    {
        await HandleAsync(Changed(revision: 5, name: "Chai", price: 18));

        var result = await HandleAsync(Changed(revision, name: "Old name", price: 1));

        result.ShouldBe(HandleResult.Processed);
        var copy = (await CopyAsync()).ShouldNotBeNull();
        copy.ProductName.ShouldBe("Chai");
        copy.UnitPrice.ShouldBe(18m);
        copy.Revision.ShouldBe(5);
    }

    [Fact]
    public async Task Deleted_KeepsTombstoneAndRemovesFromCarts()
    {
        // cart_items still references products by a foreign key (removed in the next step): seed both.
        var deletedId = await _database.SeedProductAsync(10, _cancellationToken);
        var otherId = await _database.SeedProductAsync(10, _cancellationToken);
        await HandleAsync(Changed(revision: 1, name: "Chai", price: 18, productId: deletedId));
        await SeedCartAsync("user-1", deletedId, otherId);
        await SeedCartAsync("user-2", deletedId);

        var result = await HandleAsync(Deleted(revision: 2, productId: deletedId));

        result.ShouldBe(HandleResult.Processed);
        var copy = (await CopyAsync(deletedId)).ShouldNotBeNull();
        copy.Deleted.ShouldBeTrue();
        copy.Revision.ShouldBe(2);
        await using var db = _database.CreateContext();
        (
            await db
                .CartItems.Select(i => new { i.Cart!.UserId, i.ProductId })
                .ToListAsync(_cancellationToken)
        )
            .ShouldHaveSingleItem()
            .ShouldBe(new { UserId = "user-1", ProductId = otherId });
    }

    [Fact]
    public async Task Deleted_UnknownProduct_InsertsTombstone()
    {
        await HandleAsync(Deleted(revision: 3));

        var copy = (await CopyAsync()).ShouldNotBeNull();
        copy.Deleted.ShouldBeTrue();
        copy.Revision.ShouldBe(3);
    }

    [Theory]
    [InlineData(2)] // delivered after the ProductDeleted that followed it
    [InlineData(9)] // newer than the deletion: ProductApi never sends one, the replica does not rely on that
    public async Task Changed_AfterDeleted_DoesNotBringBack(long revision)
    {
        await HandleAsync(Deleted(revision: 3));

        await HandleAsync(Changed(revision, name: "Chai", price: 18));

        var copy = (await CopyAsync()).ShouldNotBeNull();
        copy.Deleted.ShouldBeTrue();
        copy.Revision.ShouldBe(3);
    }

    [Fact]
    public async Task UnknownType_RejectsAndStoresNothing()
    {
        var result = await HandleAsync(
            Changed(revision: 1, name: "Chai", price: 18),
            "product.renamed"
        );

        result.RejectReason.ShouldNotBeNull();
        (await CopyAsync()).ShouldBeNull();
    }

    private static ProductChanged Changed(
        long revision,
        string name,
        decimal price,
        int productId = ProductId
    ) => new(productId, name, 1, price, 39, false, revision, DateTime.UtcNow);

    private static ProductDeleted Deleted(long revision, int productId = ProductId) =>
        new(productId, revision, DateTime.UtcNow);

    private Task<HandleResult> HandleAsync(
        ProductChanged changed,
        string type = ProductEventTypes.ProductChanged
    ) => HandleAsync(type, JsonSerializer.SerializeToUtf8Bytes(changed, JsonSerializerOptions.Web));

    private Task<HandleResult> HandleAsync(ProductDeleted deleted) =>
        HandleAsync(
            ProductEventTypes.ProductDeleted,
            JsonSerializer.SerializeToUtf8Bytes(deleted, JsonSerializerOptions.Web)
        );

    // As MessageConsumer: one transaction around the handler and SaveChanges, committed after a Processed result.
    private async Task<HandleResult> HandleAsync(string type, byte[] body)
    {
        await using var db = _database.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(_cancellationToken);
        var result = await new ProductEventsHandler(
            db,
            NullLogger<ProductEventsHandler>.Instance
        ).HandleAsync(new ReceivedMessage(Guid.CreateVersion7(), type, body), _cancellationToken);
        if (result.RejectReason == null)
        {
            await db.SaveChangesAsync(_cancellationToken);
            await transaction.CommitAsync(_cancellationToken);
        }
        return result;
    }

    private Task<CartProduct?> CopyAsync(int productId = ProductId) =>
        _database.FindAsync<CartProduct>(productId, _cancellationToken);

    private Task<Cart> SeedCartAsync(string userId, params int[] productIds) =>
        _database.SeedAsync(
            new Cart
            {
                UserId = userId,
                CartItems = productIds
                    .Select(id => new CartItem { ProductId = id, Quantity = 1 })
                    .ToList(),
            },
            _cancellationToken
        );
}
