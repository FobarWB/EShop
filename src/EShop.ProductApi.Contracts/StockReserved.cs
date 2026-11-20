namespace EShop.ProductApi.Contracts;

// Answer to order.placed: the stock of every line is taken in one transaction.
public sealed record StockReserved(int OrderId, DateTime ReservedAt);
