namespace EShop.CustomerApi.Dtos;

public sealed record AddressResponse(
    int AddressId,
    string RecipientName,
    string Address,
    string City,
    string? Region,
    string PostalCode,
    string Country,
    bool IsDefault
);
