using EShop.Data;
using EShop.Data.Entities;
using EShop.ProductApi.Dtos;
using EShop.ServiceDefaults;
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

        // Two SaveChanges in one transaction: the event needs the id the INSERT generates.
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
        ProductEvents.AddChanged(_db, product, DateTime.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Read back through the same projection: no second copy of the mapping.
        return await _db
            .Products.Where(p => p.ProductId == product.ProductId)
            .ToResponse()
            .SingleAsync(cancellationToken);
    }

    // categoryId null: all products. A projection to a DTO is never tracked.
    public Task<PagedResponse<ProductResponse>> RetrieveAllAsync(
        int? categoryId,
        PageRequest paging,
        CancellationToken cancellationToken
    )
    {
        IQueryable<Product> query = _db.Products;
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }
        // Ordered before the projection: EF Core cannot sort by a property of a constructed record.
        return query.OrderBy(p => p.ProductId).ToResponse().ToPageAsync(paging, cancellationToken);
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
        // A PUT that changes nothing writes nothing and publishes nothing.
        if (_db.ChangeTracker.HasChanges())
        {
            // Revision + 1 read in C# is safe: the UPDATE checks xmin, so of two concurrent PUTs the second
            // fails with 409 instead of sending the same revision twice.
            ProductEvents.AddChanged(_db, product, DateTime.UtcNow);
        }
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

        // No check of carts and orders (#75): orders keep a copy of name and price, carts drop the product on
        // ProductDeleted.
        ProductEvents.AddDeleted(_db, p, DateTime.UtcNow);
        _db.Products.Remove(p);
        await _db.SaveChangesAsync(cancellationToken);
        return DeleteResult.Deleted;
    }

    // The initial data of a new replica: every product's current state, in one transaction.
    // Not tracked: AddSnapshot only reads the entities, SaveChanges writes only the outbox rows.
    public async Task<int> RepublishAllAsync(CancellationToken cancellationToken)
    {
        var products = await _db
            .Products.AsNoTracking()
            .OrderBy(p => p.ProductId)
            .ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var product in products)
        {
            ProductEvents.AddSnapshot(_db, product, now);
        }
        await _db.SaveChangesAsync(cancellationToken);
        return products.Count;
    }
}
