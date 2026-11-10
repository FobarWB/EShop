using System.Net.Http.Headers;

namespace EShop.ServiceDefaults.Keycloak;

// Adds the service-account token to every request of a typed Admin API client.
public sealed class KeycloakAdminTokenHandler : DelegatingHandler
{
    private readonly KeycloakAdminTokenCache _tokens;

    public KeycloakAdminTokenHandler(KeycloakAdminTokenCache tokens)
    {
        _tokens = tokens;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _tokens.GetTokenAsync(cancellationToken)
        );
        return await base.SendAsync(request, cancellationToken);
    }
}
