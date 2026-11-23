namespace EShop.OrderApi.Contracts;

// Published when an order is placed, still Pending: ProductApi reserves its lines and answers with stock.reserved or
// stock.rejected (#76). Email null: the buyer has none, no notification.
public sealed record OrderPlaced(
    int OrderId,
    string? Email,
    string ShipName,
    decimal Total,
    DateTime PlacedAt,
    IReadOnlyList<OrderPlacedItem> Items,
    // Added in #45; old messages have no such field and read as false.
    bool PlacedAsGuest = false
);

public sealed record OrderPlacedItem(
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    // Added in #76; old messages read as 0, a line ProductApi cannot reserve.
    int ProductId = 0
);
