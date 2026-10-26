using EShop.Data;
using EShop.Data.Entities;
using EShop.ShoppingCart.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EShop.ShoppingCart.Repositories;

public class ShoppingCartRepository : IShoppingCartRepository
{
    private readonly EShopDbContext _db;

    public ShoppingCartRepository(EShopDbContext db)
    {
        _db = db;
    }

    public Task<CartResponse?> RetrieveCartAsync(
        string userId,
        CancellationToken cancellationToken
    ) =>
        _db
            .Carts.Where(c => c.UserId == userId)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<AddItemResult> AddItemToCartAsync(
        string userId,
        int productId,
        int quantity,
        CancellationToken cancellationToken
    )
    {
        var product = await _db.Products.FindAsync([productId], cancellationToken);
        if (product == null)
        {
            return AddItemResult.ProductNotFound;
        }

        var cart = await LoadCartAsync(userId, cancellationToken);
        if (cart == null)
        {
            // First item for this user: the cart is inserted in the same SaveChanges as the item.
            cart = new Cart { UserId = userId };
            _db.Carts.Add(cart);
        }

        var existingItem = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
        var totalQuantity = (existingItem?.Quantity ?? 0) + quantity;
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
            cart.CartItems.Add(new CartItem { ProductId = productId, Quantity = quantity });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return AddItemResult.Added;
    }

    public async Task<bool> RemoveItemFromCartAsync(
        string userId,
        int productId,
        CancellationToken cancellationToken
    )
    {
        var cart = await LoadCartAsync(userId, cancellationToken);
        if (cart == null)
            return false;

        var item = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            return false;

        cart.CartItems.Remove(item);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> ClearCartAsync(string userId, CancellationToken cancellationToken)
    {
        var cart = await LoadCartAsync(userId, cancellationToken);
        if (cart == null)
            return false;

        // The cart is tracked: SaveChanges deletes the removed items without db.Update.
        // No affected rows (an empty cart) is still a success; a failed write throws.
        cart.CartItems.Clear();
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateItemQuantityAsync(
        string userId,
        int productId,
        int newQuantity,
        CancellationToken cancellationToken
    )
    {
        var cart = await LoadCartAsync(userId, cancellationToken);
        if (cart == null)
            return false;

        var item = cart.CartItems.FirstOrDefault(i => i.ProductId == productId);
        if (item == null)
            return false;

        var product = await _db.Products.FindAsync([productId], cancellationToken);
        if (product == null || product.UnitsInStock < newQuantity)
        {
            return false;
        }

        item.Quantity = newQuantity;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool?> DeleteCartAsync(string userId, CancellationToken cancellationToken)
    {
        var cart = await LoadCartAsync(userId, cancellationToken);
        if (cart == null)
            return null;

        _db.Carts.Remove(cart);
        return await _db.SaveChangesAsync(cancellationToken) > 0;
    }

    // Tracked, with its items: for the methods that change the cart.
    private Task<Cart?> LoadCartAsync(string userId, CancellationToken cancellationToken) =>
        _db
            .Carts.Include(c => c.CartItems)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
}
