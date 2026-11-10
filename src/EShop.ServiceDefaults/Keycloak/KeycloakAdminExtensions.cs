using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace EShop.ServiceDefaults.Keycloak;

public static class KeycloakAdminExtensions
{
    /// <summary>
    /// A typed client for the Keycloak Admin API that sends the service-account token of the client configured in
    /// <paramref name="sectionName"/> (e.g. <c>KeycloakAdmin:OrderApi</c>), and 503 ProblemDetails when Keycloak is
    /// unavailable. A missing setting stops the API at start.
    /// </summary>
    public static IHttpClientBuilder AddKeycloakAdminClient<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName
    )
        where TClient : class
        where TImplementation : class, TClient
    {
        services
            .AddOptions<KeycloakAdminOptions>()
            .Bind(configuration.GetSection(sectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<KeycloakAdminTokenCache>();
        services.TryAddTransient<KeycloakAdminTokenHandler>();
        services.AddExceptionHandler<IdentityProviderUnavailableHandler>();

        services.AddHttpClient(KeycloakAdminTokenCache.TokenClientName, Configure);
        return services
            .AddHttpClient<TClient, TImplementation>(Configure)
            .AddHttpMessageHandler<KeycloakAdminTokenHandler>();
    }

    private static void Configure(IServiceProvider services, HttpClient client)
    {
        var options = services.GetRequiredService<IOptions<KeycloakAdminOptions>>().Value;
        // A trailing slash, so relative paths are appended to the base address instead of replacing its last segment.
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        // A checkout waits for this call: fail fast with 503 instead of the default 100 s.
        client.Timeout = TimeSpan.FromSeconds(10);
    }
}
