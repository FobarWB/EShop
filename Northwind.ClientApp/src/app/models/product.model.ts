import { Category } from "./category.model";

export interface Product
{
    productId: number;
    productName: string;
    supplierId: number | null;
    categoryId: number | null;
    quantityPerUnit: string | null;
    unitPrice: number | null;
    unitsInStock: number | null;
    unitsOnOrder: number | null;
    reorderLevel: number | null;
    discontinued: boolean;
    category: Category | null;
}