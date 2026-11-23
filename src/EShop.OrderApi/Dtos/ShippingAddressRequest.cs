using System.ComponentModel.DataAnnotations;

namespace EShop.OrderApi.Dtos;

// The shipping address of a checkout, signed in or as a guest. The lengths are those of the Northwind orders columns.
public sealed record ShippingAddressRequest
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
