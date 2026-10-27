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

    // An order as the API stores it: every column the response needs is set.
    public static Order NewOrder(string userId, Guid idempotencyKey) =>
        new()
        {
            UserId = userId,
            IdempotencyKey = idempotencyKey,
            OrderDate = new DateTime(2026, 10, 27),
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

    public async Task<Cart?> LoadCartAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        return await db
            .Carts.AsNoTracking()
            .Include(c => c.CartItems)
            .SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }
}
