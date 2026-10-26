using EShop.Data.Entities;

namespace EShop.ShoppingCart.Dtos;

public static class CartMappings
{
    // Extends the query, not the entity: EF Core translates the projection, items included, to SQL.
    public static IQueryable<CartResponse> ToResponse(this IQueryable<Cart> query) =>
        query.Select(c => new CartResponse(
            c.CartId,
            c.CartItems.Select(i => new CartItemResponse(i.ProductId, i.Quantity)).ToList()
        ));
}
