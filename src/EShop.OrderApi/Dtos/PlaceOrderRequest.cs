using System.ComponentModel.DataAnnotations;

namespace EShop.OrderApi.Dtos;

// Body of POST api/orders: the address and the lines of the signed-in user's cart, sent by the client since #76
// (OrderApi no longer reads carts). Product ids and quantities only: names and prices come from the catalog's
// replica at checkout, so the client cannot send its own prices.
public sealed record PlaceOrderRequest
{
    [Required]
    public required ShippingAddressRequest Address { get; init; }

    [Required, MinLength(1), MaxLength(100)]
    public required IReadOnlyList<OrderItemRequest> Items { get; init; }
}
