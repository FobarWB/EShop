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

    public async Task<Category?> CreateAsync(Category c, CancellationToken cancellationToken)
    {
        _db.Categories.Add(c);
        var affected = await _db.SaveChangesAsync(cancellationToken);
        return affected > 0 ? c : null;
    }

    public async Task<IEnumerable<Category>> RetrieveAllAsync(
        CancellationToken cancellationToken
    ) => await _db.Categories.AsNoTracking().ToListAsync(cancellationToken);

    // Not tracked: the controller loads a category and then updates it with another instance
    // of the same key, which would conflict with a tracked entity.
    public Task<Category?> RetrieveAsync(int id, CancellationToken cancellationToken) =>
        _db
            .Categories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == id, cancellationToken);

    public async Task<Category?> UpdateAsync(
        int id,
        Category c,
        CancellationToken cancellationToken
    )
    {
        _db.Categories.Update(c);
        var affected = await _db.SaveChangesAsync(cancellationToken);
        return affected > 0 ? c : null;
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
