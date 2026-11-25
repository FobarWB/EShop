using EShop.DataService;
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
public abstract class TestDatabase<TContext> : IAsyncLifetime
    where TContext : DbContext, IServiceDbContext
{
    // Only an environment variable, not user-secrets: a forgotten secret must not silently replace the container.
    public const string OverrideVariable = "ConnectionStrings__EShopTests";

    private readonly string _suffix;
    private PostgreSqlContainer? _container;
    private string? _databaseName;
    private string? _connectionString;
    private DbContextOptions<TContext>? _options;

    /// <param name="suffix">Appended to the server's database name, e.g. eshop_test_shoppingcart.</param>
    protected TestDatabase(string suffix)
    {
        _suffix = suffix;
    }

    public string DatabaseName => _databaseName ?? throw NotInitialized();

    public string ConnectionString => _connectionString ?? throw NotInitialized();

    // Interceptors let a test act in the middle of SaveChanges (see BeforeSaveChangesInterceptor).
    public TContext CreateContext(params IInterceptor[] interceptors) =>
        NewContext(
            new DbContextOptionsBuilder<TContext>(_options ?? throw NotInitialized())
                .AddInterceptors(interceptors)
                .Options
        );

    // The context's constructor: the helper cannot call it with a generic type.
    protected abstract TContext NewContext(DbContextOptions<TContext> options);

    // The schema of a service's context comes from its migrations.
    protected virtual Task CreateSchemaAsync(TContext db, CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);

    // Starts the container (unless overridden), creates the project's database and its schema.
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
        // The service's provider settings, so the tests run on the same model and migrations history.
        var options = new DbContextOptionsBuilder<TContext>();
        options.UseServiceNpgsql<TContext>(_connectionString);
        _options = options.Options;

        await using var db = CreateContext();
        await CreateSchemaAsync(db, cancellationToken);
    }

    // Empties the tables of the context's schema, except its migrations history. CASCADE follows foreign keys,
    // RESTART IDENTITY resets the id sequences.
    public async Task ResetAsync(CancellationToken cancellationToken)
    {
        await using var db = CreateContext();
        var tables = await db
            .Database.SqlQuery<string>(
                $"""
                SELECT format('%I.%I', schemaname, tablename) AS "Value"
                FROM pg_tables
                WHERE schemaname = {TContext.Schema} AND tablename <> '__EFMigrationsHistory'
                """
            )
            .ToListAsync(cancellationToken);
        if (tables.Count == 0)
        {
            return;
        }

        // Not a parameter: TRUNCATE takes identifiers, and format('%I') has quoted them.
        var truncate = $"TRUNCATE {string.Join(", ", tables)} RESTART IDENTITY CASCADE";
        await db.Database.ExecuteSqlRawAsync(truncate, cancellationToken);
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
