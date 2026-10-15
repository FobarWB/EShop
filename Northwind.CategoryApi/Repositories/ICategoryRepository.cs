using EShop.Data.Entities; // Category

namespace Northwind.CategoryApi.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> CreateAsync(Category c);
        Task<IEnumerable<Category>> RetrieveAllAsync();
        Task<Category?> RetrieveAsync(int id);
        Task<Category?> UpdateAsync(int id, Category c);
        Task<bool?> DeleteAsync(int id);
    }
}