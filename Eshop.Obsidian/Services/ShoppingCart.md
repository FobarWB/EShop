---
type: service
verified: 2026-10-26
---

# ShoppingCart

REST API for shopping carts: one cart per user, items reference products.

- Project: [EShop.ShoppingCart](../../src/EShop.ShoppingCart/)
- Local URL: https://localhost:5004 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `shoppingcart`, through nginx at http://localhost:8080/api/carts ([Dockerfile](../../src/EShop.ShoppingCart/Dockerfile))

## Endpoints (`api/carts/me`)

Every endpoint needs a signed-in user and works on that user's cart: the id is the token's `sub`, never a request value
(see [Authentication](../Learning/Authentication.md)). 401 without a token.

| Method | Route | Action |
| --- | --- | --- |
| GET | `/` | Get the cart with its items; 404 while the user has none |
| POST | `/items` | Add a product; creates the cart if the user has none. 404 unknown product, 409 not enough stock |
| PUT | `/items/{productId}` | Change item quantity |
| DELETE | `/items/{productId}` | Remove an item |
| DELETE | `/items` | Remove all items (400 if the user has no cart) |
| DELETE | `/` | Delete the cart |

## Data

`Cart` (unique `UserId`) and `CartItem` (key `CartId` + `ProductId`, `Quantity`) in the shared PostgreSQL database `eshop` (Northwind data).
Deleting a cart deletes its items; deleting a product that is in a cart is restricted.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); reads `Products` to check stock.
JWT validation and `User.GetUserId()` from `EShop.ServiceDefaults`; tokens are issued by Keycloak.

## Tests

[Unit](../../tests/EShop.ShoppingCart.Tests.Unit/) (controller) and [integration](../../tests/EShop.ShoppingCart.Tests.Integration/)
(repository and API) tests; see [Testing](../Learning/Testing.md).

## Known issues

- EF entities are returned directly (#33).
- Two concurrent first adds for the same user can both try to create a cart; the second fails on the unique `UserId` index (500).
