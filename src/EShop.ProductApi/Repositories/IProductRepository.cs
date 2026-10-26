using EShop.ProductApi.Dtos;

namespace EShop.ProductApi.Repositories
{
    public interface IProductRepository
    {
        Task<ProductResponse> CreateAsync(
            ProductRequest request,
            CancellationToken cancellationToken
        );
        Task<IReadOnlyList<ProductResponse>> RetrieveAllAsync(
            int? categoryId,
            CancellationToken cancellationToken
        );
        Task<ProductResponse?> RetrieveAsync(int id, CancellationToken cancellationToken);

        // false: no product with this id.
        Task<bool> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken);
        Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
