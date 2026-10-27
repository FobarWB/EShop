using System.ComponentModel.DataAnnotations;

namespace EShop.ShoppingCart.Dtos;

// Body of POST api/carts/me/items: the cart is the signed-in user's, so there is no cart id.
public sealed record AddCartItemRequest
{
    [Range(1, int.MaxValue)]
    public required int ProductId { get; init; }

    [Range(1, int.MaxValue)]
    public required int Quantity { get; init; }
}
