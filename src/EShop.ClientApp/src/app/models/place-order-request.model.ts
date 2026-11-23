import { IShippingAddress } from './shipping-address.model';

// A line of a checkout: only what the buyer chooses; OrderApi takes names and prices from its copy of the catalog.
export interface IOrderItemRequest
{
    productId: number;
    quantity: number;
}

// Body of POST /api/orders: the address and the items of the user's cart (#76: OrderApi no longer reads carts).
export interface IPlaceOrderRequest
{
    address: IShippingAddress;
    items: IOrderItemRequest[];
}
