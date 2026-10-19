using EShop.Data.Entities;
using EShop.Tests.Common;
using Microsoft.EntityFrameworkCore;

[assembly: AssemblyFixture(typeof(EShop.ShoppingCart.Tests.Integration.ShoppingCartDatabase))]
// All tests of this project share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.ShoppingCart.Tests.Integration;

public sealed class ShoppingCartDatabase : TestDatabase
{
    public ShoppingCartDatabase()
        : base("shoppingcart") { }

    public async Task<Cart?> LoadCartAsync(string userId, CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        return await db
            .Carts.AsNoTracking()
            .Include(c => c.CartItems)
            .SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);
    }
}
