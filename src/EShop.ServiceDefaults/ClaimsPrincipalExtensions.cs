using System.Security.Claims;

namespace EShop.ServiceDefaults;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// The id of the signed-in user: the token's subject (<c>sub</c>), the same value Keycloak keeps for the user.
    /// Only for endpoints that require a signed-in user: a validated token always has a subject.
    /// </summary>
    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub")
        ?? throw new InvalidOperationException("The signed-in user has no 'sub' claim.");
}
