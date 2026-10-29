using EShop.Data.Entities;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(typeof(EShop.OrderApi.Tests.Integration.OrderApiDatabase))]
// All tests of this project share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.OrderApi.Tests.Integration;

public sealed class OrderApiDatabase : TestDatabase
{
    public OrderApiDatabase()
        : base("orderapi") { }

    public Task<Product> SeedProductAsync(
        string name,
        decimal unitPrice,
        short unitsInStock,
        CancellationToken cancellationToken
    ) =>
        SeedAsync(
            new Product
            {
                ProductName = name,
                UnitPrice = unitPrice,
                UnitsInStock = unitsInStock,
            },
            cancellationToken
        );

    public Task<Cart> SeedCartAsync(
        string userId,
        int productId,
        int quantity,
        CancellationToken cancellationToken
    ) =>
        SeedAsync(
            new Cart
            {
                UserId = userId,
                CartItems = [new CartItem { ProductId = productId, Quantity = quantity }],
            },
            cancellationToken
        );

    // The shortest way through the lifecycle to each status: Status has no public setter.
    private static readonly Dictionary<OrderStatus, OrderStatus[]> PathTo = new()
    {
        [OrderStatus.Pending] = [],
        [OrderStatus.Paid] = [OrderStatus.Paid],
        [OrderStatus.Shipped] = [OrderStatus.Paid, OrderStatus.Shipped],
        [OrderStatus.Delivered] = [OrderStatus.Paid, OrderStatus.Shipped, OrderStatus.Delivered],
        [OrderStatus.Cancelled] = [OrderStatus.Cancelled],
    };

    // An order of the user in the given status, with one line per product.
    public Task<Order> SeedOrderAsync(
        string userId,
        OrderStatus status,
        CancellationToken cancellationToken,
        params (int ProductId, short Quantity)[] lines
    )
    {
        var order = NewOrder(userId, Guid.NewGuid());
        foreach (var (productId, quantity) in lines)
        {
            order.OrderDetails.Add(
                new OrderDetail
                {
                    ProductId = productId,
                    ProductName = "Test product",
                    UnitPrice = 10m,
                    Quantity = quantity,
                }
            );
        }
        foreach (var next in PathTo[status])
        {
            order.TryChangeStatus(next, "seed", DateTime.UtcNow).ShouldBeTrue();
        }
        return SeedAsync(order, cancellationToken);
    }

    // The order as stored, with its lines and its status journal.
    public async Task<Order> LoadOrderAsync(int orderId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        return await db
            .Orders.AsNoTracking()
            .Include(o => o.OrderDetails)
            .Include(o => o.StatusChanges)
            .AsSplitQuery()
            .SingleAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public const string BuyerEmail = "jane@example.com";

    // An order as the API stores it: every column the response needs is set.
    public static Order NewOrder(string userId, Guid idempotencyKey, DateTime? orderDate = null) =>
        new()
        {
            UserId = userId,
            CustomerEmail = BuyerEmail,
            IdempotencyKey = idempotencyKey,
            OrderDate = orderDate ?? new DateTime(2026, 10, 27, 0, 0, 0, DateTimeKind.Utc),
            Freight = 5m,
            ShipName = "Jane Doe",
            ShipAddress = "1 Main St",
            ShipCity = "Berlin",
            ShipPostalCode = "10115",
            ShipCountry = "Germany",
        };

    public async Task<List<Order>> LoadOrdersAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        return await db
            .Orders.AsNoTracking()
            .Include(o => o.OrderDetails)
            .ToListAsync(cancellationToken);
    }

    // The outbox rows in the order the events occurred.
    public async Task<List<OutboxMessage>> LoadOutboxAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        return await db
            .OutboxMessages.AsNoTracking()
            .OrderBy(m => m.OccurredAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Cart?> LoadCartAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        return await db
            .Carts.AsNoTracking()
            .Include(c => c.CartItems)
            .SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }
}
