---
type: service
verified: 2026-11-19
issue: 75
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
| POST | `/items` | Add a product, body `{ "productId": 7, "quantity": 2 }`; creates the cart if the user has none. 404 unknown or deleted product (by the cart's copy), 409 not enough stock (the copy's stock) |
| POST | `/merge` | Move the anonymous cart here, body `{ "items": [{ "productId": 7, "quantity": 2 }] }`, at most 100 items; 204. Sums quantities, caps by stock, skips unknown products ([AnonymousCartMerge](../Decisions/AnonymousCartMerge.md)) |
| PUT | `/items/{productId}` | Change the quantity, body `{ "quantity": 3 }` (a bare number is rejected since #33); 400 above stock or not in the cart |
| DELETE | `/items/{productId}` | Remove an item |
| DELETE | `/items` | Remove all items (400 if the user has no cart) |
| DELETE | `/` | Delete the cart |

`productId` and `quantity` must be at least 1 (`[Range]`, 400 ProblemDetails before the action runs).

## Contract

`CartResponse(CartId, CartItems)`; each `CartItemResponse` has `ProductId`, `ProductName`, `CategoryId` (null for a
product without a category; `CategoryName` until #75: the client resolves the name), `UnitPrice`, `Quantity`, and
since #42 `UnitsInStock` and `Discontinued`, so the client marks items that cannot be ordered as they are. The user id
and the creation time are not exposed.
Product details come from the cart's own copy, `cart_products`, joined in one SQL query (projection in
[CartMappings](../../src/EShop.ShoppingCart/Dtos/CartMappings.cs)); the price is the latest the copy has, not a copy
taken when the item was added. An item whose copy is deleted or missing is not shown. Before #33 the cart page
requested every product and its category (1 + 2N requests); from #33 to #75 the cart joined `products` and
`categories` ([ShoppingCartReadsCatalogData](../Decisions/ShoppingCartReadsCatalogData.md)). See
[DatabasePerService](../Decisions/DatabasePerService.md).

## Data

`Cart` (unique `UserId`) and `CartItem` (key `CartId` + `ProductId`, `Quantity`) in the shared PostgreSQL database `eshop` (Northwind data).
Deleting a cart deletes its items; `cart_items.product_id` has no foreign key since #75 (index
`ix_cart_items_product_id` stays).
`cart_products` (#75): `product_id` (the key from the events, not generated), name, `category_id`, price, stock,
`discontinued`, `deleted` (tombstone), `revision`. No foreign key to `products`, and none from `cart_items` to it yet
(#77, when the cart has its own DbContext).
Changes load the tracked cart with its items and save it without `db.Update`, so only changed columns are written;
an empty cart cleared or an unchanged quantity writes no rows and still succeeds.

## Consumed events (#75)

`ProductEventsHandler` on the queue `carts.products` (consumer `carts`; `product.changed` and `product.deleted` from
[ProductApi](ProductApi.md); retries 5 s / 30 s / 2 min, then the DLQ):

- `ProductChanged`: one `INSERT … ON CONFLICT (product_id) DO UPDATE … WHERE cart_products.revision < excluded.revision
  AND NOT cart_products.deleted`. No read before the write, so two deliveries at once cannot both pass a check.
- `ProductDeleted`: the tombstone (also for a product never seen: a late `ProductChanged` is then ignored) and
  `DELETE FROM cart_items WHERE product_id = …`.
- SQL because EF Core has no upsert; the interpolated values are parameters. Inside the consumer's transaction with the
  inbox row.
- The initial data comes from ProductApi's republish (`POST api/products/events/republish`), after this consumer has
  declared its queue.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); reads no other service's table since #75. RabbitMQ from
`EShop.Messaging`, contracts of `EShop.ProductApi.Contracts`; in compose it waits for a healthy RabbitMQ.
[OrderApi](OrderApi.md) reads the cart and deletes its items at checkout, in the transaction that stores the order.
JWT validation, `User.GetUserId()` and error handling from `EShop.ServiceDefaults`; tokens are issued by Keycloak.

## Tests

[Unit](../../tests/EShop.ShoppingCart.Tests.Unit/) (controller) and [integration](../../tests/EShop.ShoppingCart.Tests.Integration/)
(repository and API) tests; see [Testing](../Learning/Testing.md). The tests seed `cart_products`
(`ShoppingCartDatabase.SeedCartProductAsync`), not `products`. `ProductEventsHandlerTests` call the handler in a
transaction, as the consumer does: insert, newer and older revisions, a duplicate, tombstones, an unknown type.
The API tests remove the consumer (`EShopApiFactory`).

## Known issues

- Two concurrent first adds for the same user can both try to create a cart; the second fails on the unique `UserId` index (500).
- A quantity above stock or for an item that is not in the cart answers 400; 409 and 404 (as for POST) would be more precise.
- A discontinued product can still be added to the cart; the cart marks it and the checkout refuses it.
- The copy's stock is a hint: a checkout or a cancel in OrderApi does not reach it until #76.
- An item added before the first republish has no copy: hidden in the cart, still ordered by the checkout (OrderApi
  reads `cart_items`). Gone with #76.
