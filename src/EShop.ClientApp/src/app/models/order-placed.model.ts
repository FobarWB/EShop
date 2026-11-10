import { IOrder } from './order.model';

// What the checkout hands to /order-placed in the navigation state: a guest cannot load /orders/:id.
export interface IOrderPlacedState
{
    order: IOrder;
    email: string;
}
