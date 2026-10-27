import { IOrderItem } from './order-item.model';
import { IShippingAddress } from './shipping-address.model';

// Flat shipping fee; OrderApi charges the same (OrderRepository.StandardFreight).
export const standardFreight = 5;

// The prices are the copies stored at checkout, not the current product prices.
export interface IOrder extends IShippingAddress
{
    orderId: number;
    orderDate: string;
    freight: number;
    total: number;
    items: IOrderItem[];
}
