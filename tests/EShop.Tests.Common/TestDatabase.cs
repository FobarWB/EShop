using EShop.Data;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace EShop.Tests.Common;

/// <summary>
/// The PostgreSQL database of one test project: created and migrated once per test run,
/// emptied before each test. Each test project has its own database, so projects can run in parallel.
/// </summary>
public abstract class TestDatabase : IAsyncLifetime
{
    public const string ConnectionStringName = "EShopTests";

    // Empties every table except the migrations history. CASCADE follows foreign keys,
    // RESTART IDENTITY resets the id sequences.
    private const string ResetSql = """
        DO $$
        DECLARE tables text;
        BEGIN
            SELECT string_agg(format('%I', tablename), ', ') INTO tables
            FROM pg_tables
            WHERE schemaname = 'public' AND tablename <> '__EFMigrationsHistory';
            EXECUTE 'TRUNCATE ' || tables || ' RESTART IDENTITY CASCADE';
        END $$;
        """;

    private readonly DbContextOptions<EShopDbContext> _options;

    /// <param name="suffix">Appended to the configured database name, e.g. eshop_test_shoppingcart.</param>
    protected TestDatabase(string suffix)
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
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (
            builder.Database is null
            || !builder.Database.EndsWith("_test", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"Database '{builder.Database}' is not a test database: its name must end with '_test'."
            );
        }
        builder.Database = $"{builder.Database}_{suffix}";

        DatabaseName = builder.Database;
        ConnectionString = builder.ConnectionString;

        // Same provider settings as AddEShopDbContext.
        _options = new DbContextOptionsBuilder<EShopDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;
    }

    public string DatabaseName { get; }

    public string ConnectionString { get; }

    public EShopDbContext CreateContext() => new(_options);

    // Creates the database on the first run (the role needs CREATEDB) and applies pending migrations.
    public async ValueTask InitializeAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        await db.Database.ExecuteSqlRawAsync(ResetSql, cancellationToken);
    }

    public async Task<int> SeedProductAsync(short unitsInStock, CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        var product = new Product { ProductName = "Test product", UnitsInStock = unitsInStock };
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        return product.ProductId;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
