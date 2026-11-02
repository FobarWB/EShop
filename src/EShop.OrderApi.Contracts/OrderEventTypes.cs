namespace EShop.OrderApi.Contracts;

// Event types: the Type of the outbox row and the RabbitMQ routing key. Consumers bind to them ("order.*").
public static class OrderEventTypes
{
    public const string OrderPlaced = "order.placed";
    public const string OrderCancelled = "order.cancelled";
}
