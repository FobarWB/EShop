using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;

namespace EShop.ProductApi.Reservations;

// The queue of ProductApi for OrderApi's order events: placing an order reserves its stock, cancelling returns it.
public static class OrderEventsQueue
{
    public const string Name = "products.orders";

    // The queue name as this consumer's key in inbox_messages, as OrderApi's queues do.
    public static readonly ConsumerQueue Definition = new(
        Name,
        Name,
        [OrderEventTypes.OrderPlaced, OrderEventTypes.OrderCancelled],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
