namespace EShop.ProductApi;

public static class ProductApiOutbox
{
    // The producer of ProductApi's outbox rows: its OutboxPublisher publishes only these.
    public const string Producer = "productapi";
}
