using System.Text.Json;
using EShop.CustomerApi.Accounts;
using EShop.CustomerApi.Contracts;
using EShop.CustomerApi.GuestOrders;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.CustomerApi.Tests.Integration.GuestOrders;

// The handler alone, with a fake Keycloak; the test saves the DbContext as the consumer would.
// The consumer's mechanics (inbox, transaction, retries) are tested in EShop.Messaging.Tests.Integration.
public class GuestOrderPlacedHandlerTests : IAsyncLifetime
{
    private const string Email = "jane@example.com";

    private readonly CustomerApiDatabase _database;
    private readonly FakeProvisioner _accounts = new();
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public GuestOrderPlacedHandlerTests(CustomerApiDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task NewAccount_SendsTheSetPasswordEmailAndReportsTheAccount()
    {
        _accounts.Account = new ProvisionedAccount("user-7", NeedsPassword: true);

        var result = await HandleAsync(OrderEventTypes.GuestOrderPlaced, Placed());

        result.ShouldBe(HandleResult.Processed);
        _accounts.EmailsSentTo.ShouldBe(["user-7"]);
        var message = (await LoadOutboxAsync()).ShouldHaveSingleItem();
        message.Producer.ShouldBe(CustomerApiOutbox.Producer);
        message.Type.ShouldBe(CustomerEventTypes.GuestAccountReady);
        JsonSerializer
            .Deserialize<GuestAccountReady>(message.Payload, JsonSerializerOptions.Web)
            .ShouldBe(new GuestAccountReady(Email, "user-7"));
    }

    [Fact]
    public async Task AccountWithPassword_ReportsTheAccountWithoutAnEmail()
    {
        _accounts.Account = new ProvisionedAccount("user-7", NeedsPassword: false);

        await HandleAsync(OrderEventTypes.GuestOrderPlaced, Placed());

        _accounts.EmailsSentTo.ShouldBeEmpty();
        (await LoadOutboxAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task UnknownType_RejectsWithoutCallingKeycloak()
    {
        var result = await HandleAsync(OrderEventTypes.OrderPlaced, Placed());

        result.RejectReason.ShouldNotBeNull();
        _accounts.Calls.ShouldBe(0);
        (await LoadOutboxAsync()).ShouldBeEmpty();
    }

    private static GuestOrderPlaced Placed() => new(11085, Email, DateTime.UtcNow);

    private async Task<HandleResult> HandleAsync(string type, object body)
    {
        await using var db = _database.CreateContext();
        var handler = new GuestOrderPlacedHandler(
            _accounts,
            db,
            NullLogger<GuestOrderPlacedHandler>.Instance
        );
        var result = await handler.HandleAsync(
            new ReceivedMessage(
                Guid.CreateVersion7(),
                type,
                JsonSerializer.SerializeToUtf8Bytes(body, JsonSerializerOptions.Web)
            ),
            _cancellationToken
        );
        // What MessageConsumer does after a Processed result (with the inbox row).
        await db.SaveChangesAsync(_cancellationToken);
        return result;
    }

    private async Task<List<OutboxMessage>> LoadOutboxAsync()
    {
        await using var db = _database.CreateContext();
        return await db.OutboxMessages.AsNoTracking().ToListAsync(_cancellationToken);
    }

    private sealed class FakeProvisioner : IAccountProvisioner
    {
        public ProvisionedAccount Account { get; set; } = new("user-1", NeedsPassword: true);

        public List<string> EmailsSentTo { get; } = [];

        public int Calls { get; private set; }

        public Task<ProvisionedAccount> FindOrCreateAsync(
            string email,
            CancellationToken cancellationToken
        )
        {
            Calls++;
            return Task.FromResult(Account);
        }

        public Task SendSetPasswordEmailAsync(string userId, CancellationToken cancellationToken)
        {
            EmailsSentTo.Add(userId);
            return Task.CompletedTask;
        }
    }
}
