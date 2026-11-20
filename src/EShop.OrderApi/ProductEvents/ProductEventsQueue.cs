using EShop.Messaging.Consumers;
using EShop.ProductApi.Contracts;

namespace EShop.OrderApi.ProductEvents;

// The queue of OrderApi for ProductApi's product events, which keep order_products up to date.
public static class ProductEventsQueue
{
    public const string Name = "orders.products";

    // The queue name as this consumer's key in inbox_messages: OrderApi has several consumers, and one event bound
    // to two of them must be processed by each.
    public static readonly ConsumerQueue Definition = new(
        Name,
        Name,
        [ProductEventTypes.ProductChanged, ProductEventTypes.ProductDeleted],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
