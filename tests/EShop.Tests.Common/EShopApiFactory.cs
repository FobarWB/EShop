using EShop.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace EShop.Tests.Common;

/// <summary>
/// Runs an API in memory (TestServer) with its connection string pointed at the test database.
/// </summary>
public class EShopApiFactory<TEntryPoint> : WebApplicationFactory<TEntryPoint>
    where TEntryPoint : class
{
    private readonly TestDatabase _database;

    public EShopApiFactory(TestDatabase database)
    {
        _database = database;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(
            $"ConnectionStrings:{EShopDbContextExtensions.ConnectionStringName}",
            _database.ConnectionString
        );
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // The API must never write to the development database: check what it really uses.
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<EShopDbContext>();
        var database = new NpgsqlConnectionStringBuilder(
            db.Database.GetConnectionString()
        ).Database;
        if (database != _database.DatabaseName)
        {
            host.Dispose();
            throw new InvalidOperationException(
                $"The API uses database '{database}' instead of '{_database.DatabaseName}'."
            );
        }

        return host;
    }
}
