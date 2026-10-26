using EShop.Data;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EShop.ProductApi.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly EShopDbContext _db;

    public ProductRepository(EShopDbContext db)
    {
        _db = db;
    }

    public async Task<Product?> CreateAsync(Product p, CancellationToken cancellationToken)
    {
        _db.Products.Add(p);
        var affected = await _db.SaveChangesAsync(cancellationToken);
        return affected > 0 ? p : null;
    }

    // categoryId null: all products.
    public async Task<IEnumerable<Product>> RetrieveAllAsync(
        int? categoryId,
        CancellationToken cancellationToken
    )
    {
        var query = _db.Products.AsNoTracking();
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }
        return await query.ToListAsync(cancellationToken);
    }

    // Not tracked: the controller loads a product and then updates it with another instance
    // of the same key, which would conflict with a tracked entity.
    public Task<Product?> RetrieveAsync(int id, CancellationToken cancellationToken) =>
        _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id, cancellationToken);

    public async Task<Product?> UpdateAsync(int id, Product p, CancellationToken cancellationToken)
    {
        _db.Products.Update(p);
        var affected = await _db.SaveChangesAsync(cancellationToken);
        return affected > 0 ? p : null;
    }

    public async Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var p = await _db.Products.FindAsync([id], cancellationToken);
        if (p is null)
        {
            return DeleteResult.NotFound;
        }

        // cart_items and order_details reference products without cascade: PostgreSQL would reject
        // the DELETE (23503), so check first and let the controller return 409.
        var inUse =
            await _db.CartItems.AnyAsync(i => i.ProductId == id, cancellationToken)
            || await _db.OrderDetails.AnyAsync(d => d.ProductId == id, cancellationToken);
        if (inUse)
        {
            return DeleteResult.InUse;
        }

        _db.Products.Remove(p);
        await _db.SaveChangesAsync(cancellationToken);
        return DeleteResult.Deleted;
    }
}
