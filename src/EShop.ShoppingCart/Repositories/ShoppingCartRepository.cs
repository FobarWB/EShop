using EShop.Data;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EShop.ShoppingCart.Repositories
{
    public class ShoppingCartRepository : IShoppingCartRepository
    {
        private readonly EShopDbContext db;

        public ShoppingCartRepository(EShopDbContext db)
        {
            this.db = db;
        }

        public async Task<Cart?> RetrieveCartByUserIdAsync(
            string userId,
            CancellationToken cancellationToken
        )
        {
            return await db
                .Carts.Include(c => c.CartItems)
                .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        }

        public async Task<AddItemResult> AddItemToCartAsync(
            string userId,
            CartItem item,
            CancellationToken cancellationToken
        )
        {
            var product = await db.Products.FindAsync([item.ProductId], cancellationToken);
            if (product == null)
            {
                return AddItemResult.ProductNotFound;
            }

            var cart = await RetrieveCartByUserIdAsync(userId, cancellationToken);
            if (cart == null)
            {
                // First item for this user: the cart is inserted in the same SaveChanges as the item.
                cart = new Cart { UserId = userId };
                db.Carts.Add(cart);
            }

            var existingItem = cart.CartItems.FirstOrDefault(i => i.ProductId == item.ProductId);
            var totalQuantity = (existingItem?.Quantity ?? 0) + item.Quantity;
            if (totalQuantity > product.UnitsInStock)
            {
                return AddItemResult.InsufficientStock;
            }

            if (existingItem != null)
            {
                existingItem.Quantity = totalQuantity;
            }
            else
            {
                cart.CartItems.Add(item);
            }

            await db.SaveChangesAsync(cancellationToken);
            return AddItemResult.Added;
        }

        public async Task<bool> RemoveItemFromCartAsync(
            string userId,
            int productId,
            CancellationToken cancellationToken
        )
        {
            var cart = await RetrieveCartByUserIdAsync(userId, cancellationToken);
            if (cart == null)
                return false;

            var item = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
                return false;

            cart.CartItems.Remove(item);
            db.Update(cart);
            return await db.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> ClearCartAsync(string userId, CancellationToken cancellationToken)
        {
            var cart = await RetrieveCartByUserIdAsync(userId, cancellationToken);
            if (cart == null)
                return false;

            cart.CartItems.Clear();
            db.Update(cart);
            return await db.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool> UpdateItemQuantityAsync(
            string userId,
            int productId,
            int newQuantity,
            CancellationToken cancellationToken
        )
        {
            var cart = await RetrieveCartByUserIdAsync(userId, cancellationToken);
            if (cart == null)
                return false;

            var item = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
            if (item == null)
                return false;

            var product = await db.Products.FindAsync([productId], cancellationToken);
            if (product == null || product.UnitsInStock < newQuantity)
            {
                return false;
            }

            item.Quantity = newQuantity;
            db.Update(cart);
            return await db.SaveChangesAsync(cancellationToken) > 0;
        }

        public async Task<bool?> DeleteCartAsync(string userId, CancellationToken cancellationToken)
        {
            var cart = await RetrieveCartByUserIdAsync(userId, cancellationToken);
            if (cart == null)
                return null;

            db.Carts.Remove(cart);
            return await db.SaveChangesAsync(cancellationToken) > 0;
        }
    }
}
