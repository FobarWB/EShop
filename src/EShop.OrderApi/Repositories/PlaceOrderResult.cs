using EShop.OrderApi.Dtos;

namespace EShop.OrderApi.Repositories;

public enum PlaceOrderStatus
{
    Placed,

    // The idempotency key was used before: Order is the order that attempt created.
    AlreadyPlaced,

    // ProductIds: products the catalog's replica does not have (unknown or deleted), discontinued or without a price.
    ProductsUnavailable,

    // ProductIds: the quantity is above the replica's units in stock. A hint: ProductApi's reservation decides.
    InsufficientStock,

    // A guest's key that another guest order (another email) already has: a client error, nothing is stored.
    IdempotencyKeyReused,
}

public sealed record PlaceOrderResult(
    PlaceOrderStatus Status,
    OrderResponse? Order = null,
    IReadOnlyList<int>? ProductIds = null
);
