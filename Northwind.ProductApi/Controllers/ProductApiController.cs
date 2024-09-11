using EShop.Shared;
using Northwind.ProductApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Northwind.ProductApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductApiController : ControllerBase
    {
        private readonly IProductRepository repository;

        public ProductApiController(IProductRepository repository)
        {
            this.repository = repository;
        }

        // GET: api/products
        // GET: api/products/?categoryId=[categoryName]
        // this will always return a list of products (but it might be empty)
        [HttpGet]
        [ProducesResponseType(200, Type = typeof(IEnumerable<Product>))]
        public async Task<IEnumerable<Product>> GetProducts(int? categoryId)
        {
            if (!categoryId.HasValue)
            {
                return await repository.RetrieveAllAsync();
            }
            else
            {
                return (await repository.RetrieveAllAsync())
                .Where(p => p.CategoryId == categoryId);
            }
        }
    }
}