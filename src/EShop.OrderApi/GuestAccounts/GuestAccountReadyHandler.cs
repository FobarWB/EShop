using EShop.CustomerApi.Contracts;
using EShop.Data;
using EShop.Messaging.Consumers;
using Microsoft.EntityFrameworkCore;

namespace EShop.OrderApi.GuestAccounts;

// Attaches the guest orders of an email to the account CustomerApi created for it: they appear in the order history.
// One UPDATE for all of the email's guest orders without an account, also those placed while the account was
// being created. It runs inside the consumer's transaction, so it is stored together with the inbox row or not at all.
public sealed class GuestAccountReadyHandler : IMessageHandler
{
    private readonly EShopDbContext _db;
    private readonly ILogger<GuestAccountReadyHandler> _logger;

    public GuestAccountReadyHandler(EShopDbContext db, ILogger<GuestAccountReadyHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HandleResult> HandleAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken
    )
    {
        if (message.Type != CustomerEventTypes.GuestAccountReady)
        {
            return HandleResult.Reject($"its type '{message.Type}' is unknown");
        }
        var ready = message.Deserialize<GuestAccountReady>();

        // ix_orders_guest_customer_email: the partial index covers exactly these rows. Orders of the email that
        // belong to an account already (user_id set) are not touched.
        var attached = await _db
            .Orders.Where(o =>
                o.PlacedAsGuest && o.UserId == null && o.CustomerEmail == ready.Email
            )
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.UserId, ready.UserId), cancellationToken);

        // 0 is fine: a redelivery after a crash, the orders are attached already.
        _logger.LogInformation(
            "Attached {Count} guest orders to account {UserId}",
            attached,
            ready.UserId
        );
        return HandleResult.Processed;
    }
}
