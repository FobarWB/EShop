using System.Linq.Expressions;
using EShop.Data;
using EShop.Data.Entities;
using EShop.ProductApi.Dtos;
using Microsoft.EntityFrameworkCore;

namespace EShop.ProductApi.Repositories;

public class ProductRepository : IProductRepository
{
    // An expression, not a method: EF Core translates it to SQL and reads only these columns.
    private static readonly Expression<Func<Product, ProductResponse>> ToResponse =
        p => new ProductResponse(
            p.ProductId,
            p.ProductName,
            p.SupplierId,
            p.CategoryId,
            p.QuantityPerUnit,
            p.UnitPrice,
            p.UnitsInStock,
            p.UnitsOnOrder,
            p.ReorderLevel,
            p.Discontinued
        );

    // The same mapping for an entity already in memory (after Create).
    private static readonly Func<Product, ProductResponse> ToResponseCompiled =
        ToResponse.Compile();

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
        Apply(request, product);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponseCompiled(product);
    }

    // categoryId null: all products.
    public async Task<IReadOnlyList<ProductResponse>> RetrieveAllAsync(
        int? categoryId,
        CancellationToken cancellationToken
    )
    {
        var query = _db.Products.AsNoTracking();
        if (categoryId.HasValue)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }
        return await query.Select(ToResponse).ToListAsync(cancellationToken);
    }

    public Task<ProductResponse?> RetrieveAsync(int id, CancellationToken cancellationToken) =>
        _db
            .Products.Where(p => p.ProductId == id)
            .Select(ToResponse)
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

        Apply(request, product);
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

    private static void Apply(ProductRequest request, Product product)
    {
        product.ProductName = request.ProductName;
        product.SupplierId = request.SupplierId;
        product.CategoryId = request.CategoryId;
        product.QuantityPerUnit = request.QuantityPerUnit;
        product.UnitPrice = request.UnitPrice;
        product.UnitsInStock = request.UnitsInStock;
        product.UnitsOnOrder = request.UnitsOnOrder;
        product.ReorderLevel = request.ReorderLevel;
        product.Discontinued = request.Discontinued;
    }
}
