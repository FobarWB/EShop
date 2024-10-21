import { IProduct } from "./product.model";


export interface ICartItem
{
    cartId: number;
    productId: number;
    quantity: number;
    product?: IProduct | null; // Optionally link the product model if needed
}