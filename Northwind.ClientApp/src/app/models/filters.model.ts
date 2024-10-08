export interface IFilters
{
    priceRange?: {
        min: number;
        max: number;
    };
    unitsInStock?: boolean;
    discounted?: boolean;
}
