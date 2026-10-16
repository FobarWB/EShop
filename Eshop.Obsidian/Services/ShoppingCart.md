---
type: service
verified: 2026-10-16
---

# ShoppingCart

REST API for shopping carts: one cart per user, items reference products.

- Project: [EShop.ShoppingCart](../../src/EShop.ShoppingCart/)
- Local URL: https://localhost:5004 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)

## Endpoints (`api/carts`)

| Method | Route | Action |
| --- | --- | --- |
| POST | `/create` | Create a cart for a user |
| GET | `/{userId}` | Get the cart with its items |
| POST | `/{userId}/add` | Add a product; fails if `UnitsInStock` is too low |
| PUT | `/{userId}/update/{productId}` | Change item quantity |
| DELETE | `/{userId}/remove/{productId}` | Remove an item |
| DELETE | `/{userId}/clear` | Remove all items |
| DELETE | `/{userId}` | Delete the cart |

## Data

`Cart` (unique `UserId`) and `CartItem` (key `CartId` + `ProductId`, `Quantity`) in the shared PostgreSQL database `eshop` (Northwind data).
Deleting a cart deletes its items; deleting a product that is in a cart is restricted.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); reads `Products` to check stock.

## Known issues

- No authentication: `userId` is taken from the client as-is.
- Every action wraps its body in `try/catch` returning 500; EF entities are returned directly.
- Adding an item fails with 400 when the user has no cart yet; the client never creates one ([#22](https://github.com/FobarWB/EShop/issues/22)).
