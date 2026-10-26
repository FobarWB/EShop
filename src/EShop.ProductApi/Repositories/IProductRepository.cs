using EShop.Data.Entities; // Product

namespace EShop.ProductApi.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> CreateAsync(Product p, CancellationToken cancellationToken);
        Task<IEnumerable<Product>> RetrieveAllAsync(CancellationToken cancellationToken);
        Task<Product?> RetrieveAsync(int id, CancellationToken cancellationToken);
        Task<Product?> UpdateAsync(int id, Product p, CancellationToken cancellationToken);
        Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
