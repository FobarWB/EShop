namespace EShop.OrderApi;

// The rate limit of the anonymous guest checkout, per client address (see Program.cs).
public static class GuestCheckoutRateLimit
{
    public const string PolicyName = "guest-checkout";

    // Requests per minute and address; 5 when the setting is missing.
    public const string PermitsSetting = "GuestCheckout:PermitsPerMinute";
    public const int DefaultPermits = 5;
}
