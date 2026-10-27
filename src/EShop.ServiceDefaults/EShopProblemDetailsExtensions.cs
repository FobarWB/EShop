using Microsoft.Extensions.DependencyInjection;

namespace EShop.ServiceDefaults;

public static class EShopProblemDetailsExtensions
{
    /// <summary>
    /// Error responses as ProblemDetails (<c>application/problem+json</c>): unhandled exceptions become 500
    /// without a stack trace, foreign key violations and concurrency conflicts 409.
    /// Needs <c>app.UseExceptionHandler()</c>.
    /// </summary>
    public static IServiceCollection AddEShopProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<ForeignKeyViolationHandler>();
        services.AddExceptionHandler<ConcurrencyConflictHandler>();
        return services;
    }
}
