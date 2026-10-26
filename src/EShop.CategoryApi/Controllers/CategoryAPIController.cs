using System.Diagnostics;
using EShop.CategoryApi.Dtos;
using EShop.CategoryApi.Repositories;
using EShop.ServiceDefaults;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShop.CategoryApi.Controllers
{
    [ApiController]
    [Route("api/categories")]
    // Writes are for administrators; reads are public ([AllowAnonymous] below).
    [Authorize(Roles = EShopAuthenticationExtensions.AdminRole)]
    public class CategoryAPIController : ControllerBase
    {
        private readonly ICategoryRepository repository;

        public CategoryAPIController(ICategoryRepository repository)
        {
            this.repository = repository;
        }

        // GET: api/categories
        // GET: api/categories/?categoryName=[categoryName]
        // this will always return a list of categories (but it might be empty)
        [HttpGet]
        [AllowAnonymous]
        [ProducesResponseType(200, Type = typeof(IEnumerable<CategoryResponse>))]
        public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories(
            string? categoryName,
            CancellationToken cancellationToken
        )
        {
            var name = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName;
            var categories = await repository.RetrieveAllAsync(name, cancellationToken);
            return Ok(categories);
        }

        // GET: api/categories/[id]
        [HttpGet("{id}", Name = nameof(GetCategory))]
        [AllowAnonymous]
        [ProducesResponseType(200, Type = typeof(CategoryResponse))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCategory(int id, CancellationToken cancellationToken)
        {
            var c = await repository.RetrieveAsync(id, cancellationToken);
            if (c is null)
            {
                return NotFound();
            }
            return Ok(c);
        }

        // POST: api/categories
        // BODY: CategoryRequest (JSON)
        [HttpPost]
        [ProducesResponseType(201, Type = typeof(CategoryResponse))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create(
            [FromBody] CategoryRequest request,
            CancellationToken cancellationToken
        )
        {
            var addedCategory = await repository.CreateAsync(request, cancellationToken);
            return CreatedAtRoute(
                routeName: nameof(GetCategory),
                routeValues: new { id = addedCategory.CategoryId },
                value: addedCategory
            );
        }

        // PUT: api/categories/[id]
        // BODY: CategoryRequest (JSON); the id comes from the route only
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
            if (!await repository.UpdateAsync(id, request, cancellationToken))
            {
                return NotFound();
            }
            return NoContent();
        }

        // DELETE: api/categories/[id]
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
                    title: "Category is in use",
                    detail: $"Category {id} still has products."
                ),
                _ => throw new UnreachableException(),
            };
        }
    }
}
