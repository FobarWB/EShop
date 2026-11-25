using EShop.ShoppingCart.Data;
using EShop.ShoppingCart.Data.Entities;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(typeof(EShop.ShoppingCart.Tests.Integration.ShoppingCartDatabase))]
// All tests of this project share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.ShoppingCart.Tests.Integration;

public sealed class ShoppingCartDatabase : TestDatabase<CartDbContext>
{
    private int _lastProductId;

    public ShoppingCartDatabase()
        : base("shoppingcart") { }

    protected override CartDbContext NewContext(DbContextOptions<CartDbContext> options) =>
        new(options);

    // The cart's copy of a product, as ProductApi's events would store it (#75). The ids come from the events, not
    // from a sequence: a counter keeps them unique within the run.
    public async Task<int> SeedCartProductAsync(
        short unitsInStock,
        CancellationToken cancellationToken,
        string name = "Test product",
        int? categoryId = null,
        decimal? unitPrice = null
    )
    {
        var product = await SeedAsync(
            new CartProduct
            {
                ProductId = Interlocked.Increment(ref _lastProductId),
                ProductName = name,
                CategoryId = categoryId,
                UnitPrice = unitPrice,
                UnitsInStock = unitsInStock,
                Revision = 1,
            },
            cancellationToken
        );
        return product.ProductId;
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
