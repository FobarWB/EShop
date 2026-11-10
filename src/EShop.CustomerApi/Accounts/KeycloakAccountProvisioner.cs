using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using EShop.ServiceDefaults.Keycloak;
using Microsoft.Extensions.Options;

namespace EShop.CustomerApi.Accounts;

// Keycloak's Admin API as CustomerApi's service account (eshop-customerapi: view-users, manage-users).
// Failures throw: the consumer retries the message with delays.
public sealed class KeycloakAccountProvisioner : IAccountProvisioner
{
    // CustomerApi's Keycloak client; the secret is KeycloakAdmin:CustomerApi:ClientSecret.
    public const string SettingsSection = "KeycloakAdmin:CustomerApi";

    private readonly HttpClient _httpClient;
    private readonly GuestAccountsOptions _guestAccounts;
    private readonly string _users;

    public KeycloakAccountProvisioner(
        HttpClient httpClient,
        IOptions<KeycloakAdminOptions> keycloak,
        IOptions<GuestAccountsOptions> guestAccounts
    )
    {
        _httpClient = httpClient;
        _guestAccounts = guestAccounts.Value;
        _users = $"admin/realms/{Uri.EscapeDataString(keycloak.Value.Realm)}/users";
    }

    public async Task<ProvisionedAccount> FindOrCreateAsync(
        string email,
        CancellationToken cancellationToken
    )
    {
        var userId =
            await FindAsync(email, cancellationToken)
            ?? await CreateAsync(email, cancellationToken)
            // 409: a parallel delivery of the same message created the user first.
            ?? await FindAsync(email, cancellationToken)
            ?? throw new InvalidOperationException(
                "Keycloak reported the user as existing, but it was not found."
            );

        return new ProvisionedAccount(userId, !await HasPasswordAsync(userId, cancellationToken));
    }

    public async Task SendSetPasswordEmailAsync(string userId, CancellationToken cancellationToken)
    {
        // After the password is set, Keycloak sends the browser to the shop (a redirect URI of the client).
        var url =
            $"{_users}/{Uri.EscapeDataString(userId)}/execute-actions-email"
            + $"?client_id={Uri.EscapeDataString(_guestAccounts.ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(_guestAccounts.RedirectUri)}"
            + $"&lifespan={(int)_guestAccounts.LinkLifespan.TotalSeconds}";
        // VERIFY_EMAIL too: the link itself proves the mailbox. With UPDATE_PASSWORD alone the email stays unverified,
        // and the realm's verifyEmail sends a second email at the first sign-in (seen in the compose check of #45).
        using var response = await _httpClient.PutAsJsonAsync(
            url,
            new[] { "VERIFY_EMAIL", "UPDATE_PASSWORD" },
            cancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    // exact=true: the whole address, not a substring search.
    private async Task<string?> FindAsync(string email, CancellationToken cancellationToken)
    {
        var users = await _httpClient.GetFromJsonAsync<List<JsonElement>>(
            $"{_users}?email={Uri.EscapeDataString(email)}&exact=true&briefRepresentation=true&max=1",
            cancellationToken
        );
        return users is [var user, ..] ? user.GetProperty("id").GetString() : null;
    }

    // The email is the user name, as on the sign-up form (registrationEmailAsUsername). Not verified yet:
    // the set-password link proves the mailbox. null: the user exists already (409).
    private async Task<string?> CreateAsync(string email, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            _users,
            new
            {
                username = email,
                email,
                enabled = true,
                emailVerified = false,
            },
            cancellationToken
        );
        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();

        // 201 Created has no body; the id is the last segment of Location (.../users/{id}).
        return response.Headers.Location?.Segments[^1]
            ?? throw new InvalidOperationException("Keycloak created a user without a Location.");
    }

    private async Task<bool> HasPasswordAsync(string userId, CancellationToken cancellationToken)
    {
        var credentials = await _httpClient.GetFromJsonAsync<List<JsonElement>>(
            $"{_users}/{Uri.EscapeDataString(userId)}/credentials",
            cancellationToken
        );
        return credentials?.Any(c => c.GetProperty("type").GetString() == "password") == true;
    }
}
