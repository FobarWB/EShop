using EShop.CustomerApi.Accounts;
using EShop.Tests.Common;
using Microsoft.AspNetCore.Hosting;

namespace EShop.CustomerApi.Tests.Integration;

// A class fixture: one in-memory API per test class, using this project's test database.
// The API tests do not reach Keycloak; the provisioner is tested in KeycloakAccountProvisionerTests.
public sealed class CustomerApiFactory(CustomerApiDatabase database)
    : EShopApiFactory<Program>(database)
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // ValidateOnStart needs a secret; the other Keycloak settings come from appsettings.json.
        builder.UseSetting(
            $"{KeycloakAccountProvisioner.SettingsSection}:ClientSecret",
            "test-secret"
        );
    }
}
