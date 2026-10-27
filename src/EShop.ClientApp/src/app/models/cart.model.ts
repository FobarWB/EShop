import { ICartItem } from "./cart-item.model";

export interface ICart
{
    cartId: number;
    cartItems: ICartItem[];
}
