namespace EShop.ProductApi.Dtos;

public sealed record ProductResponse(
    int ProductId,
    string ProductName,
    int? SupplierId,
    int? CategoryId,
    string? QuantityPerUnit,
    decimal? UnitPrice,
    short UnitsInStock,
    short? UnitsOnOrder,
    short? ReorderLevel,
    bool Discontinued
);
