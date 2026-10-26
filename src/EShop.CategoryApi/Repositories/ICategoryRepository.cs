using EShop.CategoryApi.Dtos;

namespace EShop.CategoryApi.Repositories
{
    public interface ICategoryRepository
    {
        Task<CategoryResponse> CreateAsync(
            CategoryRequest request,
            CancellationToken cancellationToken
        );
        Task<IReadOnlyList<CategoryResponse>> RetrieveAllAsync(
            string? name,
            CancellationToken cancellationToken
        );
        Task<CategoryResponse?> RetrieveAsync(int id, CancellationToken cancellationToken);

        // false: no category with this id.
        Task<bool> UpdateAsync(
            int id,
            CategoryRequest request,
            CancellationToken cancellationToken
        );
        Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
