using System.ComponentModel.DataAnnotations;
using EShop.OrderApi.Data.Entities;

namespace EShop.OrderApi.Dtos;

// Body of PUT api/orders/{id}/status (admin). An unknown status name fails JSON binding: 400.
public sealed record ChangeOrderStatusRequest : IValidatableObject
{
    // Nullable with [Required]: a missing status is a 400, not a silent Pending (the enum's default).
    [Required]
    public OrderStatus? Status { get; init; }

    // Confirmed and Rejected are the answer to the stock reservation (#76): set by hand, they would confirm an order
    // whose stock was never taken.
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Status is OrderStatus.Confirmed or OrderStatus.Rejected)
        {
            yield return new ValidationResult(
                $"{Status} is set by the stock reservation, not by hand.",
                [nameof(Status)]
            );
        }
    }
}
