---
type: service
verified: 2026-10-15
---

# ClientApp

Angular 18 single-page application: catalog, product pages and the shopping cart.

- Project: [Northwind.ClientApp](../../Northwind.ClientApp/)
- Local URL: http://localhost:4200 (`npm start`)

## Routes

| Route | Page |
| --- | --- |
| `/` | Home |
| `/privacy` | Privacy |
| `/cart` | Shopping cart |
| `/:categoryName/c{id}` | Category page |
| `/:productName/p{id}` | Product page |

## Dependencies

Calls [ProductApi](ProductApi.md), [CategoryApi](CategoryApi.md) and [ShoppingCart](ShoppingCart.md).
API base URLs are hardcoded in `src/app/services/*.service.ts`.

## Known issues

- The cart uses the API when the user is "authenticated" and `localStorage` otherwise; the flag is currently always `true`.
