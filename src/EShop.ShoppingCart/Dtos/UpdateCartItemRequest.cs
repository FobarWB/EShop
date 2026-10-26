using System.ComponentModel.DataAnnotations;

namespace EShop.ShoppingCart.Dtos;

// Body of PUT api/carts/me/items/{productId}: an object, so fields can be added later.
public sealed record UpdateCartItemRequest
{
    [Range(1, int.MaxValue)]
    public required int Quantity { get; init; }
}
