using System.ComponentModel.DataAnnotations;

namespace EShop.OrderApi.Dtos;

// Body of POST api/orders: only the shipping address. The items come from the signed-in user's cart and the
// prices from the products at checkout, so the client cannot send its own prices.
// The lengths are those of the Northwind orders columns.
public sealed record PlaceOrderRequest
{
    [Required, StringLength(40)]
    public required string ShipName { get; init; }

    [Required, StringLength(60)]
    public required string ShipAddress { get; init; }

    [Required, StringLength(15)]
    public required string ShipCity { get; init; }

    [StringLength(15)]
    public string? ShipRegion { get; init; }

    [Required, StringLength(10)]
    public required string ShipPostalCode { get; init; }

    [Required, StringLength(15)]
    public required string ShipCountry { get; init; }
}
