// Product details are read with the cart: the price and the stock are the latest the cart knows. The category is
// an id (#75); the cart page looks the names up.
export interface ICartItem
{
    productId: number;
    productName: string;
    categoryId: number | null;
    unitPrice: number | null;
    quantity: number;
    unitsInStock: number;
    discontinued: boolean;
}

// The same rules as OrderApi at checkout; the page marks the item before the order is refused.
export function isUnavailable(item: ICartItem): boolean
{
    return item.discontinued || item.unitPrice === null;
}

// Why the item cannot be ordered as it is, as text for the page; null when it can.
export function cartItemProblem(item: ICartItem): string | null
{
    if (isUnavailable(item))
    {
        return 'No longer available';
    }
    if (item.quantity > item.unitsInStock)
    {
        return item.unitsInStock > 0 ? `Only ${item.unitsInStock} left` : 'Out of stock';
    }
    return null;
}
