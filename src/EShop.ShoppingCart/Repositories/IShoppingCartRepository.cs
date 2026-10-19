using EShop.Data.Entities;

namespace EShop.ShoppingCart.Repositories
{
    public interface IShoppingCartRepository
    {
        Task<Cart?> CreateCartAsync(Cart cart);
        Task<Cart?> RetrieveCartByUserIdAsync(string userId);
        Task<AddItemResult> AddItemToCartAsync(
            string userId,
            CartItem item,
            CancellationToken cancellationToken
        );
        Task<bool> RemoveItemFromCartAsync(string userId, int productId);
        Task<bool> ClearCartAsync(string userId);
        Task<bool> UpdateItemQuantityAsync(string userId, int productId, int newQuantity);
        Task<bool?> DeleteCartAsync(string userId);
    }
}
