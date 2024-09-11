using EShop.Shared; // Product

namespace Northwind.ProductApi.Repositories
{
    public interface IProductRepository
    {
        Task<Product?> CreateAsync(Product p);
        Task<IEnumerable<Product>> RetrieveAllAsync();
        Task<Product?> RetrieveAsync(int id);
        Task<Product?> UpdateAsync(int id, Product p);
        Task<bool?> DeleteAsync(int id);
    }
}