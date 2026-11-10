using System.ComponentModel.DataAnnotations;

namespace EShop.CustomerApi.Accounts;

// The set-password link of a guest account (Keycloak's execute-actions email).
public sealed class GuestAccountsOptions
{
    public const string SectionName = "GuestAccounts";

    // The client and the address Keycloak returns to after the password is set; the address must be one of the
    // client's redirect URIs.
    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string RedirectUri { get; set; } = "";

    // How long the link works; afterwards "Forgot password" on the sign-in page sends a new one.
    public TimeSpan LinkLifespan { get; set; } = TimeSpan.FromHours(24);
}
