namespace EShop.ShoppingCart.Dtos;

// The user is the one from the token: the response does not repeat the user id.
public sealed record CartResponse(int CartId, IReadOnlyList<CartItemResponse> CartItems);

public sealed record CartItemResponse(int ProductId, int Quantity);
