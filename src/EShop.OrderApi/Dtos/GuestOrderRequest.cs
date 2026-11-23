using System.ComponentModel.DataAnnotations;

namespace EShop.OrderApi.Dtos;

// Body of POST api/orders/guest: the visitor's email, the address and the items of the browser's cart.
// Names and prices come from the catalog's replica at checkout, as for a signed-in user.
public sealed record GuestOrderRequest
{
    [Required, EmailAddress, StringLength(254)]
    public required string Email { get; init; }

    [Required]
    public required ShippingAddressRequest Address { get; init; }

    [Required, MinLength(1), MaxLength(100)]
    public required IReadOnlyList<OrderItemRequest> Items { get; init; }
}
