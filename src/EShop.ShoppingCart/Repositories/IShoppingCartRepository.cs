using EShop.Data.Entities;

namespace EShop.ShoppingCart.Repositories
{
    public interface IShoppingCartRepository
    {
        Task<Cart?> RetrieveCartByUserIdAsync(string userId, CancellationToken cancellationToken);
        Task<AddItemResult> AddItemToCartAsync(
            string userId,
            CartItem item,
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
}
