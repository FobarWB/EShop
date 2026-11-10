namespace EShop.OrderApi;

public static class OrderApiOutbox
{
    // The producer of OrderApi's outbox rows: its OutboxPublisher publishes only these.
    public const string Producer = "orderapi";
}
