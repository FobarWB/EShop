using Microsoft.EntityFrameworkCore.ChangeTracking; // EntityEntry<T>
using EShop.Data;
using EShop.Data.Entities; // Product
using System.Collections.Concurrent;

namespace EShop.ProductApi.Repositories
{
    public class ProductRepository : IProductRepository
    {
        private static ConcurrentDictionary<int, Product>? productCache;
        
        private EShopDbContext db;

        public ProductRepository(EShopDbContext injectedContext)
        {
            db = injectedContext;
            if (productCache is null)
            {
                productCache = new ConcurrentDictionary<int, Product>(
                    db.Products.ToDictionary(p => p.ProductId));
            }
        }

        public async Task<Product?> CreateAsync(Product p)
        {
            EntityEntry<Product> added = await db.Products.AddAsync(p);
            int affected = await db.SaveChangesAsync();
            if (affected > 0)
            {
                if (productCache is null) return p;

                return productCache.AddOrUpdate(p.ProductId, p, UpdateCache);
            }
            else
            {
                return null;
            }
        }

        public async Task<bool?> DeleteAsync(int id)
        {
            Product? p = db.Products.Find(id);
            if (p is null) return null;
            db.Products.Remove(p);
            int affected = await db.SaveChangesAsync();
            if (affected > 0)
            {
                if (productCache is null) return null;

                return productCache.TryRemove(id, out p);
            }
            else
            {
                return null;
            }
        }
        
        public async Task<Product?> UpdateAsync(int id, Product p)
        {
            db.Products.Update(p);
            int affected = await db.SaveChangesAsync();
            if (affected > 0)
            {
                return UpdateCache(id, p);
            }
            return null;
        }

        public Task<IEnumerable<Product>> RetrieveAllAsync()
        {
            return Task.FromResult(productCache is null
             ? Enumerable.Empty<Product>() : productCache.Values);
        }

        public Task<Product?> RetrieveAsync(int id)
        {
            if (productCache is null) return null!;
            productCache.TryGetValue(id, out Product? p);
            return Task.FromResult(p);
        }

        private Product UpdateCache(int id, Product p)
        {
            Product? old;
            if (productCache is not null)
            {
                if (productCache.TryGetValue(id, out old))
                {
                    if(productCache.TryUpdate(id, p, old))
                    {
                        return p;
                    }
                }
            }
            return null!;
        }
    }
}