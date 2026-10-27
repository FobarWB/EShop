// PageRequest.MaxPageSize in the APIs.
export const maxPageSize = 100;

// One page of a collection endpoint (PagedResponse in the APIs).
export interface IPage<T>
{
    items: T[];
    page: number;
    pageSize: number;
    totalCount: number;
}
