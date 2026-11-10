namespace EShop.CustomerApi;

public static class CustomerApiOutbox
{
    // The producer of CustomerApi's outbox rows: its OutboxPublisher publishes only these.
    public const string Producer = "customerapi";
}
