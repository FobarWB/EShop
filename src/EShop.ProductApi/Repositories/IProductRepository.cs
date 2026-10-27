using EShop.ProductApi.Dtos;
using EShop.ServiceDefaults;

namespace EShop.ProductApi.Repositories;

public interface IProductRepository
{
    Task<ProductResponse> CreateAsync(ProductRequest request, CancellationToken cancellationToken);
    Task<PagedResponse<ProductResponse>> RetrieveAllAsync(
        int? categoryId,
        PageRequest paging,
        CancellationToken cancellationToken
    );
    Task<ProductResponse?> RetrieveAsync(int id, CancellationToken cancellationToken);

    // false: no product with this id.
    Task<bool> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken);
    Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
}
