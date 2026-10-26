using System.Diagnostics;
using EShop.CategoryApi.Dtos;
using EShop.CategoryApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.CategoryApi.Controllers;

[ApiController]
[Route("api/categories")]
// Writes are for administrators; reads are public ([AllowAnonymous] below).
[Authorize(Roles = EShopAuthenticationExtensions.AdminRole)]
public class CategoryAPIController : ControllerBase
{
    private readonly ICategoryRepository _repository;

    public CategoryAPIController(ICategoryRepository repository)
    {
        _repository = repository;
    }

    // GET api/categories, GET api/categories?categoryName=bev: always a list, possibly empty.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories(
        string? categoryName,
        CancellationToken cancellationToken
    )
    {
        var name = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName;
        var categories = await _repository.RetrieveAllAsync(name, cancellationToken);
        return Ok(categories);
    }

    // GET api/categories/1
    [HttpGet("{id}", Name = nameof(GetCategory))]
    [AllowAnonymous]
    [ProducesResponseType(404)]
    public async Task<ActionResult<CategoryResponse>> GetCategory(
        int id,
        CancellationToken cancellationToken
    )
    {
        var category = await _repository.RetrieveAsync(id, cancellationToken);
        return category is null ? NotFound() : category;
    }

    // POST api/categories
    [HttpPost]
    [ProducesResponseType(201)]
    [ProducesResponseType(400)]
    public async Task<ActionResult<CategoryResponse>> Create(
        [FromBody] CategoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var category = await _repository.CreateAsync(request, cancellationToken);
        return CreatedAtRoute(
            routeName: nameof(GetCategory),
            routeValues: new { id = category.CategoryId },
            value: category
        );
    }

    // PUT api/categories/1: the id comes from the route only.
    [HttpPut("{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] CategoryRequest request,
        CancellationToken cancellationToken
    )
    {
        var updated = await _repository.UpdateAsync(id, request, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    // DELETE api/categories/1
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
                title: "Category is in use",
                detail: $"Category {id} still has products."
            ),
            _ => throw new UnreachableException(),
        };
    }
}
