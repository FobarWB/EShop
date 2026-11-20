using System.Text.Json;
using EShop.Data;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;
using EShop.ProductApi.Contracts;
using EShop.ProductApi.Reservations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.ProductApi.Tests.Integration.Reservations;

// The handler alone on the test database, inside a transaction with SaveChanges, as MessageConsumer runs it.
// The consumer's mechanics (inbox, retries) are tested in EShop.Messaging.Tests.Integration.
public class OrderEventsHandlerTests : IAsyncLifetime
{
    private const int OrderId = 11090;

    private readonly ProductApiDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public OrderEventsHandlerTests(ProductApiDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Placed_EnoughStock_TakesStockAndPublishesReserved()
    {
        var chai = await SeedProductAsync(stock: 10);
        var syrup = await SeedProductAsync(stock: 5);

        var result = await HandleAsync(Placed(OrderId, (chai, 2), (syrup, 5)));

        result.ShouldBe(HandleResult.Processed);
        (await ProductAsync(chai)).UnitsInStock.ShouldBe((short)8);
        (await ProductAsync(syrup)).UnitsInStock.ShouldBe((short)0);
        var outbox = await OutboxAsync();
        outbox
            .Where(m => m.Type == ProductEventTypes.ProductChanged)
            .Select(PayloadOf<ProductChanged>)
            .Select(c => (c.ProductId, (int)c.UnitsInStock, c.Revision))
            .ShouldBe([(chai, 8, 2L), (syrup, 0, 2L)], ignoreOrder: true);
        PayloadOf<StockReserved>(outbox.Single(m => m.Type == ProductEventTypes.StockReserved))
            .OrderId.ShouldBe(OrderId);
        var reservation = await ReservationAsync(OrderId);
        reservation.Status.ShouldBe(StockReservationStatus.Reserved);
        reservation
            .Lines.Select(l => (l.ProductId, l.Quantity))
            .ShouldBe([(chai, 2), (syrup, 5)], ignoreOrder: true);
    }

    [Fact]
    public async Task Placed_SomeLinesShort_TakesNothingAndPublishesAllShortages()
    {
        var chai = await SeedProductAsync(stock: 10);
        var syrup = await SeedProductAsync(stock: 3);
        var tofu = await SeedProductAsync(stock: 0);

        await HandleAsync(Placed(OrderId, (chai, 2), (syrup, 5), (tofu, 1)));

        var product = await ProductAsync(chai);
        product.UnitsInStock.ShouldBe((short)10);
        product.Revision.ShouldBe(1);
        var outbox = await OutboxAsync();
        var rejected = PayloadOf<StockRejected>(outbox.ShouldHaveSingleItem());
        rejected.OrderId.ShouldBe(OrderId);
        rejected
            .Shortages.Select(s => (s.ProductId, s.Requested, s.Available))
            .ShouldBe([(syrup, 5, 3), (tofu, 1, 0)], ignoreOrder: true);
        (await ReservationAsync(OrderId)).Status.ShouldBe(StockReservationStatus.Rejected);
    }

    [Theory]
    [InlineData("discontinued")]
    [InlineData("no price")]
    [InlineData("unknown")]
    [InlineData("no id")] // an order.placed older than #76
    public async Task Placed_ProductNotForSale_RejectsWithNothingAvailable(string product)
    {
        var productId = product switch
        {
            "discontinued" => await SeedProductAsync(stock: 10, discontinued: true),
            "no price" => await SeedProductAsync(stock: 10, price: null),
            "unknown" => 999_999,
            _ => 0,
        };

        await HandleAsync(Placed(OrderId, (productId, 1)));

        var shortage = PayloadOf<StockRejected>((await OutboxAsync()).ShouldHaveSingleItem())
            .Shortages.ShouldHaveSingleItem();
        (shortage.ProductId, shortage.Requested, shortage.Available).ShouldBe((productId, 1, 0));
    }

    [Fact]
    public async Task Placed_Again_ChangesNothing()
    {
        var chai = await SeedProductAsync(stock: 10);
        await HandleAsync(Placed(OrderId, (chai, 2)));

        // A new message id: the inbox would not stop it, the reservation does.
        var result = await HandleAsync(Placed(OrderId, (chai, 2)));

        result.ShouldBe(HandleResult.Processed);
        (await ProductAsync(chai)).UnitsInStock.ShouldBe((short)8);
        (await OutboxAsync()).Count(m => m.Type == ProductEventTypes.StockReserved).ShouldBe(1);
    }

    [Fact]
    public async Task Cancelled_AfterReserved_ReturnsStockOnce()
    {
        var chai = await SeedProductAsync(stock: 10);
        await HandleAsync(Placed(OrderId, (chai, 2)));

        await HandleAsync(Cancelled(OrderId));
        await HandleAsync(Cancelled(OrderId));

        var product = await ProductAsync(chai);
        product.UnitsInStock.ShouldBe((short)10);
        product.Revision.ShouldBe(3); // seeded 1, reserved 2, returned 3
        (await OutboxAsync())
            .Where(m => m.Type == ProductEventTypes.ProductChanged)
            .Select(PayloadOf<ProductChanged>)
            .Select(c => c.UnitsInStock)
            .ShouldBe([(short)8, (short)10]);
        var reservation = await ReservationAsync(OrderId);
        reservation.Status.ShouldBe(StockReservationStatus.Released);
        reservation.ReleasedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cancelled_BeforePlaced_PlacedLaterTakesNothing()
    {
        var chai = await SeedProductAsync(stock: 10);

        await HandleAsync(Cancelled(OrderId));
        await HandleAsync(Placed(OrderId, (chai, 2)));

        (await ProductAsync(chai)).UnitsInStock.ShouldBe((short)10);
        // No answer: the order is cancelled in OrderApi already.
        (await OutboxAsync()).ShouldBeEmpty();
        (await ReservationAsync(OrderId)).Status.ShouldBe(StockReservationStatus.Cancelled);
    }

    [Fact]
    public async Task Cancelled_AfterRejected_ChangesNothing()
    {
        var chai = await SeedProductAsync(stock: 1);
        await HandleAsync(Placed(OrderId, (chai, 2)));

        await HandleAsync(Cancelled(OrderId));

        var product = await ProductAsync(chai);
        product.UnitsInStock.ShouldBe((short)1);
        product.Revision.ShouldBe(1);
        (await ReservationAsync(OrderId)).Status.ShouldBe(StockReservationStatus.Rejected);
    }

    [Fact]
    public async Task Placed_TwoOrdersForLastUnit_SecondWaitsAndIsRejected()
    {
        var chai = await SeedProductAsync(stock: 1);

        // The first order takes the last unit and keeps its transaction open: the row stays locked.
        await using var first = await HandleUncommittedAsync(Placed(OrderId, (chai, 1)));
        var second = HandleAsync(Placed(OrderId + 1, (chai, 1)));
        await Task.Delay(TimeSpan.FromMilliseconds(300), _cancellationToken);
        second.IsCompleted.ShouldBeFalse(); // waiting for the lock, not reading the old stock
        await first.Transaction.CommitAsync(_cancellationToken);

        // Without the lock the second one would fail on xmin instead.
        (await second).ShouldBe(HandleResult.Processed);
        (await ProductAsync(chai)).UnitsInStock.ShouldBe((short)0);
        (await ReservationAsync(OrderId)).Status.ShouldBe(StockReservationStatus.Reserved);
        (await ReservationAsync(OrderId + 1)).Status.ShouldBe(StockReservationStatus.Rejected);
        var shortage = PayloadOf<StockRejected>(
            (await OutboxAsync()).Single(m => m.Type == ProductEventTypes.StockRejected)
        )
            .Shortages.ShouldHaveSingleItem();
        shortage.Available.ShouldBe(0);
    }

    [Fact]
    public async Task UnknownType_RejectsAndStoresNothing()
    {
        var result = await HandleAsync(
            "order.lost",
            JsonSerializer.SerializeToUtf8Bytes(Cancelled(OrderId), JsonSerializerOptions.Web)
        );

        result.RejectReason.ShouldNotBeNull();
        await using var db = _database.CreateContext();
        (await db.StockReservations.AnyAsync(_cancellationToken)).ShouldBeFalse();
    }

    private static OrderPlaced Placed(int orderId, params (int ProductId, int Quantity)[] lines) =>
        new(
            orderId,
            "jane@example.com",
            "Jane Doe",
            0m,
            DateTime.UtcNow,
            lines
                .Select(l => new OrderPlacedItem("Test product", l.Quantity, 10m, l.ProductId))
                .ToList()
        );

    private static OrderCancelled Cancelled(int orderId) => new(orderId, null, DateTime.UtcNow);

    private Task<HandleResult> HandleAsync(OrderPlaced placed) =>
        HandleAsync(OrderEventTypes.OrderPlaced, Serialize(placed));

    private Task<HandleResult> HandleAsync(OrderCancelled cancelled) =>
        HandleAsync(OrderEventTypes.OrderCancelled, Serialize(cancelled));

    // As MessageConsumer: one transaction around the handler and SaveChanges, committed after a Processed result.
    private async Task<HandleResult> HandleAsync(string type, byte[] body)
    {
        await using var handling = await HandleUncommittedAsync(type, body);
        if (handling.Result.RejectReason == null)
        {
            await handling.Transaction.CommitAsync(_cancellationToken);
        }
        return handling.Result;
    }

    private Task<Handling> HandleUncommittedAsync(OrderPlaced placed) =>
        HandleUncommittedAsync(OrderEventTypes.OrderPlaced, Serialize(placed));

    // Handled and saved, not committed: the row locks are held until the caller commits or disposes.
    private async Task<Handling> HandleUncommittedAsync(string type, byte[] body)
    {
        var db = _database.CreateContext();
        IDbContextTransaction? transaction = null;
        try
        {
            transaction = await db.Database.BeginTransactionAsync(_cancellationToken);
            var result = await new OrderEventsHandler(
                db,
                NullLogger<OrderEventsHandler>.Instance
            ).HandleAsync(
                new ReceivedMessage(Guid.CreateVersion7(), type, body),
                _cancellationToken
            );
            if (result.RejectReason == null)
            {
                await db.SaveChangesAsync(_cancellationToken);
            }
            return new Handling(db, transaction, result);
        }
        catch
        {
            // Roll back now: the row locks would outlive this test and block the next one's reset.
            if (transaction != null)
            {
                await transaction.DisposeAsync();
            }
            await db.DisposeAsync();
            throw;
        }
    }

    private sealed record Handling(
        EShopDbContext Db,
        IDbContextTransaction Transaction,
        HandleResult Result
    ) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Transaction.DisposeAsync();
            await Db.DisposeAsync();
        }
    }

    private async Task<int> SeedProductAsync(
        short stock,
        bool discontinued = false,
        decimal? price = 10m
    )
    {
        var product = await _database.SeedAsync(
            new Product
            {
                ProductName = "Test product",
                UnitPrice = price,
                UnitsInStock = stock,
                Discontinued = discontinued,
                Revision = 1,
            },
            _cancellationToken
        );
        if (price == null)
        {
            // unit_price has a database default (0): EF Core leaves a null out of the INSERT, so the row would get 0.
            await using var db = _database.CreateContext();
            await db
                .Products.Where(p => p.ProductId == product.ProductId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.UnitPrice, (decimal?)null),
                    _cancellationToken
                );
        }
        return product.ProductId;
    }

    private async Task<Product> ProductAsync(int productId) =>
        (await _database.FindAsync<Product>(productId, _cancellationToken)).ShouldNotBeNull();

    private async Task<StockReservation> ReservationAsync(int orderId)
    {
        await using var db = _database.CreateContext();
        return await db
            .StockReservations.AsNoTracking()
            .Include(r => r.Lines)
            .SingleAsync(r => r.OrderId == orderId, _cancellationToken);
    }

    private async Task<List<OutboxMessage>> OutboxAsync()
    {
        await using var db = _database.CreateContext();
        return await db
            .OutboxMessages.AsNoTracking()
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(_cancellationToken);
    }

    private static byte[] Serialize<T>(T @event) =>
        JsonSerializer.SerializeToUtf8Bytes(@event, JsonSerializerOptions.Web);

    private static T PayloadOf<T>(OutboxMessage message)
        where T : class =>
        JsonSerializer.Deserialize<T>(message.Payload, JsonSerializerOptions.Web).ShouldNotBeNull();
}
