using System.Diagnostics;
using EShop.ProductApi.Dtos;
using EShop.ProductApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.ProductApi.Controllers
{
    [ApiController]
    [Route("api/products")]
    // Writes are for administrators; reads are public ([AllowAnonymous] below).
    [Authorize(Roles = EShopAuthenticationExtensions.AdminRole)]
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
        [AllowAnonymous]
        [ProducesResponseType(200, Type = typeof(IEnumerable<ProductResponse>))]
        public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts(
            int? categoryId,
            CancellationToken cancellationToken
        )
        {
            var products = await repository.RetrieveAllAsync(categoryId, cancellationToken);
            return Ok(products);
        }

        // GET: api/products/[id]
        [HttpGet("{id}", Name = nameof(GetProduct))]
        [AllowAnonymous]
        [ProducesResponseType(200, Type = typeof(ProductResponse))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetProduct(int id, CancellationToken cancellationToken)
        {
            var p = await repository.RetrieveAsync(id, cancellationToken);
            if (p is null)
            {
                return NotFound();
            }
            return Ok(p);
        }

        // POST: api/products
        // BODY: ProductRequest (JSON)
        [HttpPost]
        [ProducesResponseType(201, Type = typeof(ProductResponse))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create(
            [FromBody] ProductRequest request,
            CancellationToken cancellationToken
        )
        {
            var addedProduct = await repository.CreateAsync(request, cancellationToken);
            return CreatedAtRoute( // 201
                routeName: nameof(GetProduct),
                routeValues: new { id = addedProduct.ProductId },
                value: addedProduct
            );
        }

        // PUT: api/products/[id]
        // BODY: ProductRequest (JSON); the id comes from the route only
        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> Update(
            int id,
            [FromBody] ProductRequest request,
            CancellationToken cancellationToken
        )
        {
            if (!await repository.UpdateAsync(id, request, cancellationToken))
            {
                return NotFound(); // 404
            }
            return NoContent(); // 204
        }

        // DELETE: api/products/[id]
        [HttpDelete("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
        {
            var result = await repository.DeleteAsync(id, cancellationToken);
            return result switch
            {
                DeleteResult.Deleted => NoContent(),
                DeleteResult.NotFound => NotFound(),
                DeleteResult.InUse => Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Product is in use",
                    detail: $"Product {id} is referenced by cart items or order details."
                ),
                _ => throw new UnreachableException(),
            };
        }
    }
}
