using System.ComponentModel.DataAnnotations;

namespace EShop.CustomerApi.Dtos;

// Body of POST and PUT api/customers/me/addresses. The lengths are those of PlaceOrderRequest (OrderApi),
// so a saved address can always be sent to the checkout.
public sealed record AddressRequest
{
    [Required, StringLength(40)]
    public required string RecipientName { get; init; }

    [Required, StringLength(60)]
    public required string Address { get; init; }

    [Required, StringLength(15)]
    public required string City { get; init; }

    [StringLength(15)]
    public string? Region { get; init; }

    [Required, StringLength(10)]
    public required string PostalCode { get; init; }

    [Required, StringLength(15)]
    public required string Country { get; init; }

    // true makes this the default address and clears the flag of the previous one.
    public bool IsDefault { get; init; }
}
