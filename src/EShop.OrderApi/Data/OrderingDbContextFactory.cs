using EShop.DataService;
using Microsoft.EntityFrameworkCore.Design;

namespace EShop.OrderApi.Data;

// For dotnet ef and the migrations bundle: they create the context here instead of starting the API.
public sealed class OrderingDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args) =>
        new(ServiceDbContextExtensions.CreateDesignTimeOptions<OrderingDbContext>());
}
