using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.ServiceDefaults;

public static class EShopAuthenticationExtensions
{
    public const string SectionName = "Authentication";
    public const string AdminRole = "admin";

    // JWT claim names are kept as they are (MapInboundClaims = false): "sub", not a long .NET claim type.
    public const string NameClaimType = "preferred_username";
    public const string RoleClaimType = "role";

    /// <summary>
    /// Validates the JWTs issued by Keycloak (<c>Authentication:Authority</c>, <c>Authentication:Audience</c>)
    /// and requires a signed-in user for every endpoint that is not marked <c>[AllowAnonymous]</c>.
    /// </summary>
    public static IServiceCollection AddEShopAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var section = configuration.GetSection(SectionName);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // The expected issuer; the signing keys come from its metadata.
                options.Authority = section["Authority"];
                options.Audience = section["Audience"];
                // In containers the metadata is read through the internal address (keycloak:8080);
                // the issuer in it is still Authority.
                if (section["MetadataAddress"] is { Length: > 0 } metadataAddress)
                {
                    options.MetadataAddress = metadataAddress;
                }
                options.RequireHttpsMetadata = section.GetValue("RequireHttpsMetadata", true);
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = NameClaimType;
                options.TokenValidationParameters.RoleClaimType = RoleClaimType;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        AddKeycloakRealmRoles(context.Principal);
                        return Task.CompletedTask;
                    },
                };
            });

        // Closed by default: an endpoint someone forgot to mark needs a signed-in user instead of being public.
        services
            .AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    // Keycloak puts realm roles into one claim {"roles": [...]}; [Authorize(Roles = ...)] needs a claim per role.
    private static void AddKeycloakRealmRoles(ClaimsPrincipal? principal)
    {
        if (
            principal?.Identity is not ClaimsIdentity identity
            || identity.FindFirst("realm_access")?.Value is not { } realmAccess
        )
        {
            return;
        }

        using var json = JsonDocument.Parse(realmAccess);
        if (!json.RootElement.TryGetProperty("roles", out var roles))
        {
            return;
        }

        foreach (var role in roles.EnumerateArray())
        {
            if (role.GetString() is { } name)
            {
                identity.AddClaim(new Claim(RoleClaimType, name));
            }
        }
    }
}
