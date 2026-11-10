using EShop.CustomerApi.Accounts;
using EShop.CustomerApi.Contracts;
using EShop.Data;
using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;

namespace EShop.CustomerApi.GuestOrders;

// Creates the account of a guest order's email and reports it to OrderApi. Keycloak cannot join the database
// transaction: a redelivery finds the account and sends the email again while it has no password.
// A duplicate email beats a guest without an account.
public sealed class GuestOrderPlacedHandler : IMessageHandler
{
    private readonly IAccountProvisioner _accounts;
    private readonly EShopDbContext _db;
    private readonly ILogger<GuestOrderPlacedHandler> _logger;

    public GuestOrderPlacedHandler(
        IAccountProvisioner accounts,
        EShopDbContext db,
        ILogger<GuestOrderPlacedHandler> logger
    )
    {
        _accounts = accounts;
        _db = db;
        _logger = logger;
    }

    public async Task<HandleResult> HandleAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken
    )
    {
        if (message.Type != OrderEventTypes.GuestOrderPlaced)
        {
            return HandleResult.Reject($"its type '{message.Type}' is unknown");
        }
        var placed = message.Deserialize<GuestOrderPlaced>();

        var account = await _accounts.FindOrCreateAsync(placed.Email, cancellationToken);
        // Also an account created by an earlier, failed try: until the password is set, the link is what it needs.
        if (account.NeedsPassword)
        {
            await _accounts.SendSetPasswordEmailAsync(account.UserId, cancellationToken);
        }

        // Saved by the consumer together with the inbox row: reported once per message.
        _db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                CustomerApiOutbox.Producer,
                CustomerEventTypes.GuestAccountReady,
                new GuestAccountReady(placed.Email, account.UserId),
                DateTime.UtcNow
            )
        );

        // The email is personal data: only the ids are logged.
        _logger.LogInformation(
            "Account {UserId} ready for guest order {OrderId}; set-password email {Email}",
            account.UserId,
            placed.OrderId,
            account.NeedsPassword ? "sent" : "not needed"
        );
        return HandleResult.Processed;
    }
}
