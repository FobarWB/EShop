using EShop.Data.Entities;
using EShop.OrderApi.Dtos;
using EShop.OrderApi.Repositories;
using EShop.ServiceDefaults;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EShop.OrderApi.Tests.Integration.Repositories;

// Arrange, act and assert each use their own DbContext, so assertions read what is
// stored in PostgreSQL, not entities cached by the change tracker.
public class OrderRepositoryTests : IAsyncLifetime
{
    private const string UserId = "user-1";
    private const string Email = "jane@example.com";

    private static readonly PlaceOrderRequest Address = new()
    {
        ShipName = "Jane Doe",
        ShipAddress = "1 Main St",
        ShipCity = "Berlin",
        ShipPostalCode = "10115",
        ShipCountry = "Germany",
    };

    private readonly OrderApiDatabase _database;
    private readonly Guid _key = Guid.NewGuid();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public OrderRepositoryTests(OrderApiDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task PlaceOrder_CartWithItems_StoresOrderWithCopiedPrices()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var chang = await _database.SeedProductAsync("Chang", 19m, 5, _cancellationToken);
        await _database.SeedAsync(
            new Cart
            {
                UserId = UserId,
                CartItems =
                [
                    new CartItem { ProductId = chai.ProductId, Quantity = 2 },
                    new CartItem { ProductId = chang.ProductId, Quantity = 1 },
                ],
            },
            _cancellationToken
        );

        var result = await PlaceOrderAsync();

        result.Status.ShouldBe(PlaceOrderStatus.Placed);
        result
            .Order.ShouldNotBeNull()
            .Total.ShouldBe(18m * 2 + 19m + OrderRepository.StandardFreight);
        var order = (await _database.LoadOrdersAsync(_cancellationToken)).ShouldHaveSingleItem();
        order.UserId.ShouldBe(UserId);
        order.CustomerEmail.ShouldBe(Email);
        order.IdempotencyKey.ShouldBe(_key);
        order.Freight.ShouldBe(OrderRepository.StandardFreight);
        order.ShipCity.ShouldBe("Berlin");
        order
            .OrderDetails.Select(d => (d.ProductId, d.UnitPrice, d.Quantity))
            .ShouldBe(
                [(chai.ProductId, 18m, (short)2), (chang.ProductId, 19m, (short)1)],
                ignoreOrder: true
            );
    }

    [Fact]
    public async Task PlaceOrder_CartWithItems_DecrementsStockAndEmptiesCart()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 3, _cancellationToken);

        await PlaceOrderAsync();

        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)7);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceOrder_PriceChangedAfterwards_OrderKeepsCopiedPrice()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 1, _cancellationToken);
        var placed = (await PlaceOrderAsync()).Order.ShouldNotBeNull();
        await using (var db = _database.CreateContext())
        {
            await db
                .Products.Where(p => p.ProductId == chai.ProductId)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.UnitPrice, 25m), _cancellationToken);
        }

        var order = await ActAsync(r =>
            r.RetrieveOrderAsync(UserId, placed.OrderId, _cancellationToken)
        );

        order.ShouldNotBeNull().Items.ShouldHaveSingleItem().UnitPrice.ShouldBe(18m);
    }

    [Fact]
    public async Task PlaceOrder_InsufficientStock_ReturnsProductIdsAndChangesNothing()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 2, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 3, _cancellationToken);

        var result = await PlaceOrderAsync();

        result.Status.ShouldBe(PlaceOrderStatus.InsufficientStock);
        result.ProductIds.ShouldBe([chai.ProductId]);
        await ShouldBeUnchangedAsync(chai.ProductId, unitsInStock: 2);
    }

    [Fact]
    public async Task PlaceOrder_DiscontinuedProduct_ReturnsProductIdsAndChangesNothing()
    {
        var chai = await _database.SeedAsync(
            new Product
            {
                ProductName = "Chai",
                UnitPrice = 18m,
                UnitsInStock = 10,
                Discontinued = true,
            },
            _cancellationToken
        );
        await _database.SeedCartAsync(UserId, chai.ProductId, 1, _cancellationToken);

        var result = await PlaceOrderAsync();

        result.Status.ShouldBe(PlaceOrderStatus.ProductsUnavailable);
        result.ProductIds.ShouldBe([chai.ProductId]);
        await ShouldBeUnchangedAsync(chai.ProductId, unitsInStock: 10);
    }

    [Fact]
    public async Task PlaceOrder_NoCart_ReturnsCartEmpty()
    {
        var result = await PlaceOrderAsync();

        result.Status.ShouldBe(PlaceOrderStatus.CartEmpty);
    }

    [Fact]
    public async Task PlaceOrder_CartWithoutItems_ReturnsCartEmpty()
    {
        await _database.SeedAsync(new Cart { UserId = UserId }, _cancellationToken);

        var result = await PlaceOrderAsync();

        result.Status.ShouldBe(PlaceOrderStatus.CartEmpty);
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceOrder_SameKeyAgain_ReturnsSameOrderAndDecrementsStockOnce()
    {
        // The retry finds a filled cart again: the key, not the empty cart, prevents the second order.
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var cart = await _database.SeedCartAsync(UserId, chai.ProductId, 3, _cancellationToken);
        var first = await PlaceOrderAsync();
        await _database.SeedAsync(
            new CartItem
            {
                CartId = cart.CartId,
                ProductId = chai.ProductId,
                Quantity = 3,
            },
            _cancellationToken
        );

        var second = await PlaceOrderAsync();

        second.Status.ShouldBe(PlaceOrderStatus.AlreadyPlaced);
        second.Order.ShouldNotBeNull().OrderId.ShouldBe(first.Order.ShouldNotBeNull().OrderId);
        (await _database.LoadOrdersAsync(_cancellationToken)).ShouldHaveSingleItem();
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)7);
    }

    [Fact]
    public async Task PlaceOrder_SameKeyPlacedConcurrently_ReturnsThatOrderAndChangesNothingElse()
    {
        // The other request passes the key check too and commits first: our INSERT hits the unique index.
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 3, _cancellationToken);
        Order? concurrent = null;

        var result = await PlaceOrderAsync(
            new BeforeSaveChangesInterceptor(async () =>
                concurrent = await _database.SeedAsync(
                    OrderApiDatabase.NewOrder(UserId, _key),
                    _cancellationToken
                )
            )
        );

        result.Status.ShouldBe(PlaceOrderStatus.AlreadyPlaced);
        result.Order.ShouldNotBeNull().OrderId.ShouldBe(concurrent.ShouldNotBeNull().OrderId);
        await ShouldBeUnchangedAsync(chai.ProductId, unitsInStock: 10, expectedOrders: 1);
    }

    [Fact]
    public async Task PlaceOrder_ProductChangedAfterRead_ThrowsAndChangesNothing()
    {
        // Another buyer takes the stock between our check and our save: xmin no longer matches.
        var chai = await _database.SeedProductAsync("Chai", 18m, 3, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 3, _cancellationToken);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() =>
            PlaceOrderAsync(
                new BeforeSaveChangesInterceptor(async () =>
                {
                    await using var db = _database.CreateContext();
                    await db
                        .Products.Where(p => p.ProductId == chai.ProductId)
                        .ExecuteUpdateAsync(
                            s => s.SetProperty(p => p.UnitsInStock, (short)0),
                            _cancellationToken
                        );
                })
            )
        );

        await ShouldBeUnchangedAsync(chai.ProductId, unitsInStock: 0);
    }

    [Fact]
    public async Task RetrieveOrder_OtherUsersOrder_ReturnsNull()
    {
        var order = await _database.SeedAsync(
            OrderApiDatabase.NewOrder("user-2", Guid.NewGuid()),
            _cancellationToken
        );

        var result = await ActAsync(r =>
            r.RetrieveOrderAsync(UserId, order.OrderId, _cancellationToken)
        );

        result.ShouldBeNull();
    }

    [Fact]
    public async Task PlaceOrder_CartWithItems_StartsStatusJournal()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 1, _cancellationToken);

        var placed = (await PlaceOrderAsync()).Order.ShouldNotBeNull();

        var order = await _database.LoadOrderAsync(placed.OrderId, _cancellationToken);
        order.Status.ShouldBe(OrderStatus.Pending);
        var change = order.StatusChanges.ShouldHaveSingleItem();
        change.FromStatus.ShouldBeNull();
        change.ToStatus.ShouldBe(OrderStatus.Pending);
        change.ChangedBy.ShouldBe(UserId);
    }

    [Fact]
    public async Task PlaceOrder_ProductRenamedAfterwards_OrderKeepsCopiedName()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        await _database.SeedCartAsync(UserId, chai.ProductId, 1, _cancellationToken);
        var placed = (await PlaceOrderAsync()).Order.ShouldNotBeNull();
        await using (var db = _database.CreateContext())
        {
            await db
                .Products.Where(p => p.ProductId == chai.ProductId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(p => p.ProductName, "Chai Tea"),
                    _cancellationToken
                );
        }

        var order = await ActAsync(r =>
            r.RetrieveOrderAsync(UserId, placed.OrderId, _cancellationToken)
        );

        order.ShouldNotBeNull().Items.ShouldHaveSingleItem().ProductName.ShouldBe("Chai");
    }

    [Fact]
    public async Task ChangeStatus_CancelPending_ReturnsStockAndRecordsChange()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var chang = await _database.SeedProductAsync("Chang", 19m, 5, _cancellationToken);
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Pending,
            _cancellationToken,
            (chai.ProductId, 2),
            (chang.ProductId, 3)
        );

        var result = await CancelAsync(seeded.OrderId);

        result.ShouldBe(ChangeOrderStatusResult.Changed);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)12);
        (await _database.FindAsync<Product>(chang.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)8);
        var order = await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken);
        order.Status.ShouldBe(OrderStatus.Cancelled);
        var change = order.StatusChanges.ShouldHaveSingleItem();
        (change.FromStatus, change.ToStatus, change.ChangedBy).ShouldBe(
            (OrderStatus.Pending, OrderStatus.Cancelled, UserId)
        );
    }

    [Fact]
    public async Task ChangeStatus_CancelShipped_ReturnsNotAllowedAndChangesNothing()
    {
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Shipped,
            _cancellationToken,
            (chai.ProductId, 2)
        );

        var result = await CancelAsync(seeded.OrderId);

        result.ShouldBe(ChangeOrderStatusResult.NotAllowed);
        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)10);
        (await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken)).Status.ShouldBe(
            OrderStatus.Shipped
        );
    }

    [Fact]
    public async Task ChangeStatus_OtherUsersOrder_ReturnsNotFound()
    {
        var seeded = await _database.SeedOrderAsync(
            "user-2",
            OrderStatus.Pending,
            _cancellationToken
        );

        var result = await CancelAsync(seeded.OrderId);

        result.ShouldBe(ChangeOrderStatusResult.NotFound);
        (await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken)).Status.ShouldBe(
            OrderStatus.Pending
        );
    }

    [Fact]
    public async Task ChangeStatus_WithoutOwner_ChangesAnyUsersOrder()
    {
        var seeded = await _database.SeedOrderAsync(
            "user-2",
            OrderStatus.Pending,
            _cancellationToken
        );

        var result = await ActAsync(r =>
            r.ChangeStatusAsync(
                seeded.OrderId,
                null,
                OrderStatus.Paid,
                "admin-1",
                _cancellationToken
            )
        );

        result.ShouldBe(ChangeOrderStatusResult.Changed);
        (await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken)).Status.ShouldBe(
            OrderStatus.Paid
        );
    }

    [Fact]
    public async Task ChangeStatus_CancelledConcurrently_ThrowsAndReturnsStockOnce()
    {
        // Both requests read Pending. The other one cancels and commits inside our SaveChanges:
        // our UPDATE finds a changed xmin, nothing of ours is stored, and the stock comes back once.
        var chai = await _database.SeedProductAsync("Chai", 18m, 10, _cancellationToken);
        var seeded = await _database.SeedOrderAsync(
            UserId,
            OrderStatus.Pending,
            _cancellationToken,
            (chai.ProductId, 2)
        );

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() =>
            CancelAsync(
                seeded.OrderId,
                new BeforeSaveChangesInterceptor(async () =>
                    (await CancelAsync(seeded.OrderId)).ShouldBe(ChangeOrderStatusResult.Changed)
                )
            )
        );

        (await _database.FindAsync<Product>(chai.ProductId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe((short)12);
        (
            await _database.LoadOrderAsync(seeded.OrderId, _cancellationToken)
        ).StatusChanges.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RetrieveOrders_ReturnsOwnOrdersNewestFirstPaged()
    {
        var older = await _database.SeedAsync(
            OrderApiDatabase.NewOrder(
                UserId,
                Guid.NewGuid(),
                new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc)
            ),
            _cancellationToken
        );
        var newer = await _database.SeedAsync(
            OrderApiDatabase.NewOrder(
                UserId,
                Guid.NewGuid(),
                new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc)
            ),
            _cancellationToken
        );
        await _database.SeedOrderAsync("user-2", OrderStatus.Pending, _cancellationToken);

        var first = await ActAsync(r =>
            r.RetrieveOrdersAsync(
                UserId,
                new PageRequest { Page = 1, PageSize = 1 },
                _cancellationToken
            )
        );
        var second = await ActAsync(r =>
            r.RetrieveOrdersAsync(
                UserId,
                new PageRequest { Page = 2, PageSize = 1 },
                _cancellationToken
            )
        );

        first.TotalCount.ShouldBe(2);
        first.Items.ShouldHaveSingleItem().OrderId.ShouldBe(newer.OrderId);
        second.Items.ShouldHaveSingleItem().OrderId.ShouldBe(older.OrderId);
    }

    private Task<ChangeOrderStatusResult> CancelAsync(
        int orderId,
        params IInterceptor[] interceptors
    ) =>
        ActAsync(
            r =>
                r.ChangeStatusAsync(
                    orderId,
                    UserId,
                    OrderStatus.Cancelled,
                    UserId,
                    _cancellationToken
                ),
            interceptors
        );

    private Task<PlaceOrderResult> PlaceOrderAsync(params IInterceptor[] interceptors) =>
        ActAsync(
            r => r.PlaceOrderAsync(UserId, Email, _key, Address, _cancellationToken),
            interceptors
        );

    private async Task<T> ActAsync<T>(
        Func<OrderRepository, Task<T>> action,
        params IInterceptor[] interceptors
    )
    {
        await using var db = _database.CreateContext(interceptors);
        return await action(new OrderRepository(db));
    }

    // The checkout wrote nothing: no order of its own, the stock as expected, the cart item still there.
    private async Task ShouldBeUnchangedAsync(
        int productId,
        short unitsInStock,
        int expectedOrders = 0
    )
    {
        (await _database.LoadOrdersAsync(_cancellationToken)).Count.ShouldBe(expectedOrders);
        (await _database.FindAsync<Product>(productId, _cancellationToken))
            .ShouldNotBeNull()
            .UnitsInStock.ShouldBe(unitsInStock);
        (await _database.LoadCartAsync(UserId, _cancellationToken))
            .ShouldNotBeNull()
            .CartItems.ShouldHaveSingleItem();
    }
}
