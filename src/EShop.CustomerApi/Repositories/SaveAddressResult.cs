using EShop.CustomerApi.Dtos;

namespace EShop.CustomerApi.Repositories;

public enum SaveAddressStatus
{
    Saved,

    // No such address, or another user's address.
    NotFound,

    // The user already has CustomerRepository.MaxAddresses addresses.
    LimitReached,

    // A concurrent request made another address the default; nothing was saved.
    DefaultConflict,
}

public sealed record SaveAddressResult(SaveAddressStatus Status, AddressResponse? Address = null);
