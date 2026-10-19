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

    public async Task<Category?> CreateAsync(Category c)
    {
        _db.Categories.Add(c);
        var affected = await _db.SaveChangesAsync();
        return affected > 0 ? c : null;
    }

    public async Task<IEnumerable<Category>> RetrieveAllAsync() =>
        await _db.Categories.AsNoTracking().ToListAsync();

    // Not tracked: the controller loads a category and then updates it with another instance
    // of the same key, which would conflict with a tracked entity.
    public Task<Category?> RetrieveAsync(int id) =>
        _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryId == id);

    public async Task<Category?> UpdateAsync(int id, Category c)
    {
        _db.Categories.Update(c);
        var affected = await _db.SaveChangesAsync();
        return affected > 0 ? c : null;
    }

    public async Task<bool?> DeleteAsync(int id)
    {
        var c = await _db.Categories.FindAsync(id);
        if (c is null)
        {
            return null;
        }

        _db.Categories.Remove(c);
        return await _db.SaveChangesAsync() > 0;
    }
}
