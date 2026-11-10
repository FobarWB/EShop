using Testcontainers.Keycloak;
using Xunit;

namespace EShop.Tests.Common;

/// <summary>
/// A throwaway Keycloak for one test project (Testcontainers) that imports the realm file of the repository, so the
/// tests check that file too: its clients, service accounts and roles. Started once per test run, removed afterwards.
/// </summary>
public sealed class KeycloakFixture : IAsyncLifetime
{
    public const string Realm = "eshop";
    public const string OrderApiClientId = "eshop-orderapi";
    public const string OrderApiClientSecret = "orderapi-test-secret";
    public const string CustomerApiClientId = "eshop-customerapi";
    public const string CustomerApiClientSecret = "customerapi-test-secret";

    // The same image as docker-compose.yml; the realm file reads the secrets as ${..._KEYCLOAK_SECRET}.
    private readonly KeycloakContainer _container = new KeycloakBuilder(
        "quay.io/keycloak/keycloak:26.8.2"
    )
        .WithResourceMapping(new FileInfo(RealmFile()), "/opt/keycloak/data/import/")
        .WithCommand("--import-realm")
        .WithEnvironment("ORDERAPI_KEYCLOAK_SECRET", OrderApiClientSecret)
        .WithEnvironment("CUSTOMERAPI_KEYCLOAK_SECRET", CustomerApiClientSecret)
        .Build();

    public string BaseUrl => _container.GetBaseAddress();

    public ValueTask InitializeAsync() =>
        new(_container.StartAsync(TestContext.Current.CancellationToken));

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    // keycloak/realms/eshop-realm.json, found from the test binaries up to the repository root.
    private static string RealmFile()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory != null;
            directory = directory.Parent
        )
        {
            var file = Path.Combine(directory.FullName, "keycloak", "realms", "eshop-realm.json");
            if (File.Exists(file))
            {
                return file;
            }
        }
        throw new FileNotFoundException("keycloak/realms/eshop-realm.json was not found.");
    }
}
