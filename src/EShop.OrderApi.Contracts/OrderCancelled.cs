namespace EShop.OrderApi.Contracts;

// Published when an order is cancelled (by its buyer or an admin). Email null: the buyer has none, no notification.
public sealed record OrderCancelled(int OrderId, string? Email, DateTime CancelledAt);
