using Microsoft.EntityFrameworkCore; // UseSqlServer
using Microsoft.Extensions.DependencyInjection; // IServiceCollection

namespace EShop.Data
{
    public static  class EShopDbContextExtensions
    {
 
    /// <summary>
    /// Adds EShopDbContext to the specified IServiceCollection. Uses the SqlServer database provider.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="connectionString">Set to override the default.</param>
    /// <returns>An IServiceCollection that can be used to add more services.</returns>
    public static IServiceCollection AddEShopDbContext(
        this IServiceCollection services, string connectionString =
        "Data Source=.;Initial Catalog=Northwind;"
        + "Integrated Security=true;MultipleActiveResultsets=true;")
        {
            services.AddDbContext<EShopDbContext>(options =>
            options.UseSqlServer(connectionString),contextLifetime: ServiceLifetime.Transient);
            return services;
        }
    }
}