using EShop.ServiceDefaults;

namespace EShop.Tests.Common;

/// <summary>Makes the requests of a test client come from a signed-in user (see <see cref="TestAuthHandler"/>).</summary>
public static class TestUsers
{
    public static HttpClient AsUser(this HttpClient client, string userId, params string[] roles)
    {
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserHeader, userId);
        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }
        return client;
    }

    public static HttpClient AsAdmin(this HttpClient client) =>
        client.AsUser("admin-1", EShopAuthenticationExtensions.AdminRole);
}
