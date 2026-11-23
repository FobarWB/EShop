namespace EShop.OrderApi.Contracts;

// Published when ProductApi has reserved the stock of every line: the order is accepted. Carries what the
// "order accepted" email and the cart need, so consumers never call OrderApi back.
// UserId null: a guest order, there is no server cart. Email null: the buyer has none, no notification.
public sealed record OrderConfirmed(
    int OrderId,
    string? UserId,
    string? Email,
    string ShipName,
    decimal Total,
    bool PlacedAsGuest,
    DateTime ConfirmedAt,
    IReadOnlyList<OrderConfirmedItem> Items
);

public sealed record OrderConfirmedItem(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
);
