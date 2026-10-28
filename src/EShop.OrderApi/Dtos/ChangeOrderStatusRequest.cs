using System.ComponentModel.DataAnnotations;
using EShop.Data.Entities;

namespace EShop.OrderApi.Dtos;

// Body of PUT api/orders/{id}/status (admin). An unknown status name fails JSON binding: 400.
public sealed record ChangeOrderStatusRequest
{
    // Nullable with [Required]: a missing status is a 400, not a silent Pending (the enum's default).
    [Required]
    public OrderStatus? Status { get; init; }
}
