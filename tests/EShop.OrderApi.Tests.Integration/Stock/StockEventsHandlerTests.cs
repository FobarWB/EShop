using System.Text.Json;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;
using EShop.OrderApi.Stock;
using EShop.ProductApi.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.OrderApi.Tests.Integration.Stock;

// The handler alone on the test database, inside a transaction with SaveChanges, as MessageConsumer runs it.
// The consumer's mechanics (inbox, retries) are tested in EShop.Messaging.Tests.Integration.
public class StockEventsHandlerTests : IAsyncLifetime
{
    private const string UserId = "user-1";
    private const int ChaiId = 7;
    private const int SyrupId = 9;

    private readonly OrderApiDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public StockEventsHandlerTests(OrderApiDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Reserved_PendingOrder_ConfirmsAndPublishesOrderConfirmed()
    {
        var seeded = await SeedOrderAsync();

        var result = await HandleAsync(Reserved(seeded.OrderId));

        result.ShouldBe(HandleResult.Processed);
        var order = await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken);
        order.Status.ShouldBe(OrderStatus.Confirmed);
        var change = order.StatusChanges.ShouldHaveSingleItem();
        (change.FromStatus, change.ToStatus, change.ChangedBy).ShouldBe(
            (OrderStatus.Pending, OrderStatus.Confirmed, StockEventsHandler.ChangedBy)
        );
        var confirmed = PayloadOf<OrderConfirmed>(
            (await _database.LoadOutboxAsync(_cancellationToken)).ShouldHaveSingleItem(),
            OrderEventTypes.OrderConfirmed
        );
        confirmed.OrderId.ShouldBe(seeded.OrderId);
        confirmed.UserId.ShouldBe(UserId);
        confirmed.Email.ShouldBe(OrderApiDatabase.BuyerEmail);
        confirmed.Total.ShouldBe(18m * 2 + 10m + 5m); // the lines and the freight of NewOrder
        confirmed
            .Items.Select(i => (i.ProductId, i.ProductName, i.Quantity, i.UnitPrice))
            .ShouldBe(
                [(ChaiId, "Chai", 2, 18m), (SyrupId, "Aniseed Syrup", 1, 10m)],
                ignoreOrder: true
            );
    }

    [Fact]
    public async Task Reserved_GuestOrderWithAccountAttached_PublishesNoUserId()
    {
        // CustomerApi attached the account before the stock answer came: the guest's cart is still the browser's.
        var seeded = await SeedOrderAsync(order =>
        {
            order.PlacedAsGuest = true;
            order.UserId = "account-of-the-guest";
        });

        await HandleAsync(Reserved(seeded.OrderId));

        var confirmed = PayloadOf<OrderConfirmed>(
            (await _database.LoadOutboxAsync(_cancellationToken)).ShouldHaveSingleItem(),
            OrderEventTypes.OrderConfirmed
        );
        confirmed.UserId.ShouldBeNull();
        confirmed.PlacedAsGuest.ShouldBeTrue();
    }

    [Fact]
    public async Task Rejected_PendingOrder_RejectsAndPublishesTheShortLinesByName()
    {
        var seeded = await SeedOrderAsync();

        var result = await HandleAsync(
            Rejected(seeded.OrderId, new StockShortage(SyrupId, Requested: 1, Available: 0))
        );

        result.ShouldBe(HandleResult.Processed);
        var order = await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken);
        order.Status.ShouldBe(OrderStatus.Rejected);
        order.StatusChanges.ShouldHaveSingleItem().ChangedBy.ShouldBe(StockEventsHandler.ChangedBy);
        var rejected = PayloadOf<OrderRejected>(
            (await _database.LoadOutboxAsync(_cancellationToken)).ShouldHaveSingleItem(),
            OrderEventTypes.OrderRejected
        );
        rejected.OrderId.ShouldBe(seeded.OrderId);
        rejected.Email.ShouldBe(OrderApiDatabase.BuyerEmail);
        rejected
            .Items.Select(i => (i.ProductName, i.Quantity, i.Available))
            .ShouldBe([("Aniseed Syrup", 1, 0)]);
    }

    [Theory]
    [InlineData(OrderStatus.Cancelled, ProductEventTypes.StockReserved)] // cancelled while the stock was reserved
    [InlineData(OrderStatus.Cancelled, ProductEventTypes.StockRejected)]
    [InlineData(OrderStatus.Confirmed, ProductEventTypes.StockReserved)] // a redelivery
    [InlineData(OrderStatus.Rejected, ProductEventTypes.StockRejected)] // a redelivery
    public async Task Answer_OrderNoLongerPending_ChangesNothing(OrderStatus status, string type)
    {
        var seeded = await SeedOrderAsync(status: status);
        var journal = (await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken))
            .StatusChanges
            .Count;

        var result =
            type == ProductEventTypes.StockReserved
                ? await HandleAsync(Reserved(seeded.OrderId))
                : await HandleAsync(
                    Rejected(seeded.OrderId, new StockShortage(ChaiId, Requested: 2, Available: 0))
                );

        result.ShouldBe(HandleResult.Processed);
        var order = await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken);
        order.Status.ShouldBe(status);
        order.StatusChanges.Count.ShouldBe(journal);
        (await _database.LoadOutboxAsync(_cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Answer_UnknownOrder_Rejects()
    {
        var result = await HandleAsync(Reserved(orderId: 999_999));

        result.RejectReason.ShouldNotBeNull();
        (await _database.LoadOutboxAsync(_cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownType_RejectsAndChangesNothing()
    {
        var seeded = await SeedOrderAsync();

        var result = await HandleAsync(
            "stock.lost",
            JsonSerializer.SerializeToUtf8Bytes(Reserved(seeded.OrderId), JsonSerializerOptions.Web)
        );

        result.RejectReason.ShouldNotBeNull();
        (await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken)).Status.ShouldBe(
            OrderStatus.Pending
        );
    }

    // A Pending order (or walked on to the status) of Chai × 2 at 18 and Aniseed Syrup × 1 at 10, as checkout
    // stores it; the journal of the walk is in the database, so a test compares counts, not the whole journal.
    private async Task<Order> SeedOrderAsync(
        Action<Order>? change = null,
        OrderStatus status = OrderStatus.Pending
    )
    {
        var order = OrderApiDatabase.NewOrder(UserId, Guid.NewGuid());
        order.OrderDetails.Add(
            new OrderDetail
            {
                ProductId = ChaiId,
                ProductName = "Chai",
                UnitPrice = 18m,
                Quantity = 2,
            }
        );
        order.OrderDetails.Add(
            new OrderDetail
            {
                ProductId = SyrupId,
                ProductName = "Aniseed Syrup",
                UnitPrice = 10m,
                Quantity = 1,
            }
        );
        change?.Invoke(order);
        if (status != OrderStatus.Pending)
        {
            // The one step from Pending to each of the statuses used here.
            order.TryChangeStatus(status, "seed", DateTime.UtcNow).ShouldBeTrue();
        }
        return await _database.SeedAsync(order, _cancellationToken);
    }

    private static StockReserved Reserved(int orderId) => new(orderId, DateTime.UtcNow);

    private static StockRejected Rejected(int orderId, params StockShortage[] shortages) =>
        new(orderId, shortages, DateTime.UtcNow);

    private Task<HandleResult> HandleAsync(StockReserved reserved) =>
        HandleAsync(
            ProductEventTypes.StockReserved,
            JsonSerializer.SerializeToUtf8Bytes(reserved, JsonSerializerOptions.Web)
        );

    private Task<HandleResult> HandleAsync(StockRejected rejected) =>
        HandleAsync(
            ProductEventTypes.StockRejected,
            JsonSerializer.SerializeToUtf8Bytes(rejected, JsonSerializerOptions.Web)
        );

    // As MessageConsumer: one transaction around the handler and SaveChanges, committed after a Processed result.
    private async Task<HandleResult> HandleAsync(string type, byte[] body)
    {
        await using var db = _database.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(_cancellationToken);
        var result = await new StockEventsHandler(
            db,
            NullLogger<StockEventsHandler>.Instance
        ).HandleAsync(new ReceivedMessage(Guid.CreateVersion7(), type, body), _cancellationToken);
        if (result.RejectReason == null)
        {
            await db.SaveChangesAsync(_cancellationToken);
            await transaction.CommitAsync(_cancellationToken);
        }
        return result;
    }

    // The outbox payload as a consumer reads it, after checking the row's type.
    private static T PayloadOf<T>(OutboxMessage message, string type)
        where T : class
    {
        message.Type.ShouldBe(type);
        return JsonSerializer
            .Deserialize<T>(message.Payload, JsonSerializerOptions.Web)
            .ShouldNotBeNull();
    }
}
