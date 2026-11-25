using EShop.ShoppingCart.Data.Entities;

namespace EShop.ShoppingCart.Dtos;

public static class CartMappings
{
    // Extends the query, not the entity: EF Core translates the projection, the join with the cart's product copies
    // included, to one SQL query. An inner join on rows that are not deleted: ProductDeleted removes the item from
    // the carts anyway.
    public static IQueryable<CartResponse> ToResponse(
        this IQueryable<Cart> query,
        IQueryable<CartProduct> products
    ) =>
        query.Select(c => new CartResponse(
            c.CartId,
            c.CartItems.Join(
                    products.Where(p => !p.Deleted),
                    i => i.ProductId,
                    p => p.ProductId,
                    (i, p) =>
                        new CartItemResponse(
                            i.ProductId,
                            p.ProductName,
                            p.CategoryId,
                            p.UnitPrice,
                            i.Quantity,
                            p.UnitsInStock,
                            p.Discontinued
                        )
                )
                .ToList()
        ));
}
