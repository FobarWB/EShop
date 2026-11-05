using System.ComponentModel.DataAnnotations;

namespace EShop.ShoppingCart.Dtos;

// Body of POST api/carts/me/merge: the anonymous cart from the browser.
public sealed record MergeCartRequest
{
    [MaxLength(100)]
    public required IReadOnlyList<AddCartItemRequest> Items { get; init; }
}
