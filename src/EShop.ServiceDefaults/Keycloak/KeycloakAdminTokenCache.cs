using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EShop.ServiceDefaults.Keycloak;

// One service-account token (client credentials) for all Admin API calls, renewed shortly before it expires:
// without the cache every call would first ask Keycloak for a token.
public sealed class KeycloakAdminTokenCache
{
    // The named HttpClient of the token requests; public so tests can watch them.
    public const string TokenClientName = "keycloak-token";
    private static readonly TimeSpan RenewBefore = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakAdminOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _lock = new(1, 1);

    // One reference, so a reader never sees a new token with an old expiry.
    private CachedToken? _current;

    public KeycloakAdminTokenCache(
        IHttpClientFactory httpClientFactory,
        IOptions<KeycloakAdminOptions> options,
        TimeProvider timeProvider
    )
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<string> GetTokenAsync(CancellationToken cancellationToken)
    {
        if (Valid(_current) is { } token)
        {
            return token;
        }

        // Concurrent requests with an expired token wait here, and only the first asks Keycloak.
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (Valid(_current) is { } renewed)
            {
                return renewed;
            }

            var client = _httpClientFactory.CreateClient(TokenClientName);
            using var response = await client.PostAsync(
                $"realms/{Uri.EscapeDataString(_options.Realm)}/protocol/openid-connect/token",
                new FormUrlEncodedContent(
                    [
                        new("grant_type", "client_credentials"),
                        new("client_id", _options.ClientId),
                        new("client_secret", _options.ClientSecret),
                    ]
                ),
                cancellationToken
            );
            response.EnsureSuccessStatusCode();
            var body =
                await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
                ?? throw new HttpRequestException("Keycloak returned an empty token response.");

            _current = new CachedToken(
                body.AccessToken,
                _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(body.ExpiresIn) - RenewBefore
            );
            return body.AccessToken;
        }
        finally
        {
            _lock.Release();
        }
    }

    private string? Valid(CachedToken? token) =>
        token != null && _timeProvider.GetUtcNow() < token.ExpiresAt ? token.Value : null;

    private sealed record CachedToken(string Value, DateTimeOffset ExpiresAt);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn
    );
}
