using System.Net.Http.Json;
using System.Text.Json;
using EShop.ServiceDefaults.Keycloak;
using Microsoft.Extensions.Options;

namespace EShop.OrderApi.Accounts;

public sealed class KeycloakAccountDirectory : IAccountDirectory
{
    private readonly HttpClient _httpClient;
    private readonly string _realm;

    public KeycloakAccountDirectory(HttpClient httpClient, IOptions<KeycloakAdminOptions> options)
    {
        _httpClient = httpClient;
        _realm = Uri.EscapeDataString(options.Value.Realm);
    }

    public async Task<bool> AccountExistsAsync(string email, CancellationToken cancellationToken)
    {
        // exact=true: the whole address, not a substring search; one match is enough, and only the ids are needed.
        var url =
            $"admin/realms/{_realm}/users?email={Uri.EscapeDataString(email)}&exact=true&briefRepresentation=true&max=1";
        try
        {
            var users = await _httpClient.GetFromJsonAsync<List<JsonElement>>(
                url,
                cancellationToken
            );
            return users is { Count: > 0 };
        }
        catch (Exception ex)
            when (ex is HttpRequestException
                || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested)
            )
        {
            // A timeout surfaces as TaskCanceledException; a cancelled request is the caller's and is not wrapped.
            throw new IdentityProviderUnavailableException(
                "Keycloak could not be asked for the account.",
                ex
            );
        }
    }
}
