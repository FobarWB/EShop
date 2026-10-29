namespace EShop.OrderApi.Contracts;

// Published when an order is placed: everything the confirmation email needs, so consumers never call OrderApi back.
// Email null: the buyer has none, no notification.
public sealed record OrderPlaced(
    int OrderId,
    string? Email,
    string ShipName,
    decimal Total,
    DateTime PlacedAt,
    IReadOnlyList<OrderPlacedItem> Items
);

public sealed record OrderPlacedItem(string ProductName, int Quantity, decimal UnitPrice);
