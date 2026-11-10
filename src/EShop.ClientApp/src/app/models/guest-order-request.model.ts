import { IShippingAddress } from './shipping-address.model';

// Body of POST /api/orders/guest: the items of the browser's cart; the server takes prices and stock from the products.
export interface IGuestOrderRequest
{
    email: string;
    address: IShippingAddress;
    items: { productId: number; quantity: number }[];
}
