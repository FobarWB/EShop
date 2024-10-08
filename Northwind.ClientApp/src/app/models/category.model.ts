import { IProduct } from "./product.model";

export interface ICategory
{
    categoryId: number;
    categoryName: string;
    description: string | null;
    picture: string | null;
    products: IProduct[];
}