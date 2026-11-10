using EShop.CustomerApi.Contracts;
using EShop.Messaging.Consumers;

namespace EShop.OrderApi.GuestAccounts;

// The queue of OrderApi: the accounts CustomerApi created for guest orders.
public static class GuestAccountsQueue
{
    public const string Name = "orders.guest-accounts";

    // "orders": this consumer's key in inbox_messages.
    public static readonly ConsumerQueue Definition = new(
        Name,
        "orders",
        [CustomerEventTypes.GuestAccountReady],
        [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(2)]
    );
}
