using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;

namespace EShop.NotificationService.OrderEvents;

// The queue of this service: the order events it needs and how it retries a failed email.
public static class OrderEventsQueue
{
    public const string Name = "notifications.order-events";

    public static readonly IReadOnlyList<string> RoutingKeys =
    [
        // Not order.placed since #76: the order is only accepted once the stock is reserved.
        OrderEventTypes.OrderConfirmed,
        OrderEventTypes.OrderRejected,
        OrderEventTypes.OrderCancelled,
    ];

    // Bound until #76: unbound on start, or every order.placed would still arrive and go to the DLQ.
    public static readonly IReadOnlyList<string> RetiredRoutingKeys = [OrderEventTypes.OrderPlaced];

    // A short outage (Mailpit restarting) is covered by the first retries, a longer one by the last.
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
    ];

    // "notifications": this consumer's key in inbox_messages.
    public static readonly ConsumerQueue Definition = new(
        Name,
        "notifications",
        RoutingKeys,
        RetryDelays,
        RetiredRoutingKeys
    );
}
