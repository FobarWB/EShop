using EShop.Data.Entities;

namespace EShop.CustomerApi.Dtos;

public static class CustomerMappings
{
    public static IQueryable<AddressResponse> ToResponse(this IQueryable<CustomerAddress> query) =>
        query.Select(a => new AddressResponse(
            a.AddressId,
            a.RecipientName,
            a.Address,
            a.City,
            a.Region,
            a.PostalCode,
            a.Country,
            a.IsDefault
        ));

    public static void ApplyTo(this AddressRequest request, CustomerAddress address)
    {
        address.RecipientName = request.RecipientName;
        address.Address = request.Address;
        address.City = request.City;
        address.Region = request.Region;
        address.PostalCode = request.PostalCode;
        address.Country = request.Country;
        address.IsDefault = request.IsDefault;
    }
}
