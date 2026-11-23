using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;

namespace EShop.ShoppingCart.OrderEvents;

// The queue of ShoppingCart for OrderApi's order events: a confirmed order leaves the buyer's cart (#76).
public static class OrderEventsQueue
{
    public const string Name = "carts.orders";

    // The queue name as this consumer's key in inbox_messages ("carts" is the key of carts.products).
    public static readonly ConsumerQueue Definition = new(
        Name,
        Name,
        [OrderEventTypes.OrderConfirmed],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
