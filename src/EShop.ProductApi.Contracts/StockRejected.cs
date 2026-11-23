namespace EShop.ProductApi.Contracts;

// Answer to order.placed: at least one line could not be reserved, and nothing was taken.
// Shortages lists every such line, not only the first one, so the buyer can fix the whole cart at once.
public sealed record StockRejected(
    int OrderId,
    IReadOnlyList<StockShortage> Shortages,
    DateTime RejectedAt
);

// Available: 0 for a product that is deleted, discontinued or unknown.
public sealed record StockShortage(int ProductId, int Requested, int Available);
