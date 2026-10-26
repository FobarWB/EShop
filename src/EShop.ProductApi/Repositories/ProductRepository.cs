using EShop.Data;
using EShop.Data.Entities;
using EShop.ProductApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EShop.ProductApi.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly EShopDbContext _db;

    public ProductRepository(EShopDbContext db)
    {
        _db = db;
    }

    public async Task<ProductResponse> CreateAsync(
        ProductRequest request,
        CancellationToken cancellationToken
    )
    {
        var product = new Product();
        request.ApplyTo(product);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);

        // Read back through the same projection: no second copy of the mapping.
        return await _db
            .Products.Where(p => p.ProductId == product.ProductId)
            .ToResponse()
            .SingleAsync(cancellationToken);
    }

    // categoryId null: all products. A projection to a DTO is never tracked.
    public async Task<IReadOnlyList<ProductResponse>> RetrieveAllAsync(
        int? categoryId,
        CancellationToken cancellationToken
    )
    {
        IQueryable<Product> query = _db.Products;
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }
        return await query.ToResponse().ToListAsync(cancellationToken);
    }

    public Task<ProductResponse?> RetrieveAsync(int id, CancellationToken cancellationToken) =>
        _db
            .Products.Where(p => p.ProductId == id)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);

    // Loads the tracked entity and copies the request onto it: SaveChanges updates only the changed columns.
    public async Task<bool> UpdateAsync(
        int id,
        ProductRequest request,
        CancellationToken cancellationToken
    )
    {
        var product = await _db.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return false;
        }

        request.ApplyTo(product);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
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
