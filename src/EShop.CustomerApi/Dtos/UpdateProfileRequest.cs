using System.ComponentModel.DataAnnotations;

namespace EShop.CustomerApi.Dtos;

// Body of PUT api/customers/me. Email and name are changed in the Keycloak account console, not here.
public sealed record UpdateProfileRequest
{
    // Digits, spaces, parentheses and hyphens, an optional leading "+"; starts with a digit or "(", ends with a digit.
    // Not a full number check (that needs libphonenumber), it only keeps out text. Null or empty removes the phone.
    [StringLength(24)]
    [RegularExpression(@"^\+?[0-9(][0-9 ()\-]{3,21}[0-9]$")]
    public string? Phone { get; init; }
}
