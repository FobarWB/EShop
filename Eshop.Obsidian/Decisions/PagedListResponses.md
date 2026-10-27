---
type: decision
status: accepted
verified: 2026-10-27
issue: 33
---

# PagedListResponses

## Context

`GET api/products` and `GET api/categories` returned whole tables. The API rules require paged collection endpoints.
The client filters and sorts a category in the browser, which only gives correct results when it has the whole category.

## Decision

- Query string `?page=1&pageSize=20`: `page` ≥ 1, `pageSize` 1–100, default 20; out of range is 400 ProblemDetails.
  The maximum keeps a client from asking for the whole table again.
- Response envelope `{ items, page, pageSize, totalCount }` (`PagedResponse<T>`), not a bare array with a
  `X-Total-Count` or `Link` header: the metadata is visible in the contract and in OpenAPI.
- Offset paging (`COUNT(*)`, then `ORDER BY <primary key> OFFSET … LIMIT …`), ordered before the projection.
  Without `ORDER BY` PostgreSQL may return rows in any order, so a row could appear on two pages or on none.
- The parameter is bound as `[FromQuery] PageRequest paging`: named `page`, MVC looks for `page.Page`, finds nothing
  and silently uses the defaults (`?page=2` returned page 1, `?page=0` passed validation; both caught by tests).
- The client keeps filtering in the browser and asks for one page of 100; server-side filters, sorting and paging
  are [#61](https://github.com/FobarWB/EShop/issues/61).

## Consequences

- Two queries per page (count and rows).
- Offset paging reads and discards the skipped rows: fine for a catalog of this size; large tables would use keyset
  paging (`WHERE id > last id ORDER BY id LIMIT n`), which also stays stable while rows are inserted.
- A category with more than 100 products shows only the first 100 until #61 (the page says so).
