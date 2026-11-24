using System.Text.Json;
using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;
using EShop.ShoppingCart.Data.Entities;
using EShop.ShoppingCart.OrderEvents;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.ShoppingCart.Tests.Integration.OrderEvents;

// The handler alone on the test database, inside a transaction with SaveChanges, as MessageConsumer runs it.
// The consumer's mechanics (inbox, retries) are tested in EShop.Messaging.Tests.Integration.
public class OrderEventsHandlerTests : IAsyncLifetime
{
    private const string UserId = "user-1";
    private const int ChaiId = 7;
    private const int SyrupId = 9;
    private const int TofuId = 14;

    private readonly ShoppingCartDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public OrderEventsHandlerTests(ShoppingCartDatabase database)
    {
        _database = database;
    }

    // The items reference the cart's product copies (a foreign key), so the copies come first.
    public async ValueTask InitializeAsync()
    {
        await _database.ResetAsync(_cancellationToken);
        foreach (var productId in new[] { ChaiId, SyrupId, TofuId })
        {
            await _database.SeedAsync(
                new CartProduct
                {
                    ProductId = productId,
                    ProductName = "Test product",
                    UnitsInStock = 10,
                    Revision = 1,
                },
                _cancellationToken
            );
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Confirmed_TakesTheOrderedLinesOutOfTheCart()
    {
        await SeedCartAsync(UserId, (ChaiId, 2), (SyrupId, 1), (TofuId, 1));

        var result = await HandleAsync(Confirmed(UserId, (ChaiId, 2), (SyrupId, 1)));

        result.ShouldBe(HandleResult.Processed);
        (await LinesAsync(UserId)).ShouldBe([(TofuId, 1)]);
    }

    [Fact]
    public async Task Confirmed_MoreInTheCartThanOrdered_KeepsTheRest()
    {
        // Two more were added after the checkout.
        await SeedCartAsync(UserId, (ChaiId, 5));

        await HandleAsync(Confirmed(UserId, (ChaiId, 3)));

        (await LinesAsync(UserId)).ShouldBe([(ChaiId, 2)]);
    }

    [Fact]
    public async Task Confirmed_LessInTheCartThanOrdered_RemovesTheLine()
    {
        // Lowered after the checkout: nothing negative is left.
        await SeedCartAsync(UserId, (ChaiId, 1), (SyrupId, 1));

        await HandleAsync(Confirmed(UserId, (ChaiId, 3)));

        (await LinesAsync(UserId)).ShouldBe([(SyrupId, 1)]);
    }

    [Fact]
    public async Task Confirmed_LeavesOtherUsersCarts()
    {
        await SeedCartAsync(UserId, (ChaiId, 2));
        await SeedCartAsync("user-2", (ChaiId, 2));

        await HandleAsync(Confirmed(UserId, (ChaiId, 2)));

        (await LinesAsync("user-2")).ShouldBe([(ChaiId, 2)]);
    }

    [Fact]
    public async Task Confirmed_GuestOrder_ChangesNoCart()
    {
        await SeedCartAsync(UserId, (ChaiId, 2));

        var result = await HandleAsync(Confirmed(userId: null, (ChaiId, 2)));

        result.ShouldBe(HandleResult.Processed);
        (await LinesAsync(UserId)).ShouldBe([(ChaiId, 2)]);
    }

    [Fact]
    public async Task Confirmed_UserWithoutCart_Processed()
    {
        var result = await HandleAsync(Confirmed(UserId, (ChaiId, 2)));

        result.ShouldBe(HandleResult.Processed);
    }

    [Fact]
    public async Task UnknownType_RejectsAndChangesNothing()
    {
        await SeedCartAsync(UserId, (ChaiId, 2));

        var result = await HandleAsync(
            OrderEventTypes.OrderPlaced,
            JsonSerializer.SerializeToUtf8Bytes(
                Confirmed(UserId, (ChaiId, 2)),
                JsonSerializerOptions.Web
            )
        );

        result.RejectReason.ShouldNotBeNull();
        (await LinesAsync(UserId)).ShouldBe([(ChaiId, 2)]);
    }

    private static OrderConfirmed Confirmed(
        string? userId,
        params (int ProductId, int Quantity)[] items
    ) =>
        new(
            11090,
            userId,
            "jane@example.com",
            "Jane Doe",
            0m,
            PlacedAsGuest: userId == null,
            DateTime.UtcNow,
            items
                .Select(i => new OrderConfirmedItem(i.ProductId, "Test product", i.Quantity, 10m))
                .ToList()
        );

    private Task<HandleResult> HandleAsync(OrderConfirmed confirmed) =>
        HandleAsync(
            OrderEventTypes.OrderConfirmed,
            JsonSerializer.SerializeToUtf8Bytes(confirmed, JsonSerializerOptions.Web)
        );

    // As MessageConsumer: one transaction around the handler and SaveChanges, committed after a Processed result.
    private async Task<HandleResult> HandleAsync(string type, byte[] body)
    {
        await using var db = _database.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(_cancellationToken);
        var result = await new OrderEventsHandler(
            db,
            NullLogger<OrderEventsHandler>.Instance
        ).HandleAsync(new ReceivedMessage(Guid.CreateVersion7(), type, body), _cancellationToken);
        if (result.RejectReason == null)
        {
            await db.SaveChangesAsync(_cancellationToken);
            await transaction.CommitAsync(_cancellationToken);
        }
        return result;
    }

    private Task<Cart> SeedCartAsync(string userId, params (int ProductId, int Quantity)[] lines) =>
        _database.SeedAsync(
            new Cart
            {
                UserId = userId,
                CartItems = lines
                    .Select(l => new CartItem { ProductId = l.ProductId, Quantity = l.Quantity })
                    .ToList(),
            },
            _cancellationToken
        );

    private async Task<List<(int ProductId, int Quantity)>> LinesAsync(string userId) =>
        (await _database.LoadCartAsync(userId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.OrderBy(i => i.ProductId)
            .Select(i => (i.ProductId, i.Quantity))
            .ToList();
}
