using System.Text.RegularExpressions;
using EShop.CategoryApi.Dtos;
using EShop.Data;
using EShop.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace EShop.CategoryApi.Repositories;

public class CategoryRepository : ICategoryRepository
{
    private readonly EShopDbContext _db;

    public CategoryRepository(EShopDbContext db)
    {
        _db = db;
    }

    public async Task<CategoryResponse> CreateAsync(
        CategoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var category = new Category();
        request.ApplyTo(category);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync(cancellationToken);

        // Read back through the same projection: no second copy of the mapping.
        return await _db
            .Categories.Where(c => c.CategoryId == category.CategoryId)
            .ToResponse()
            .SingleAsync(cancellationToken);
    }

    // name null: all categories; otherwise a case-insensitive "contains".
    // A projection to a DTO is never tracked.
    public async Task<IReadOnlyList<CategoryResponse>> RetrieveAllAsync(
        string? name,
        CancellationToken cancellationToken
    )
    {
        IQueryable<Category> query = _db.Categories;
        if (name is not null)
        {
            // % and _ are LIKE wildcards and \ is its escape character: match them literally.
            var pattern = $"%{Regex.Replace(name, @"[\\%_]", @"\$0")}%";
            query = query.Where(c => EF.Functions.ILike(c.CategoryName, pattern));
        }
        return await query.ToResponse().ToListAsync(cancellationToken);
    }

    public Task<CategoryResponse?> RetrieveAsync(int id, CancellationToken cancellationToken) =>
        _db
            .Categories.Where(c => c.CategoryId == id)
            .ToResponse()
            .FirstOrDefaultAsync(cancellationToken);

    // Loads the tracked entity and copies the request onto it: SaveChanges updates only the changed
    // columns, so the stored picture stays.
    public async Task<bool> UpdateAsync(
        int id,
        CategoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var category = await _db.Categories.FindAsync([id], cancellationToken);
        if (category is null)
        {
            return false;
        }

        request.ApplyTo(category);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var c = await _db.Categories.FindAsync([id], cancellationToken);
        if (c is null)
        {
            return DeleteResult.NotFound;
        }

        // products reference categories without cascade: PostgreSQL would reject
        // the DELETE (23503), so check first and let the controller return 409.
        if (await _db.Products.AnyAsync(p => p.CategoryId == id, cancellationToken))
        {
            return DeleteResult.InUse;
        }

        _db.Categories.Remove(c);
        await _db.SaveChangesAsync(cancellationToken);
        return DeleteResult.Deleted;
    }
}
