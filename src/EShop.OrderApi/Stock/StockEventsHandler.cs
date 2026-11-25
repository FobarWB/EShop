using EShop.Messaging.Consumers;
using EShop.Messaging.Outbox;
using EShop.OrderApi.Contracts;
using EShop.OrderApi.Data;
using EShop.OrderApi.Data.Entities;
using EShop.ProductApi.Contracts;
using Microsoft.EntityFrameworkCore;

namespace EShop.OrderApi.Stock;

// OrderApi's end of the checkout saga (#76): ProductApi's answer to order.placed closes the Pending order.
// stock.reserved → Confirmed and order.confirmed (the cart is emptied, the "order accepted" email goes out);
// stock.rejected → Rejected and order.rejected (the email lists the short lines).
// The lifecycle makes it idempotent: an order no longer Pending (a redelivery, or cancelled meanwhile) stays as it is.
// Runs inside the consumer's transaction; the order's xmin orders it against a cancel running at the same time:
// the later of the two fails, a cancel with 409, this handler with a retry that then finds the order closed.
public sealed class StockEventsHandler : IMessageHandler
{
    // ChangedBy in the status journal: the catalog decided, not a person.
    public const string ChangedBy = "productapi";

    private readonly OrderingDbContext _db;
    private readonly ILogger<StockEventsHandler> _logger;

    public StockEventsHandler(OrderingDbContext db, ILogger<StockEventsHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<HandleResult> HandleAsync(
        ReceivedMessage message,
        CancellationToken cancellationToken
    ) =>
        message.Type switch
        {
            ProductEventTypes.StockReserved => ConfirmAsync(
                message.Deserialize<StockReserved>(),
                cancellationToken
            ),
            ProductEventTypes.StockRejected => RejectAsync(
                message.Deserialize<StockRejected>(),
                cancellationToken
            ),
            _ => Task.FromResult(HandleResult.Reject($"its type '{message.Type}' is unknown")),
        };

    private async Task<HandleResult> ConfirmAsync(
        StockReserved reserved,
        CancellationToken cancellationToken
    )
    {
        var order = await LoadAsync(reserved.OrderId, cancellationToken);
        if (order == null)
        {
            return UnknownOrder(reserved.OrderId);
        }
        var at = DateTime.UtcNow;
        if (!order.TryChangeStatus(OrderStatus.Confirmed, ChangedBy, at))
        {
            return KeptAsItIs(order);
        }

        _db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                OrderApiOutbox.Producer,
                OrderEventTypes.OrderConfirmed,
                new OrderConfirmed(
                    order.OrderId,
                    // A guest ordered from the browser's cart: even with an account attached meanwhile (#45),
                    // its server cart has nothing to do with this order.
                    order.PlacedAsGuest
                        ? null
                        : order.UserId,
                    order.CustomerEmail,
                    ShipNameOf(order),
                    order.OrderDetails.Sum(d => d.UnitPrice * d.Quantity) + (order.Freight ?? 0m),
                    order.PlacedAsGuest,
                    at,
                    order
                        .OrderDetails.Select(d => new OrderConfirmedItem(
                            d.ProductId,
                            d.ProductName,
                            d.Quantity,
                            d.UnitPrice
                        ))
                        .ToList()
                ),
                at
            )
        );
        _logger.LogInformation("Order {OrderId} confirmed", order.OrderId);
        return HandleResult.Processed;
    }

    private async Task<HandleResult> RejectAsync(
        StockRejected rejected,
        CancellationToken cancellationToken
    )
    {
        var order = await LoadAsync(rejected.OrderId, cancellationToken);
        if (order == null)
        {
            return UnknownOrder(rejected.OrderId);
        }
        var at = DateTime.UtcNow;
        if (!order.TryChangeStatus(OrderStatus.Rejected, ChangedBy, at))
        {
            return KeptAsItIs(order);
        }

        // The names as the buyer saw them at checkout: the order's copies, not the catalog's current ones.
        var names = order.OrderDetails.ToDictionary(d => d.ProductId, d => d.ProductName);
        _db.OutboxMessages.Add(
            OutboxMessageFactory.Create(
                OrderApiOutbox.Producer,
                OrderEventTypes.OrderRejected,
                new OrderRejected(
                    order.OrderId,
                    order.CustomerEmail,
                    ShipNameOf(order),
                    at,
                    rejected
                        .Shortages.Select(s => new OrderRejectedItem(
                            names.GetValueOrDefault(s.ProductId, $"Product {s.ProductId}"),
                            s.Requested,
                            s.Available
                        ))
                        .ToList()
                ),
                at
            )
        );
        _logger.LogInformation(
            "Order {OrderId} rejected: {Count} lines short",
            order.OrderId,
            rejected.Shortages.Count
        );
        return HandleResult.Processed;
    }

    // Tracked: the status changes. With its lines, for the events.
    private Task<Order?> LoadAsync(int orderId, CancellationToken cancellationToken) =>
        _db
            .Orders.Include(o => o.OrderDetails)
            .FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);

    // An answer for an order OrderApi never stored cannot be processed, now or later: the DLQ shows it.
    private static HandleResult UnknownOrder(int orderId) =>
        HandleResult.Reject($"order {orderId} is unknown");

    private HandleResult KeptAsItIs(Order order)
    {
        _logger.LogInformation(
            "Order {OrderId} is {Status}: the stock answer changes nothing",
            order.OrderId,
            order.Status
        );
        return HandleResult.Processed;
    }

    // Checkout always sets it; only Northwind orders have none, and they never get a stock answer.
    private static string ShipNameOf(Order order) => order.ShipName ?? string.Empty;
}
