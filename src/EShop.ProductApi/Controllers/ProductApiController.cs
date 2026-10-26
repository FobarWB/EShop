using System.Diagnostics;
using EShop.ProductApi.Dtos;
using EShop.ProductApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.ProductApi.Controllers;

[ApiController]
[Route("api/products")]
// Writes are for administrators; reads are public ([AllowAnonymous] below).
[Authorize(Roles = EShopAuthenticationExtensions.AdminRole)]
public class ProductApiController : ControllerBase
{
    private readonly IProductRepository _repository;

    public ProductApiController(IProductRepository repository)
    {
        _repository = repository;
    }

    // GET api/products, GET api/products?categoryId=1: always a list, possibly empty.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts(
        int? categoryId,
        CancellationToken cancellationToken
    )
    {
        var products = await _repository.RetrieveAllAsync(categoryId, cancellationToken);
        return Ok(products);
    }

    // GET api/products/1
    [HttpGet("{id}", Name = nameof(GetProduct))]
    [AllowAnonymous]
    [ProducesResponseType(404)]
    public async Task<ActionResult<ProductResponse>> GetProduct(
        int id,
        CancellationToken cancellationToken
    )
    {
        var product = await _repository.RetrieveAsync(id, cancellationToken);
        return product is null ? NotFound() : product;
    }

    // POST api/products
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<ProductResponse>> Create(
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken
    )
    {
        var product = await _repository.CreateAsync(request, cancellationToken);
        return CreatedAtRoute(
            routeName: nameof(GetProduct),
            routeValues: new { id = product.ProductId },
            value: product
        );
    }

    // PUT api/products/1: the id comes from the route only.
    [HttpPut("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] ProductRequest request,
        CancellationToken cancellationToken
    )
    {
        var updated = await _repository.UpdateAsync(id, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    // DELETE api/products/1
    [HttpDelete("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    [ProducesResponseType(409)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, cancellationToken);
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
