import { IOrderItem } from './order-item.model';
import { IShippingAddress } from './shipping-address.model';

// Flat shipping fee; OrderApi charges the same (OrderRepository.StandardFreight).
export const standardFreight = 5;

// The statuses of OrderApi (OrderStatus), sent as names.
export type OrderStatus = 'Pending' | 'Paid' | 'Shipped' | 'Delivered' | 'Cancelled';

// Mirrors the server rule (Order.Lifecycle.cs) only to show or hide the button; the server decides (409).
export const cancellableStatuses: readonly OrderStatus[] = ['Pending', 'Paid'];

// The prices are the copies stored at checkout, not the current product prices.
export interface IOrder extends IShippingAddress
{
    orderId: number;
    orderDate: string;
    status: OrderStatus;
    freight: number;
    total: number;
    items: IOrderItem[];
}
