---
type: service
verified: 2026-10-27
---

# ShoppingCart

REST API for shopping carts: one cart per user, items reference products.

- Project: [EShop.ShoppingCart](../../src/EShop.ShoppingCart/)
- Local URL: https://localhost:5004 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `shoppingcart`, through nginx at http://localhost:8080/api/carts ([Dockerfile](../../src/EShop.ShoppingCart/Dockerfile))

## Endpoints (`api/carts/me`)

Every endpoint needs a signed-in user and works on that user's cart: the id is the token's `sub`, never a request value
(see [Authentication](../Learning/Authentication.md)). 401 without a token. Errors are ProblemDetails.

| Method | Route | Action |
| --- | --- | --- |
| GET | `/` | Get the cart (`CartResponse`); 404 while the user has none |
| POST | `/items` | Add a product, body `{ "productId": 7, "quantity": 2 }`; creates the cart if the user has none. 404 unknown product, 409 not enough stock |
| PUT | `/items/{productId}` | Change the quantity, body `{ "quantity": 3 }` (a bare number is rejected since #33); 400 above stock or not in the cart |
| DELETE | `/items/{productId}` | Remove an item |
| DELETE | `/items` | Remove all items (400 if the user has no cart) |
| DELETE | `/` | Delete the cart |

`productId` and `quantity` must be at least 1 (`[Range]`, 400 ProblemDetails before the action runs).

## Contract

`CartResponse(CartId, CartItems)`; each `CartItemResponse` has `ProductId`, `ProductName`, `CategoryName` (null for a
product without a category), `UnitPrice` and `Quantity`. The user id and the creation time are not exposed.
Product details are read with the cart in one SQL query (`cart_items → products → categories`, projection in
[CartMappings](../../src/EShop.ShoppingCart/Dtos/CartMappings.cs)); the price is the current one, not a copy taken
when the item was added. Before #33 the cart page requested every product and its category (1 + 2N requests).
See [ShoppingCartReadsCatalogData](../Decisions/ShoppingCartReadsCatalogData.md).

## Data

`Cart` (unique `UserId`) and `CartItem` (key `CartId` + `ProductId`, `Quantity`) in the shared PostgreSQL database `eshop` (Northwind data).
Deleting a cart deletes its items; deleting a product that is in a cart is restricted (`ON DELETE RESTRICT`).
Changes load the tracked cart with its items and save it without `db.Update`, so only changed columns are written;
an empty cart cleared or an unchanged quantity writes no rows and still succeeds.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); reads `products` (stock, name, price) and `categories` (name).
JWT validation, `User.GetUserId()` and error handling from `EShop.ServiceDefaults`; tokens are issued by Keycloak.

## Tests

[Unit](../../tests/EShop.ShoppingCart.Tests.Unit/) (controller) and [integration](../../tests/EShop.ShoppingCart.Tests.Integration/)
(repository and API) tests; see [Testing](../Learning/Testing.md).

## Known issues

- Two concurrent first adds for the same user can both try to create a cart; the second fails on the unique `UserId` index (500).
- A quantity above stock or for an item that is not in the cart answers 400; 409 and 404 (as for POST) would be more precise.
