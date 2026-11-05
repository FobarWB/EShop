using EShop.CustomerApi.Dtos;

namespace EShop.CustomerApi.Repositories;

public interface ICustomerRepository
{
    // Null when the user has no profile yet or no phone: reading never creates the profile.
    Task<string?> RetrievePhoneAsync(string userId, CancellationToken cancellationToken);

    Task UpdatePhoneAsync(string userId, string? phone, CancellationToken cancellationToken);

    Task<List<AddressResponse>> RetrieveAddressesAsync(
        string userId,
        CancellationToken cancellationToken
    );

    Task<AddressResponse?> RetrieveAddressAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken
    );

    Task<SaveAddressResult> AddAddressAsync(
        string userId,
        AddressRequest request,
        CancellationToken cancellationToken
    );

    Task<SaveAddressResult> UpdateAddressAsync(
        string userId,
        int addressId,
        AddressRequest request,
        CancellationToken cancellationToken
    );

    Task<bool> DeleteAddressAsync(
        string userId,
        int addressId,
        CancellationToken cancellationToken
    );
}
