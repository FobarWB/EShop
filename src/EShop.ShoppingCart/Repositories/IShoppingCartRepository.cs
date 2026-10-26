using EShop.ShoppingCart.Dtos;

namespace EShop.ShoppingCart.Repositories;

public interface IShoppingCartRepository
{
    // null: the user has no cart yet (it is created with the first item).
    Task<CartResponse?> RetrieveCartAsync(string userId, CancellationToken cancellationToken);
    Task<AddItemResult> AddItemToCartAsync(
        string userId,
        int productId,
        int quantity,
        CancellationToken cancellationToken
    );
    Task<bool> RemoveItemFromCartAsync(
        string userId,
        int productId,
        CancellationToken cancellationToken
    );
    Task<bool> ClearCartAsync(string userId, CancellationToken cancellationToken);
    Task<bool> UpdateItemQuantityAsync(
        string userId,
        int productId,
        int newQuantity,
        CancellationToken cancellationToken
    );
    Task<bool?> DeleteCartAsync(string userId, CancellationToken cancellationToken);
}
