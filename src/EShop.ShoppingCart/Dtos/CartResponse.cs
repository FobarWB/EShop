namespace EShop.ShoppingCart.Dtos;

// The user is the one from the token: the response does not repeat the user id.
public sealed record CartResponse(int CartId, IReadOnlyList<CartItemResponse> CartItems);

// Product details come from the shared database at read time: the price and the stock are the current ones,
// so the client can mark items that cannot be ordered as they are.
public sealed record CartItemResponse(
    int ProductId,
    string ProductName,
    string? CategoryName,
    decimal? UnitPrice,
    int Quantity,
    short UnitsInStock,
    bool Discontinued
);
