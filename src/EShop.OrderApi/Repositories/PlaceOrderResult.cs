using EShop.OrderApi.Dtos;

namespace EShop.OrderApi.Repositories;

public enum PlaceOrderStatus
{
    Placed,

    // The idempotency key was used before: Order is the order that attempt created.
    AlreadyPlaced,
    CartEmpty,

    // ProductIds: discontinued products or products without a price.
    ProductsUnavailable,

    // ProductIds: the cart quantity is above the units in stock.
    InsufficientStock,

    // A guest's key that another guest order (another email) already has: a client error, nothing is stored.
    IdempotencyKeyReused,
}

public sealed record PlaceOrderResult(
    PlaceOrderStatus Status,
    OrderResponse? Order = null,
    IReadOnlyList<int>? ProductIds = null
);
