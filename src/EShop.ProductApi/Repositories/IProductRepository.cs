using EShop.Data.Entities; // Product

namespace EShop.ProductApi.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> CreateAsync(Product p);
        Task<IEnumerable<Product>> RetrieveAllAsync();
        Task<Product?> RetrieveAsync(int id);
        Task<Product?> UpdateAsync(int id, Product p);
        Task<DeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
    }
}
