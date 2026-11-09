using System.ComponentModel.DataAnnotations;

namespace EShop.ServiceDefaults.Keycloak;

// A confidential client with a service account: the API calls the Admin API as itself, not as a user.
public sealed class KeycloakAdminOptions
{
    public const string SectionName = "KeycloakAdmin";

    [Required]
    public string BaseUrl { get; set; } = "";

    [Required]
    public string Realm { get; set; } = "";

    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string ClientSecret { get; set; } = "";
}
