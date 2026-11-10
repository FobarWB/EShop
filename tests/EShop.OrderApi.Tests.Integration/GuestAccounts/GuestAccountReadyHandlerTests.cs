using System.Text.Json;
using EShop.CustomerApi.Contracts;
using EShop.Data.Entities;
using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;
using EShop.OrderApi.GuestAccounts;
using EShop.OrderApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.Extensions.Logging.Abstractions;

namespace EShop.OrderApi.Tests.Integration.GuestAccounts;

// The handler alone on the test database, inside a transaction with SaveChanges, as MessageConsumer runs it.
// The consumer's mechanics (inbox, retries) are tested in EShop.Messaging.Tests.Integration.
public class GuestAccountReadyHandlerTests : IAsyncLifetime
{
    private const string Email = "jane@example.com";
    private const string NewUserId = "kc-user-7";

    private readonly OrderApiDatabase _database;
    private readonly CancellationToken _cancellationToken = TestContext.Current.CancellationToken;

    public GuestAccountReadyHandlerTests(OrderApiDatabase database)
    {
        _database = database;
    }

    public async ValueTask InitializeAsync() => await _database.ResetAsync(_cancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AccountReady_AttachesOnlyTheEmailsGuestOrdersWithoutAccount()
    {
        var first = await SeedGuestOrderAsync(Email);
        var second = await SeedGuestOrderAsync(Email);
        var otherGuest = await SeedGuestOrderAsync("someone@example.com");
        // A signed-in buyer's order with the same email: it has an owner and stays theirs.
        var signedIn = await _database.SeedAsync(
            OrderApiDatabase.NewOrder("user-1", Guid.NewGuid()),
            _cancellationToken
        );

        var result = await HandleAsync(new GuestAccountReady(Email, NewUserId));

        result.ShouldBe(HandleResult.Processed);
        (await UserIdOfAsync(first)).ShouldBe(NewUserId);
        (await UserIdOfAsync(second)).ShouldBe(NewUserId);
        (await UserIdOfAsync(otherGuest)).ShouldBeNull();
        (await UserIdOfAsync(signedIn.OrderId)).ShouldBe("user-1");
    }

    [Fact]
    public async Task AccountReady_TheOrderIsInTheHistoryAndAGuestRetryStillFindsIt()
    {
        var key = Guid.NewGuid();
        var orderId = await SeedGuestOrderAsync(Email, key);

        await HandleAsync(new GuestAccountReady(Email, NewUserId));

        await using var db = _database.CreateContext();
        var repository = new OrderRepository(db);
        (await repository.RetrieveOrdersAsync(NewUserId, new PageRequest(), _cancellationToken))
            .Items.ShouldHaveSingleItem()
            .OrderId.ShouldBe(orderId);
        // The guest index is on placed_as_guest, not on user_id IS NULL: a lost response's retry still gets its order.
        (await repository.RetrieveGuestOrderAsync(Email, key, _cancellationToken))
            .ShouldNotBeNull()
            .OrderId.ShouldBe(orderId);
    }

    [Fact]
    public async Task AccountReadyAgain_ChangesNothing()
    {
        var orderId = await SeedGuestOrderAsync(Email);
        await HandleAsync(new GuestAccountReady(Email, NewUserId));

        var result = await HandleAsync(new GuestAccountReady(Email, "kc-user-other"));

        result.ShouldBe(HandleResult.Processed);
        (await UserIdOfAsync(orderId)).ShouldBe(NewUserId);
    }

    [Fact]
    public async Task UnknownType_RejectsAndAttachesNothing()
    {
        var orderId = await SeedGuestOrderAsync(Email);

        var result = await HandleAsync(
            new GuestAccountReady(Email, NewUserId),
            OrderEventTypes.OrderPlaced
        );

        result.RejectReason.ShouldNotBeNull();
        (await UserIdOfAsync(orderId)).ShouldBeNull();
    }

    private async Task<int> SeedGuestOrderAsync(string email, Guid? key = null)
    {
        var order = OrderApiDatabase.NewOrder("ignored", key ?? Guid.NewGuid());
        order.UserId = null;
        order.PlacedAsGuest = true;
        order.CustomerEmail = email;
        return (await _database.SeedAsync(order, _cancellationToken)).OrderId;
    }

    // As MessageConsumer: one transaction around the handler and SaveChanges, committed after a Processed result.
    private async Task<HandleResult> HandleAsync(
        GuestAccountReady ready,
        string type = CustomerEventTypes.GuestAccountReady
    )
    {
        await using var db = _database.CreateContext();
        await using var transaction = await db.Database.BeginTransactionAsync(_cancellationToken);
        var result = await new GuestAccountReadyHandler(
            db,
            NullLogger<GuestAccountReadyHandler>.Instance
        ).HandleAsync(
            new ReceivedMessage(
                Guid.CreateVersion7(),
                type,
                JsonSerializer.SerializeToUtf8Bytes(ready, JsonSerializerOptions.Web)
            ),
            _cancellationToken
        );
        if (result.RejectReason == null)
        {
            await db.SaveChangesAsync(_cancellationToken);
            await transaction.CommitAsync(_cancellationToken);
        }
        return result;
    }

    private async Task<string?> UserIdOfAsync(int orderId) =>
        (await _database.FindAsync<Order>(orderId, _cancellationToken)).ShouldNotBeNull().UserId;
}
