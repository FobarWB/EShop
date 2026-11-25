using EShop.DataService;
using Microsoft.EntityFrameworkCore.Design;

namespace EShop.CustomerApi.Data;

// For dotnet ef and the migrations bundle: they create the context here instead of starting the API.
public sealed class CustomerDbContextFactory : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args) =>
        new(ServiceDbContextExtensions.CreateDesignTimeOptions<CustomerDbContext>());
}
