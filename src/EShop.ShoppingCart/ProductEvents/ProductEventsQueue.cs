using EShop.Messaging.Consumers;
using EShop.ProductApi.Contracts;

namespace EShop.ShoppingCart.ProductEvents;

// The queue of ShoppingCart: ProductApi's product events, which keep cart_products up to date.
public static class ProductEventsQueue
{
    public const string Name = "carts.products";

    // "carts": this consumer's key in inbox_messages.
    public static readonly ConsumerQueue Definition = new(
        Name,
        "carts",
        [ProductEventTypes.ProductChanged, ProductEventTypes.ProductDeleted],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
