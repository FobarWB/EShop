export interface IShippingAddress
{
    shipName: string;
    shipAddress: string;
    shipCity: string;
    shipRegion: string | null;
    shipPostalCode: string;
    shipCountry: string;
}
