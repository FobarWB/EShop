// CustomerRepository.MaxAddresses: only hides the button, the server decides (409).
export const maxAddresses = 10;

// Body of POST and PUT /api/customers/me/addresses; the lengths are checked by CustomerApi.
export interface IAddressRequest
{
    recipientName: string;
    address: string;
    city: string;
    region: string | null;
    postalCode: string;
    country: string;
    isDefault: boolean;
}

export interface IAddress extends IAddressRequest
{
    addressId: number;
}
