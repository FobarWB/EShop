import { Product } from "./product.model";

export interface Category
{
    categoryId: number;
    categoryName: string;
    description: string | null;
    picture: string | null;
    products: Product[];
}