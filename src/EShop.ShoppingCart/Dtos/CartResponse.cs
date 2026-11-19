namespace EShop.ShoppingCart.Dtos;

// The user is the one from the token: the response does not repeat the user id.
public sealed record CartResponse(int CartId, IReadOnlyList<CartItemResponse> CartItems);

// Product details come from the cart's copy of the catalog (cart_products, #75): the price and the stock are the
// latest the cart has heard of, so the client can mark items that cannot be ordered as they are. The category
// is an id; the client has the names.
public sealed record CartItemResponse(
    int ProductId,
    string ProductName,
    int? CategoryId,
    decimal? UnitPrice,
    int Quantity,
    short UnitsInStock,
    bool Discontinued
);
