import { IProduct } from "./product.model";


export interface ICartItem
{
    cartId: number;
    productId: number;
    quantity: number
}