import { ICartItem } from "./cart-item.model";

export interface ICart
{
    cartId: number;
    userId: string | null;
    createdAt: Date;
    cartItems: ICartItem[];
}
