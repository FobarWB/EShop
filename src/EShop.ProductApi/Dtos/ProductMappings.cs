using EShop.Data.Entities;

namespace EShop.ProductApi.Dtos;

public static class ProductMappings
{
    // Extends the query, not the entity: EF Core translates the projection to SQL
    // and reads only these columns.
    public static IQueryable<ProductResponse> ToResponse(this IQueryable<Product> query) =>
        query.Select(p => new ProductResponse(
            p.ProductId,
            p.ProductName,
            p.SupplierId,
            p.CategoryId,
            p.QuantityPerUnit,
            p.UnitPrice,
            p.UnitsInStock,
            p.UnitsOnOrder,
            p.ReorderLevel,
            p.Discontinued
        ));

    public static void ApplyTo(this ProductRequest request, Product product)
    {
        product.ProductName = request.ProductName;
        product.SupplierId = request.SupplierId;
        product.CategoryId = request.CategoryId;
        product.QuantityPerUnit = request.QuantityPerUnit;
        product.UnitPrice = request.UnitPrice;
        product.UnitsInStock = request.UnitsInStock;
        product.UnitsOnOrder = request.UnitsOnOrder;
        product.ReorderLevel = request.ReorderLevel;
        product.Discontinued = request.Discontinued;
    }
}
