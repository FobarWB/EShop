using EShop.Messaging.Consumers;
using EShop.ProductApi.Contracts;

namespace EShop.OrderApi.Stock;

// The queue of OrderApi for ProductApi's answers to order.placed (#76).
public static class StockEventsQueue
{
    public const string Name = "orders.stock";

    // The queue name as this consumer's key in inbox_messages, as orders.products.
    public static readonly ConsumerQueue Definition = new(
        Name,
        Name,
        [ProductEventTypes.StockReserved, ProductEventTypes.StockRejected],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
