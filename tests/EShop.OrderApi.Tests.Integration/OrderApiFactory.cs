using System.Collections.Concurrent;
using EShop.OrderApi.Accounts;
using EShop.ServiceDefaults.Keycloak;
using EShop.Tests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace EShop.OrderApi.Tests.Integration;

// A class fixture: one in-memory API per test class, using this project's test database.
// Keycloak is replaced by FakeAccountDirectory; the real client is tested in KeycloakAccountDirectoryTests.
public class OrderApiFactory(OrderApiDatabase database) : EShopApiFactory<Program>(database)
{
    public FakeAccountDirectory Accounts { get; } = new();

    // High enough that the guest tests of one class never meet the limit.
    protected virtual int GuestPermitsPerMinute => 1000;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        // ValidateOnStart needs a secret; the other KeycloakAdmin settings come from appsettings.json.
        builder.UseSetting("KeycloakAdmin:ClientSecret", "test-secret");
        builder.UseSetting(GuestCheckoutRateLimit.PermitsSetting, GuestPermitsPerMinute.ToString());
        builder.ConfigureTestServices(services =>
            services.AddSingleton<IAccountDirectory>(Accounts)
        );
    }
}

// The same API with a guest limit of 2 requests per minute.
public sealed class RateLimitedOrderApiFactory(OrderApiDatabase database)
    : OrderApiFactory(database)
{
    protected override int GuestPermitsPerMinute => 2;
}

// Emails that already have an account, and a switch for an unavailable Keycloak.
public sealed class FakeAccountDirectory : IAccountDirectory
{
    private readonly ConcurrentDictionary<string, byte> _emails = new();

    public bool Unavailable { get; set; }

    public void Add(string email) => _emails[email] = 0;

    public void Reset()
    {
        _emails.Clear();
        Unavailable = false;
    }

    public Task<bool> AccountExistsAsync(string email, CancellationToken cancellationToken) =>
        Unavailable
            ? throw new IdentityProviderUnavailableException(
                "Keycloak is unavailable (test).",
                new HttpRequestException()
            )
            : Task.FromResult(_emails.ContainsKey(email));
}
