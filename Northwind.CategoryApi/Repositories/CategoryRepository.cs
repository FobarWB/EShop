using Microsoft.EntityFrameworkCore.ChangeTracking; // EntityEntry<T>
using EShop.Shared; // Category
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Northwind.CategoryApi.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {

        private static ConcurrentDictionary<int, Category>? categoryCache;

        private NorthwindContext db;

        public CategoryRepository(NorthwindContext injectedContext)
        {
            db = injectedContext;
            if (categoryCache is null)
            {
                categoryCache = new ConcurrentDictionary<int, Category>(
                    db.Categories.ToDictionary(c => c.CategoryId));
            }
        }

        public async Task<Category?> CreateAsync(Category c)
        {
            EntityEntry<Category> added = await db.Categories.AddAsync(c);
            int affected = await db.SaveChangesAsync();
            if (affected > 0)
            {
                if (categoryCache is null) return c;

                return categoryCache.AddOrUpdate(c.CategoryId, c, UpdateCache);
            }
            else
            {
                return null;
            }
        }

        public async Task<bool?> DeleteAsync(int id)
        {
            Category? c = db.Categories.Find(id);
            if (c is null) return null;
            db.Categories.Remove(c);
            int affected = await db.SaveChangesAsync();
            if (affected > 0)
            {
                if (categoryCache is null) return null;

                return categoryCache.TryRemove(id, out c);
            }
            else
            {
                return null;
            }
        }

        public async Task<IEnumerable<Category>> RetrieveAllAsync()
        {
            return await Task.FromResult(categoryCache is null
             ? Enumerable.Empty<Category>() : categoryCache.Values);
        }

        public async Task<Category?> RetrieveAsync(int id)
        {
            if (categoryCache is null) return null!;
            categoryCache.TryGetValue(id, out Category? c);
            return await Task.FromResult(c);
        }

        public async Task<Category?> UpdateAsync(int id, Category c)
        {
            db.Categories.Update(c);
            int affected = await db.SaveChangesAsync();
            if (affected > 0)
            {
                return UpdateCache(id, c);
            }
            return null;
        }

        private Category UpdateCache(int id, Category c)
        {
            Category? old;
            if (categoryCache is not null)
            {
                if (categoryCache.TryGetValue(id, out old))
                {
                    if (categoryCache.TryUpdate(id, c, old))
                    {
                        return c;
                    }
                }
            }
            return null!;
        }
    }
}