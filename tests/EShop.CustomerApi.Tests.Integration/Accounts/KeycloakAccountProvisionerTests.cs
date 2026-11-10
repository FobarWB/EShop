using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using EShop.CustomerApi.Accounts;
using EShop.ServiceDefaults.Keycloak;
using EShop.Tests.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(KeycloakFixture))]

namespace EShop.CustomerApi.Tests.Integration.Accounts;

// The real Admin API of a Keycloak that imported our realm file, through the same registration as Program.cs.
// Every test uses an email of its own: the container lives for the whole test run.
public class KeycloakAccountProvisionerTests
{
    private const string Section = KeycloakAccountProvisioner.SettingsSection;

    private readonly KeycloakFixture _keycloak;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public KeycloakAccountProvisionerTests(KeycloakFixture keycloak)
    {
        _keycloak = keycloak;
    }

    [Fact]
    public async Task FindOrCreate_NewEmail_CreatesUserWithoutPassword()
    {
        using var services = CreateServices();

        var account = await Provisioner(services).FindOrCreateAsync(NewEmail(), _cancellationToken);

        account.UserId.ShouldNotBeNullOrEmpty();
        account.NeedsPassword.ShouldBeTrue();
    }

    [Fact]
    public async Task FindOrCreate_SameEmailAgain_ReturnsTheSameUser()
    {
        using var services = CreateServices();
        var email = NewEmail();
        var first = await Provisioner(services).FindOrCreateAsync(email, _cancellationToken);

        var second = await Provisioner(services).FindOrCreateAsync(email, _cancellationToken);

        second.ShouldBe(first);
    }

    [Fact]
    public async Task FindOrCreate_SameEmailConcurrently_ReturnsOneUser()
    {
        // Two deliveries of the same message: both miss the user, one creation gets 409 and finds the other's user.
        using var services = CreateServices();
        var email = NewEmail();

        var accounts = await Task.WhenAll(
            Provisioner(services).FindOrCreateAsync(email, _cancellationToken),
            Provisioner(services).FindOrCreateAsync(email, _cancellationToken)
        );

        accounts[1].UserId.ShouldBe(accounts[0].UserId);
    }

    [Fact]
    public async Task FindOrCreate_UserWithPassword_DoesNotNeedPassword()
    {
        using var services = CreateServices();

        var account = await Provisioner(services)
            .FindOrCreateAsync("customer@eshop.local", _cancellationToken);

        // The fixed id of the realm file's development user.
        account.ShouldBe(new ProvisionedAccount("00000000-0000-0000-0000-00000000c001", false));
    }

    [Fact]
    public async Task OrderApiClient_CannotCreateUsers()
    {
        // Least privilege: OrderApi's service account may read users (view-users), not create them.
        using var http = new HttpClient { BaseAddress = new Uri(_keycloak.BaseUrl) };
        var token = await ClientTokenAsync(
            http,
            KeycloakFixture.OrderApiClientId,
            KeycloakFixture.OrderApiClientSecret
        );
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"admin/realms/{KeycloakFixture.Realm}/users"
        )
        {
            Content = JsonContent.Create(new { username = NewEmail(), email = NewEmail() }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await http.SendAsync(request, _cancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static string NewEmail() => $"guest-{Guid.NewGuid():N}@example.com";

    private ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"{Section}:BaseUrl"] = _keycloak.BaseUrl,
                    [$"{Section}:Realm"] = KeycloakFixture.Realm,
                    [$"{Section}:ClientId"] = KeycloakFixture.CustomerApiClientId,
                    [$"{Section}:ClientSecret"] = KeycloakFixture.CustomerApiClientSecret,
                    ["GuestAccounts:ClientId"] = "eshop-client",
                    ["GuestAccounts:RedirectUri"] = "http://localhost:8080/",
                }
            )
            .Build();
        var services = new ServiceCollection();
        services.Configure<GuestAccountsOptions>(
            configuration.GetSection(GuestAccountsOptions.SectionName)
        );
        services.AddKeycloakAdminClient<IAccountProvisioner, KeycloakAccountProvisioner>(
            configuration,
            Section
        );
        return services.BuildServiceProvider();
    }

    private static IAccountProvisioner Provisioner(IServiceProvider services) =>
        services.GetRequiredService<IAccountProvisioner>();

    private async Task<string> ClientTokenAsync(HttpClient http, string clientId, string secret)
    {
        using var response = await http.PostAsync(
            $"realms/{KeycloakFixture.Realm}/protocol/openid-connect/token",
            new FormUrlEncodedContent(
                [
                    new("grant_type", "client_credentials"),
                    new("client_id", clientId),
                    new("client_secret", secret),
                ]
            ),
            _cancellationToken
        );
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(_cancellationToken)
        );
        return json.RootElement.GetProperty("access_token").GetString().ShouldNotBeNull();
    }
}
