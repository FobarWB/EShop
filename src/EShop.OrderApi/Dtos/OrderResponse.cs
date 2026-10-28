using EShop.Data.Entities;

namespace EShop.OrderApi.Dtos;

// The prices are the copies stored with the order at checkout, not the current product prices.
public sealed record OrderResponse(
    int OrderId,
    DateTime OrderDate,
    OrderStatus Status,
    string ShipName,
    string ShipAddress,
    string ShipCity,
    string? ShipRegion,
    string ShipPostalCode,
    string ShipCountry,
    decimal Freight,
    decimal Total,
    IReadOnlyList<OrderItemResponse> Items
);

public sealed record OrderItemResponse(
    int ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity
);
