namespace EShop.CustomerApi.Contracts;

// Published when the account of a guest order's email exists (created now or found): OrderApi attaches the email's
// guest orders to it (#45). The email is in lower case, as Keycloak stores it.
public sealed record GuestAccountReady(string Email, string UserId);
