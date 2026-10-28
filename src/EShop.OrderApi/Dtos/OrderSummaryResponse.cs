using EShop.Data.Entities;

namespace EShop.OrderApi.Dtos;

// A line of the user's order list; the details come from GET api/orders/{id}.
public sealed record OrderSummaryResponse(
    int OrderId,
    DateTime OrderDate,
    OrderStatus Status,
    decimal Total,
    int ItemCount
);
