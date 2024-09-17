using EShop.Shared;
using Northwind.ProductApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Northwind.ProductApi.Controllers
{
    [ApiController]
    [Route("api/products")]
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
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts(int? categoryId)
        {
            IEnumerable<Product> products = await repository.RetrieveAllAsync();
            if (!categoryId.HasValue)
            {
                return Ok(products);
            }
            else
            {
                return Ok(products.Where(p => p.CategoryId == categoryId));
            }
        }

        [HttpGet("{id}", Name = nameof(GetProduct))]
        [ProducesResponseType(200, Type = typeof(Product))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetProduct(int id)
        {
            Product? p = await repository.RetrieveAsync(id);
            if (p is null)
            {
                return NotFound();
            }
            return Ok(p);
        }

        // POST: api/products
        // BODY: Product (JSON, XML)
        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Product))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] Product p)
        {
            if (p is null)
            {
                return BadRequest(); // 400
            }

            Product? addedProduct = await repository.CreateAsync(p);

            if (addedProduct is null)
            {
                return BadRequest("Repository failed to create product");
            }
            else
            {
                return CreatedAtRoute(  // 201
                    routeName: nameof(GetProduct),
                    routeValues: new { id = addedProduct.ProductId },
                    value: addedProduct
                );
            }
        }

        // PUT: api/products/[id]
        // BODY: Product (JSON, XML)
        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> Update(int id, [FromBody] Product p)
        {
            if (p is null || p.ProductId != id)
            {
                return BadRequest(); // 400
            }

            Product? existing = await repository.RetrieveAsync(id);

            if (existing is null)
            {
                return NotFound(); // 404
            }

            await repository.UpdateAsync(id, p);
            return new NoContentResult(); //204
        }

        // DELETE: api/products/[id]
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (!id.HasValue)
            {
                ProblemDetails problemDetails = new()
                {
                    Status = StatusCodes.Status400BadRequest,
                    Type = "https://localhost:5002/products/failed-to-delete",
                    Title = $"Product Id {id} found but failed to delete.",
                    Detail = "More details like Company Name, Country and so on.",
                    Instance = HttpContext.Request.Path
                };
                return BadRequest(problemDetails); // 400 Bad Request
            }

            Product? existing = await repository.RetrieveAsync(id.Value);
            if (existing == null)
            {
                return NotFound(); // 404
            }

            bool? deleted = await repository.DeleteAsync(id.Value);

            if (deleted.HasValue && deleted.Value)
            {
                return new NoContentResult(); // 204
            }
            else
            {
                return BadRequest(
                    $"Product {id} was found but failed to delete"
                ); // 400
            }
        }
    }
}