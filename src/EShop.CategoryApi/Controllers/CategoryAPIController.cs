using System.Diagnostics;
using EShop.CategoryApi.Repositories;
using EShop.Data.Entities;
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
        [ProducesResponseType(200, Type = typeof(IEnumerable<Category>))]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories(
            string? categoryName,
            CancellationToken cancellationToken
        )
        {
            var name = string.IsNullOrWhiteSpace(categoryName) ? null : categoryName;
            IEnumerable<Category> categories = await repository.RetrieveAllAsync(
                name,
                cancellationToken
            );
            return Ok(categories);
        }

        // GET: api/categories/[id]
        [HttpGet("{id}", Name = nameof(GetCategory))]
        [AllowAnonymous]
        [ProducesResponseType(200, Type = typeof(Category))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCategory(int id, CancellationToken cancellationToken)
        {
            Category? c = await repository.RetrieveAsync(id, cancellationToken);
            if (c is null)
            {
                return NotFound();
            }
            return Ok(c);
        }

        // POST: api/categories
        // BODY: Category (JSON)
        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Category))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create(
            [FromBody] Category c,
            CancellationToken cancellationToken
        )
        {
            Category? addedCategory = await repository.CreateAsync(c, cancellationToken);

            if (addedCategory is null)
            {
                return BadRequest("Repository failed to create category");
            }
            else
            {
                return CreatedAtRoute(
                    routeName: nameof(GetCategory),
                    routeValues: new { id = addedCategory.CategoryId },
                    value: addedCategory
                );
            }
        }

        // PUT: api/categories/[id]
        // BODY: Category (JSON)
        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(
            int id,
            [FromBody] Category c,
            CancellationToken cancellationToken
        )
        {
            if (c.CategoryId != id)
            {
                return BadRequest();
            }

            Category? existing = await repository.RetrieveAsync(id, cancellationToken);

            if (existing is null)
            {
                return NotFound();
            }

            await repository.UpdateAsync(id, c, cancellationToken);
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
