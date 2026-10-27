---
type: decision
status: accepted
verified: 2026-10-27
issue: 42
---

# IdempotentCheckout

## Context

POST is not idempotent. A double click, a retry after a timeout or a lost response would place a second order and
decrement the stock twice. After a network error the client cannot tell whether the request reached the server.

## Decision

- The client creates a UUID once per checkout attempt (`crypto.randomUUID()` when the checkout page opens) and sends it
  in the `Idempotency-Key` header with every try of that attempt. The header is required (400 without it).
- OrderApi stores it in `orders.idempotency_key`, unique together with `user_id`: per user, so another user's key can
  neither collide with an order nor return it.
- A request with a known key returns the existing order with 201, the same answer as the first try. This check runs
  before the cart is read: after the first success the cart is empty, and a retry would otherwise get 400.
- Two concurrent requests with the same key: the unique index rejects the second INSERT (23505); the repository catches
  exactly that violation (`ConstraintName`) and returns the first order.

## Consequences

- A retry is always safe; the client also disables "Place order" while a request is pending.
- A failed attempt (400, 409) stores nothing, so the same key can be sent again after the cart is fixed.
- A concurrent duplicate can lose on the products' `xmin` before it reaches the unique index and get 409; the next
  retry returns the order.
- Keys never expire: the order itself is the stored response. APIs such as Stripe's store the response and expire
  keys after 24 hours.
