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

    public async Task<Product?> CreateAsync(Product p)
    {
        _db.Products.Add(p);
        var affected = await _db.SaveChangesAsync();
        return affected > 0 ? p : null;
    }

    public async Task<IEnumerable<Product>> RetrieveAllAsync() =>
        await _db.Products.AsNoTracking().ToListAsync();

    // Not tracked: the controller loads a product and then updates it with another instance
    // of the same key, which would conflict with a tracked entity.
    public Task<Product?> RetrieveAsync(int id) =>
        _db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == id);

    public async Task<Product?> UpdateAsync(int id, Product p)
    {
        _db.Products.Update(p);
        var affected = await _db.SaveChangesAsync();
        return affected > 0 ? p : null;
    }

    public async Task<bool?> DeleteAsync(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null)
        {
            return null;
        }

        _db.Products.Remove(p);
        return await _db.SaveChangesAsync() > 0;
    }
}
