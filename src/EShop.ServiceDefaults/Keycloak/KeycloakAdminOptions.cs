using System.ComponentModel.DataAnnotations;

namespace EShop.ServiceDefaults.Keycloak;

// A confidential client with a service account: the API calls the Admin API as itself, not as a user.
// Each API has its own client and section (KeycloakAdmin:OrderApi, KeycloakAdmin:CustomerApi): the APIs share one
// user-secrets store, and one key for all would give every API the secret of the most privileged client.
public sealed class KeycloakAdminOptions
{
    [Required]
    public string BaseUrl { get; set; } = "";

    [Required]
    public string Realm { get; set; } = "";

    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string ClientSecret { get; set; } = "";
}
