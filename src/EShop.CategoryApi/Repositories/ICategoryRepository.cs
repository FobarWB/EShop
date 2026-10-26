using EShop.Data.Entities; // Category

namespace EShop.CategoryApi.Repositories
{
    public interface ICategoryRepository
    {
        Task<Category?> CreateAsync(Category c, CancellationToken cancellationToken);
        Task<IEnumerable<Category>> RetrieveAllAsync(
            string? name,
            CancellationToken cancellationToken
        );
        Task<Category?> RetrieveAsync(int id, CancellationToken cancellationToken);
        Task<Category?> UpdateAsync(int id, Category c, CancellationToken cancellationToken);
        Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
