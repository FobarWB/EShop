using System.ComponentModel.DataAnnotations;

namespace EShop.ProductApi.Dtos;

// Body of POST and PUT: the id comes from the route, related data cannot be posted.
public sealed record ProductRequest
{
    [Required, StringLength(40)]
    public required string ProductName { get; init; }

    public int? SupplierId { get; init; }

    public int? CategoryId { get; init; }

    [StringLength(20)]
    public string? QuantityPerUnit { get; init; }

    public decimal? UnitPrice { get; init; }

    public short UnitsInStock { get; init; }

    public short? UnitsOnOrder { get; init; }

    public short? ReorderLevel { get; init; }

    public bool Discontinued { get; init; }
}
