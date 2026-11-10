namespace EShop.CustomerApi.Contracts;

// Event types: the Type of the outbox row and the RabbitMQ routing key. Consumers bind to them.
public static class CustomerEventTypes
{
    public const string GuestAccountReady = "customer.guest-account-ready";
}
