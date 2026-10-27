// Product details are read with the cart: the price is the current one.
export interface ICartItem
{
    productId: number;
    productName: string;
    categoryName: string | null;
    unitPrice: number | null;
    quantity: number;
}
