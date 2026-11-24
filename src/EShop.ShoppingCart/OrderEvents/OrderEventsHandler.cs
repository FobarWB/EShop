using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;
using EShop.ShoppingCart.Data;
using Microsoft.EntityFrameworkCore;

namespace EShop.ShoppingCart.OrderEvents;

// Removes a confirmed order from its buyer's cart (#76): only the ordered quantities, so what was added after the
// checkout stays. Not on order.placed: a rejected order keeps the cart, the buyer fixes it and orders again.
// Relative statements: a quantity changed through the API at the same time is not overwritten with a stale value.
// They run inside the consumer's transaction, together with the inbox row, which stops a redelivery.
public sealed class OrderEventsHandler : IMessageHandler
{
    private readonly CartDbContext _db;
    private readonly ILogger<OrderEventsHandler> _logger;

    public OrderEventsHandler(CartDbContext db, ILogger<OrderEventsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<HandleResult> HandleAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken
    )
    {
        if (message.Type != OrderEventTypes.OrderConfirmed)
        {
            return HandleResult.Reject($"its type '{message.Type}' is unknown");
        }
        var confirmed = message.Deserialize<OrderConfirmed>();

        // A guest ordered from the browser's cart, which the client has emptied.
        if (confirmed.UserId == null)
        {
            return HandleResult.Processed;
        }
        var cartId = await _db
            .Carts.Where(c => c.UserId == confirmed.UserId)
            .Select(c => (int?)c.CartId)
            .FirstOrDefaultAsync(cancellationToken);
        if (cartId == null)
        {
            // Emptied and removed meanwhile: nothing left to take out.
            return HandleResult.Processed;
        }

        foreach (var item in confirmed.Items)
        {
            var line = _db.CartItems.Where(i =>
                i.CartId == cartId && i.ProductId == item.ProductId
            );
            // Not more than was ordered: the line goes. More (added after the checkout): the rest stays.
            await line.Where(i => i.Quantity <= item.Quantity)
                .ExecuteDeleteAsync(cancellationToken);
            await line.ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Quantity, i => i.Quantity - item.Quantity),
                cancellationToken
            );
        }
        _logger.LogInformation(
            "Order {OrderId} confirmed: its {Count} lines taken out of the cart of {UserId}",
            confirmed.OrderId,
            confirmed.Items.Count,
            confirmed.UserId
        );
        return HandleResult.Processed;
    }
}
