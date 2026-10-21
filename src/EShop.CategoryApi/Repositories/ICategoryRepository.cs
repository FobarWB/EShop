using EShop.Data.Entities; // Category

namespace EShop.CategoryApi.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> CreateAsync(Category c);
        Task<IEnumerable<Category>> RetrieveAllAsync();
        Task<Category?> RetrieveAsync(int id);
        Task<Category?> UpdateAsync(int id, Category c);
        Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
