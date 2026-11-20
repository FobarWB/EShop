namespace EShop.OrderApi.Contracts;

// Published when ProductApi could not reserve the stock: the order is closed, nothing was taken from the stock.
// Items are the lines that were short, for the email. Email null: the buyer has none, no notification.
public sealed record OrderRejected(
    int OrderId,
    string? Email,
    string ShipName,
    DateTime RejectedAt,
    IReadOnlyList<OrderRejectedItem> Items
);

// Available: the stock ProductApi saw, 0 for a product it no longer sells.
public sealed record OrderRejectedItem(string ProductName, int Quantity, int Available);
