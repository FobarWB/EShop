using System.ComponentModel.DataAnnotations;

namespace EShop.OrderApi.Dtos;

// A line of a checkout: only what the buyer chooses. Name and price come from the catalog's replica.
public sealed record OrderItemRequest
{
    [Range(1, int.MaxValue)]
    public required int ProductId { get; init; }

    [Range(1, short.MaxValue)]
    public required int Quantity { get; init; }
}
