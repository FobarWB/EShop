using System.ComponentModel.DataAnnotations;

namespace EShop.CategoryApi.Dtos;

// Body of POST and PUT: the id comes from the route; an update keeps the stored picture.
public sealed record CategoryRequest
{
    [Required, StringLength(15)]
    public required string CategoryName { get; init; }

    public string? Description { get; init; }
}
