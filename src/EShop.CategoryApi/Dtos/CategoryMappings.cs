using EShop.Data.Entities;

namespace EShop.CategoryApi.Dtos;

public static class CategoryMappings
{
    // Extends the query, not the entity: EF Core translates the projection to SQL
    // and never reads the picture column.
    public static IQueryable<CategoryResponse> ToResponse(this IQueryable<Category> query) =>
        query.Select(c => new CategoryResponse(c.CategoryId, c.CategoryName, c.Description));

    public static void ApplyTo(this CategoryRequest request, Category category)
    {
        category.CategoryName = request.CategoryName;
        category.Description = request.Description;
    }
}
