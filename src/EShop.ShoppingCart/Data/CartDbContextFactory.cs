using EShop.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace EShop.ShoppingCart.Data;

// For dotnet ef and the migrations bundle: they create the context here instead of starting the API.
public sealed class CartDbContextFactory : IDesignTimeDbContextFactory<CartDbContext>
{
    public CartDbContext CreateDbContext(string[] args) =>
        new(ServiceDbContextExtensions.CreateDesignTimeOptions<CartDbContext>());
}
