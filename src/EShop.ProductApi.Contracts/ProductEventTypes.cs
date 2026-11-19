namespace EShop.ProductApi.Contracts;

// Event types: the Type of the outbox row and the RabbitMQ routing key. Consumers bind to them exactly.
public static class ProductEventTypes
{
    public const string ProductChanged = "product.changed";
    public const string ProductDeleted = "product.deleted";
}
