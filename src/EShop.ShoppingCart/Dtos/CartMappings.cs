using EShop.Data.Entities;

namespace EShop.ShoppingCart.Dtos;

public static class CartMappings
{
    // Extends the query, not the entity: EF Core translates the projection, items, products and
    // categories included, to one SQL query.
    public static IQueryable<CartResponse> ToResponse(this IQueryable<Cart> query) =>
        query.Select(c => new CartResponse(
            c.CartId,
            c.CartItems.Select(i => new CartItemResponse(
                    i.ProductId,
                    // Inside a query the navigations become JOINs: nothing is dereferenced in C#,
                    // and a product without a category gives a null name (LEFT JOIN).
                    i.Product!.ProductName,
                    i.Product.Category!.CategoryName,
                    i.Product.UnitPrice,
                    i.Quantity
                ))
                .ToList()
        ));
}
