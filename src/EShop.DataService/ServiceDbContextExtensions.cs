using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.DataService;

public static class ServiceDbContextExtensions
{
    // One database for all services until each gets its own (#78).
    public const string ConnectionStringName = "EShop";

    /// <summary>
    /// Registers a service's DbContext with PostgreSQL, snake_case naming and its own schema.
    /// The connection string is read from <c>ConnectionStrings:EShop</c>.
    /// </summary>
    public static IServiceCollection AddServiceDbContext<TContext>(
        this IServiceCollection services,
        IConfiguration configuration
    )
        where TContext : DbContext, IServiceDbContext
    {
        var connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with "
                    + "dotnet user-secrets set \"ConnectionStrings:EShop\" \"<value>\" --project <service project>"
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
        where TContext : DbContext, IServiceDbContext =>
        options
            // HasDefaultSchema does not move the migrations history table: without this, every context would share
            // public.__EFMigrationsHistory and see the others' migrations as unknown.
            .UseNpgsql(
                connectionString,
                npgsql =>
                    npgsql.MigrationsHistoryTable(
                        HistoryRepository.DefaultTableName,
                        TContext.Schema
                    )
            )
            .UseSnakeCaseNamingConvention();
}
