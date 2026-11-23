import { IPlaceOrderRequest } from './place-order-request.model';

// Body of POST /api/orders/guest: as a signed-in order, with the email the account is created for.
export interface IGuestOrderRequest extends IPlaceOrderRequest
{
    email: string;
}
