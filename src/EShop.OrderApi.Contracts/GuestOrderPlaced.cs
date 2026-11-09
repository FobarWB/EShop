namespace EShop.OrderApi.Contracts;

// Published with OrderPlaced for a guest order: CustomerApi finds or creates the account of this email,
// then OrderApi attaches the order to it (#45). The email is in lower case, as Keycloak stores it.
public sealed record GuestOrderPlaced(int OrderId, string Email, DateTime PlacedAt);
