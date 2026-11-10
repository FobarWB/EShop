using EShop.Messaging.Consumers;
using EShop.OrderApi.Contracts;

namespace EShop.CustomerApi.GuestOrders;

// The queue of CustomerApi: guest orders, whose emails need an account.
public static class GuestOrdersQueue
{
    public const string Name = "customers.guest-orders";

    // A short Keycloak outage is covered by the first retries, a longer one by the last; then the DLQ.
    // "customers": this consumer's key in inbox_messages.
    public static readonly ConsumerQueue Definition = new(
        Name,
        "customers",
        [OrderEventTypes.GuestOrderPlaced],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
