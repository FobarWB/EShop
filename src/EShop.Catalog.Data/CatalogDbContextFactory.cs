using EShop.DataService;
using Microsoft.EntityFrameworkCore.Design;

namespace EShop.Catalog.Data;

// For dotnet ef and the migrations bundle: they create the context here instead of starting ProductApi.
public sealed class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args) =>
        new(ServiceDbContextExtensions.CreateDesignTimeOptions<CatalogDbContext>());
}
