using EShop.Data.Entities;
using Northwind.CategoryApi.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace Northwind.CategoryApi.Controllers
{
    [ApiController]
    [Route("api/categories")]
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
        [ProducesResponseType(200, Type = typeof(IEnumerable<Category>))]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories(string? categoryName)
        {
            IEnumerable<Category> categories = await repository.RetrieveAllAsync();
            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return Ok(categories);
            }
            else
            {
                categoryName = categoryName.ToUpper();
                return Ok(categories.Where(c => c.CategoryName.ToUpper().Contains(categoryName)));
            }
        }

        // GET: api/categories/[id]
        [HttpGet("{id}", Name = nameof(GetCategory))]
        [ProducesResponseType(200, Type = typeof(Category))]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCategory(int id)
        {
            Category? c = await repository.RetrieveAsync(id);
            if (c is null)
            {
                return NotFound();
            }
            return Ok(c);
        }

        // POST: api/categories
        // BODY: Category (JSON, XML)
        [HttpPost]
        [ProducesResponseType(201, Type = typeof(Category))]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] Category c)
        {
            if (c is null)
            {
                return BadRequest();
            }

            Category? addedCategory = await repository.CreateAsync(c);

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
        // BODY: Category (JSON, XML)
        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromBody] Category c)
        {
            if (c is null || c.CategoryId != id)
            {
                return BadRequest();
            }

            Category? existing = await repository.RetrieveAsync(id);

            if (existing is null)
            {
                return NotFound();
            }

            await repository.UpdateAsync(id, c);
            return NoContent();
        }

        // DELETE: api/categories/[id]
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
                    Type = "https://localhost:5003/categories/failed-to-delete",
                    Title = $"Category Id {id} found but failed to delete.",
                    Detail = "More details like Company Name, Country and so on.",
                    Instance = HttpContext.Request.Path
                };
            }

            Category? existing = await repository.RetrieveAsync(id!.Value);
            if (existing is null)
            {
                return NotFound();
            }

            bool? deleted = await repository.DeleteAsync(id.Value);

            if (deleted.HasValue && deleted.Value)
            {
                return NoContent();
            }
            else
            {
                return BadRequest(
                    $"Category {id} was found but failed to delete"
                );
            }
        }
    }
}