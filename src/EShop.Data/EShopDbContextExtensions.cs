using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.Data;

public static class EShopDbContextExtensions
{
    public const string ConnectionStringName = "EShop";

    /// <summary>
    /// Registers <see cref="EShopDbContext"/> with the PostgreSQL provider and snake_case naming.
    /// The connection string is read from <c>ConnectionStrings:EShop</c>.
    /// </summary>
    public static IServiceCollection AddEShopDbContext(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration.GetConnectionString(ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' is not configured. Set it with "
                    + "dotnet user-secrets set \"ConnectionStrings:EShop\" \"<value>\" --project src/EShop.ProductApi"
            );

        services.AddDbContext<EShopDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention()
        );
        return services;
    }
}
