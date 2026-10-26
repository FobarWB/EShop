using System.Security.Claims;
using System.Text.Encodings.Web;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EShop.Tests.Common;

/// <summary>
/// Signs in the user named in the X-Test-User header (roles from X-Test-Roles), so API tests need no Keycloak.
/// A request without the header stays anonymous.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserHeader = "X-Test-User";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? user = Request.Headers[UserHeader];
        if (string.IsNullOrEmpty(user))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        List<Claim> claims =
        [
            new("sub", user),
            new(EShopAuthenticationExtensions.NameClaimType, user),
        ];
        claims.AddRange(
            Request
                .Headers[RolesHeader]
                .ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(EShopAuthenticationExtensions.RoleClaimType, role))
        );
        var identity = new ClaimsIdentity(
            claims,
            SchemeName,
            EShopAuthenticationExtensions.NameClaimType,
            EShopAuthenticationExtensions.RoleClaimType
        );

        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)
            )
        );
    }
}
