using EShop.Data;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace EShop.Tests.Common;

/// <summary>
/// The PostgreSQL database of one test project: created and migrated once per test run, emptied before each test.
/// By default it lives on a throwaway PostgreSQL container of the project (Testcontainers), removed afterwards;
/// the environment variable ConnectionStrings__EShopTests points the tests at another server instead.
/// </summary>
public abstract class TestDatabase : IAsyncLifetime
{
    // Only an environment variable, not user-secrets: a forgotten secret must not silently replace the container.
    public const string OverrideVariable = "ConnectionStrings__EShopTests";

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

    private readonly string _suffix;
    private PostgreSqlContainer? _container;
    private string? _databaseName;
    private string? _connectionString;
    private DbContextOptions<EShopDbContext>? _options;

    /// <param name="suffix">Appended to the server's database name, e.g. eshop_test_shoppingcart.</param>
    protected TestDatabase(string suffix)
    {
        _suffix = suffix;
    }

    public string DatabaseName => _databaseName ?? throw NotInitialized();

    public string ConnectionString => _connectionString ?? throw NotInitialized();

    // Interceptors let a test act in the middle of SaveChanges (see BeforeSaveChangesInterceptor).
    public EShopDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(
            new DbContextOptionsBuilder<EShopDbContext>(_options ?? throw NotInitialized())
                .AddInterceptors(interceptors)
                .Options
        );

    // Starts the container (unless overridden), creates the project's database and applies the migrations.
    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var serverConnectionString =
            Environment.GetEnvironmentVariable(OverrideVariable)
            ?? await StartContainerAsync(cancellationToken);

        // The tests delete all data: refuse to run against anything but a test database.
        var builder = new NpgsqlConnectionStringBuilder(serverConnectionString);
        if (
            builder.Database is null
            || !builder.Database.EndsWith("_test", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"Database '{builder.Database}' is not a test database: its name must end with '_test'."
            );
        }
        builder.Database = $"{builder.Database}_{_suffix}";

        _databaseName = builder.Database;
        _connectionString = builder.ConnectionString;
        // Same provider settings as AddEShopDbContext.
        _options = new DbContextOptionsBuilder<EShopDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        await using var db = CreateContext();
        await db.Database.MigrateAsync(cancellationToken);
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

    public async Task<T> SeedAsync<T>(T entity, CancellationToken cancellationToken)
        where T : class
    {
        await using var db = CreateContext();
        db.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    // Reads on a fresh DbContext, so the result is what PostgreSQL stores.
    public async Task<T?> FindAsync<T>(int id, CancellationToken cancellationToken)
        where T : class
    {
        await using var db = CreateContext();
        return await db.FindAsync<T>([id], cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    // The same major version as docker-compose.yml; its superuser may create the project's database.
    private async Task<string> StartContainerAsync(CancellationToken cancellationToken)
    {
        _container = new PostgreSqlBuilder("postgres:18").WithDatabase("eshop_test").Build();
        await _container.StartAsync(cancellationToken);
        return _container.GetConnectionString();
    }

    private static InvalidOperationException NotInitialized() =>
        new("The test database has not been initialized.");
}
