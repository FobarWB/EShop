using EShop.Persistence;
using Microsoft.EntityFrameworkCore.Design;

namespace EShop.NotificationService.Data;

// For dotnet ef and the migrations bundle: they create the context here instead of starting the worker.
public sealed class NotificationDbContextFactory
    : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) =>
        new(ServiceDbContextExtensions.CreateDesignTimeOptions<NotificationDbContext>());
}
