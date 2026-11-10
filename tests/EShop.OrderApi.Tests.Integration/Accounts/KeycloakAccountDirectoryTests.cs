using EShop.OrderApi.Accounts;
using EShop.ServiceDefaults.Keycloak;
using EShop.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(KeycloakFixture))]

namespace EShop.OrderApi.Tests.Integration.Accounts;

// The real Admin API of a Keycloak that imported our realm file, through the same registration as Program.cs.
public class KeycloakAccountDirectoryTests
{
    private const string Section = KeycloakAccountDirectory.SettingsSection;

    private readonly KeycloakFixture _keycloak;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public KeycloakAccountDirectoryTests(KeycloakFixture keycloak)
    {
        _keycloak = keycloak;
    }

    [Fact]
    public async Task AccountExists_UserFromRealmFile_ReturnsTrue()
    {
        using var services = CreateServices();

        var exists = await Directory(services)
            .AccountExistsAsync("customer@eshop.local", _cancellationToken);

        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task AccountExists_UnknownEmail_ReturnsFalse()
    {
        using var services = CreateServices();

        var exists = await Directory(services)
            .AccountExistsAsync("nobody@eshop.local", _cancellationToken);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task AccountExists_PartOfAnEmail_ReturnsFalse()
    {
        using var services = CreateServices();

        // Without exact=true Keycloak searches for a substring and would find customer@eshop.local.
        var exists = await Directory(services)
            .AccountExistsAsync("customer@eshop", _cancellationToken);

        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task AccountExists_WrongSecret_ThrowsIdentityProviderUnavailable()
    {
        using var services = CreateServices(clientSecret: "wrong-secret");

        await Should.ThrowAsync<IdentityProviderUnavailableException>(() =>
            Directory(services).AccountExistsAsync("customer@eshop.local", _cancellationToken)
        );
    }

    [Fact]
    public async Task GetToken_SecondCall_ReusesTheToken()
    {
        var tokenRequests = new CountingHandler();
        using var services = CreateServices(tokenRequests: tokenRequests);

        await Directory(services).AccountExistsAsync("customer@eshop.local", _cancellationToken);
        await Directory(services).AccountExistsAsync("nobody@eshop.local", _cancellationToken);

        tokenRequests.Count.ShouldBe(1);
    }

    private ServiceProvider CreateServices(
        string clientSecret = KeycloakFixture.OrderApiClientSecret,
        CountingHandler? tokenRequests = null
    )
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{Section}:BaseUrl"] = _keycloak.BaseUrl,
                    [$"{Section}:Realm"] = KeycloakFixture.Realm,
                    [$"{Section}:ClientId"] = KeycloakFixture.OrderApiClientId,
                    [$"{Section}:ClientSecret"] = clientSecret,
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.AddKeycloakAdminClient<IAccountDirectory, KeycloakAccountDirectory>(
            configuration,
            Section
        );
        if (tokenRequests != null)
        {
            services
                .AddHttpClient(KeycloakAdminTokenCache.TokenClientName)
                .AddHttpMessageHandler(() => tokenRequests);
        }
        return services.BuildServiceProvider();
    }

    private static IAccountDirectory Directory(IServiceProvider services) =>
        services.GetRequiredService<IAccountDirectory>();

    // Counts the requests of the token client; the token cache must send one for several Admin API calls.
    private sealed class CountingHandler : DelegatingHandler
    {
        public int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Count++;
            return base.SendAsync(request, cancellationToken);
        }
    }
}
