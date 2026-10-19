using EShop.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

[assembly: AssemblyFixture(typeof(EShop.ShoppingCart.Tests.Integration.TestDatabase))]
// All tests share one database and empty it before each test, so they must not run in parallel.
[assembly: Xunit.v3.Parallelization(Mode = Xunit.Sdk.ParallelMode.None)]

namespace EShop.ShoppingCart.Tests.Integration;

/// <summary>
/// The PostgreSQL test database: migrated once per test run, emptied before each test.
/// </summary>
public sealed class TestDatabase : IAsyncLifetime
{
    public const string ConnectionStringName = "EShopTests";

    private readonly DbContextOptions<EShopDbContext> _options;

    public TestDatabase()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets("eshop")
            .AddEnvironmentVariables()
            .Build();

        var connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with "
                    + "dotnet user-secrets set \"ConnectionStrings:EShopTests\" \"<value>\" --project src/EShop.ProductApi"
            );

        // The tests delete all data: refuse to run against anything but a test database.
        var database = new NpgsqlConnectionStringBuilder(connectionString).Database;
        if (database is null || !database.EndsWith("_test", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Database '{database}' is not a test database: its name must end with '_test'."
            );
        }

        // Same provider settings as AddEShopDbContext.
        _options = new DbContextOptionsBuilder<EShopDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
    }

    public EShopDbContext CreateContext() => new(_options);

    public async ValueTask InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE cart_items, carts, products RESTART IDENTITY CASCADE",
            cancellationToken
        );
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
