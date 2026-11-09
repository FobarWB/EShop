using System.ComponentModel.DataAnnotations;

namespace EShop.OrderApi.Dtos;

// Body of POST api/orders/guest: the visitor's email, the address and the items of the browser's cart.
// Prices and stock come from the products at checkout, as for a signed-in user.
public sealed record GuestOrderRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required]
    public required PlaceOrderRequest Address { get; init; }

    [Required, MinLength(1), MaxLength(100)]
    public required IReadOnlyList<GuestOrderItem> Items { get; init; }
}

public sealed record GuestOrderItem
{
    [Range(1, int.MaxValue)]
    public required int ProductId { get; init; }

    [Range(1, short.MaxValue)]
    public required int Quantity { get; init; }
}
