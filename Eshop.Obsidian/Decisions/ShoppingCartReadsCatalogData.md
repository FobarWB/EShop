---
type: decision
status: accepted
verified: 2026-10-27
issue: 33
---

# ShoppingCartReadsCatalogData

## Context

The cart page needs the name, category and price of every item. Before #33 the cart API returned only product ids
and quantities, and `CartComponent` requested each product and then its category: 1 + 2N HTTP requests, the same
category loaded several times. All APIs share one PostgreSQL database, but `products` and `categories` belong to
ProductApi and CategoryApi.

## Decision

- [ShoppingCart](../Services/ShoppingCart.md) reads `products` and `categories` directly: `GET api/carts/me` returns
  each item with `productName`, `categoryName` and `unitPrice` from one SQL query (`cart_items → products → categories`).
- The price is the **current** price of the product, read with the cart; no copy is stored in `cart_items`
  (no migration). A price change shows up in the cart at once, as it did before.
- The anonymous cart (`localStorage`) stores only ids and quantities for the same reason; `LocalCartService` loads the
  details when the cart is read.

## Consequences

- One request and one query for the cart page instead of 1 + 2N requests.
- ShoppingCart is coupled to the schema of two tables it does not own. This is accepted while the database is shared.
- With a database per service ([#51](https://github.com/FobarWB/EShop/issues/51)) the cart cannot join the catalog:
  it would keep its own copy of name and price, updated by events such as `ProductPriceChanged` (RabbitMQ, #50).
  The decision between "current price" and "price when added" has to be made again then (and at checkout, #42).
