namespace EShop.ProductApi.Contracts;

// Event types: the Type of the outbox row and the RabbitMQ routing key. Consumers bind to them exactly.
// stock.* answer OrderApi's order.placed (#76).
public static class ProductEventTypes
{
    public const string ProductChanged = "product.changed";
    public const string ProductDeleted = "product.deleted";
    public const string StockReserved = "stock.reserved";
    public const string StockRejected = "stock.rejected";
}
