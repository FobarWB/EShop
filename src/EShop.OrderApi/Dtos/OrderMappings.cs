using EShop.Data.Entities;

namespace EShop.OrderApi.Dtos;

public static class OrderMappings
{
    // Orders placed through the API always have an order date and a full address (PlaceOrderRequest requires it),
    // and the repository returns only those orders (user_id is set): the "!" below cannot meet a null.
    public static IQueryable<OrderResponse> ToResponse(this IQueryable<Order> query) =>
        query.Select(o => new OrderResponse(
            o.OrderId,
            o.OrderDate!.Value,
            o.Status,
            o.CustomerEmail,
            o.ShipName!,
            o.ShipAddress!,
            o.ShipCity!,
            o.ShipRegion,
            o.ShipPostalCode!,
            o.ShipCountry!,
            o.Freight ?? 0m,
            o.OrderDetails.Sum(d => d.UnitPrice * d.Quantity) + (o.Freight ?? 0m),
            o.OrderDetails.OrderBy(d => d.ProductId)
                .Select(d => new OrderItemResponse(
                    d.ProductId,
                    d.ProductName,
                    d.UnitPrice,
                    d.Quantity
                ))
                .ToList()
        ));

    public static IQueryable<OrderSummaryResponse> ToSummaryResponse(
        this IQueryable<Order> query
    ) =>
        query.Select(o => new OrderSummaryResponse(
            o.OrderId,
            o.OrderDate!.Value,
            o.Status,
            o.OrderDetails.Sum(d => d.UnitPrice * d.Quantity) + (o.Freight ?? 0m),
            o.OrderDetails.Count
        ));
}
