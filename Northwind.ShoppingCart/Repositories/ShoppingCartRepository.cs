using EShop.Shared;
using Microsoft.EntityFrameworkCore;


namespace Northwind.ShoppingCart.Repositories
{
    public class ShoppingCartRepository : IShoppingCartRepository
    {
        private readonly NorthwindContext db;

        public ShoppingCartRepository(NorthwindContext db)
        {
            this.db = db;
        }

        public async Task<Cart?> CreateCartAsync(Cart cart)
        {
            db.Carts.Add(cart);
            await db.SaveChangesAsync();
            return cart;
        }
        
        public async Task<Cart?> RetrieveCartByUserIdAsync(string userId)
        {
            if (userId == null) return null;
            return await db.Carts
            .Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId);
        }

        public async Task<bool> AddItemToCartAsync(string userId, CartItem item)
        {
            var cart = await RetrieveCartByUserIdAsync(userId);
            if (cart == null) return false;

            var product = await db.Products.FindAsync(item.ProductId);
            if (product == null || product.UnitsInStock < item.Quantity)
            {
                return false;
            }
            
            var existingItem = cart.CartItems.FirstOrDefault(i => i.ProductId == item.ProductId);
            if (existingItem != null)
            {
                if (existingItem.Quantity + item.Quantity > product.UnitsInStock)
                {
                    return false;
                }

                existingItem.Quantity += item.Quantity;
            }
            else
            {
                cart.CartItems.Add(item);
            }

            db.Update(cart);
            return await db.SaveChangesAsync() > 0; 
        }
        public async Task<bool> RemoveItemFromCartAsync(string userId, int productId)
        {
            var cart = await RetrieveCartByUserIdAsync(userId);
            if(cart == null) return false;

            var item = cart.CartItems.FirstOrDefault(
                i => i.ProductId == productId);
            if (item == null) return false;
            
            cart.CartItems.Remove(item);
            db.Update(cart);
            return await db.SaveChangesAsync() > 0;

        }

        public async Task<bool> ClearCartAsync(string userId)
        {
            var cart = await RetrieveCartByUserIdAsync(userId);
            if(cart == null) return false;

            cart.CartItems.Clear();
            db.Update(cart);
            return await db.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateItemQuantityAsync(string userId, int productId, int newQuantity)
        {
            var cart = await RetrieveCartByUserIdAsync(userId);
            if(cart == null) return false;

            var item = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
            if (item == null) return false;

            var product = await db.Products.FindAsync(productId);
            if (product == null || product.UnitsInStock < newQuantity)
            {
                return false;
            }

            item.Quantity = newQuantity;
            db.Update(cart);
            return await db.SaveChangesAsync() > 0;
        }

        public async Task<bool?> DeleteCartAsync(string userId)
        {
            var cart = await RetrieveCartByUserIdAsync(userId);
            if (cart == null) return null;

            db.Carts.Remove(cart);
            return await db.SaveChangesAsync() > 0;
        }
    }
}