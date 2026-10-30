using EShop.OrderApi.Contracts;

namespace EShop.NotificationService.Messaging;

// The queue of this service: the order events it needs and how it retries a failed email.
public static class OrderEventsQueue
{
    public const string Name = "notifications.order-events";

    public static readonly IReadOnlyList<string> RoutingKeys =
    [
        OrderEventTypes.OrderPlaced,
        OrderEventTypes.OrderCancelled,
    ];

    // A short outage (Mailpit restarting) is covered by the first retries, a longer one by the last.
    public static readonly IReadOnlyList<TimeSpan> RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(2),
    ];
}
