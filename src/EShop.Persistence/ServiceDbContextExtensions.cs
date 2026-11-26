using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace EShop.Persistence;

public static class ServiceDbContextExtensions
{
    /// <summary>
    /// Registers a service's DbContext with PostgreSQL, snake_case naming and its own schema.
    /// The connection string is read from <c>ConnectionStrings:&lt;TContext.ConnectionStringName&gt;</c>.
    /// </summary>
    public static IServiceCollection AddServiceDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TContext : DbContext, IServiceDbContext
    {
        var connectionString =
            configuration.GetConnectionString(TContext.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{TContext.ConnectionStringName}' is not configured. Set it with "
                    + $"dotnet user-secrets set \"ConnectionStrings:{TContext.ConnectionStringName}\" \"<value>\" --project <service project>"
            );

        services.AddDbContext<TContext>(options =>
            options.UseServiceNpgsql<TContext>(connectionString)
        );
        return services;
    }

    /// <summary>
    /// The provider settings of every service's DbContext, shared with the design-time factories and the test databases.
    /// </summary>
    public static DbContextOptionsBuilder UseServiceNpgsql<TContext>(
        this DbContextOptionsBuilder options,
        string connectionString
    )
        where TContext : DbContext, IServiceDbContext
    {
        // EF Core qualifies its own SQL with the schema; the search path resolves the table names of a service's raw SQL
        // (FromSql, ExecuteSql) to its schema too, never to public or another service's schema.
        var connection = new NpgsqlConnectionStringBuilder(connectionString)
        {
            SearchPath = TContext.Schema,
        };
        return options
            // HasDefaultSchema does not move the migrations history table: without this, every context would share
            // public.__EFMigrationsHistory and see the others' migrations as unknown.
            .UseNpgsql(
                connection.ConnectionString,
                npgsql =>
                    npgsql.MigrationsHistoryTable(
                        HistoryRepository.DefaultTableName,
                        TContext.Schema
                    )
            )
            .UseSnakeCaseNamingConvention();
    }

    /// <summary>
    /// The options of a design-time factory: dotnet ef and the migrations bundle create the context through it instead
    /// of starting the service, so adding a migration or building the bundle needs no Keycloak, RabbitMQ or user-secrets.
    /// </summary>
    public static DbContextOptions<TContext> CreateDesignTimeOptions<TContext>()
        where TContext : DbContext, IServiceDbContext
    {
        // migrations add never connects; the bundle and database update get the real one through --connection.
        var connectionString =
            Environment.GetEnvironmentVariable(
                $"ConnectionStrings__{TContext.ConnectionStringName}"
            ) ?? $"Host=localhost;Database={TContext.Schema}";
        var options = new DbContextOptionsBuilder<TContext>();
        options.UseServiceNpgsql<TContext>(connectionString);
        return options.Options;
    }
}
