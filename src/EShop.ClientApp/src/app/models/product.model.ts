export interface IProduct
{
    productId: number;
    productName: string;
    supplierId: number | null;
    categoryId: number | null;
    quantityPerUnit: string | null;
    unitPrice: number | null;
    unitsInStock: number;
    unitsOnOrder: number | null;
    reorderLevel: number | null;
    discontinued: boolean;
}