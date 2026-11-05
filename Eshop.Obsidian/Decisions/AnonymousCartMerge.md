---
type: decision
status: accepted
verified: 2026-11-05
issue: 24
---

# AnonymousCartMerge

## Context

An anonymous cart lives in `localStorage`, so visitors who never sign in leave no rows on the server. After the sign-in
its items must reach the server cart. Adding them one by one through `POST /items` fails above the stock (409), and a
failure in the middle leaves a partial merge that a retry would double.

## Decision

- `POST /api/carts/me/merge` takes all items and stores them in one `SaveChanges`, one transaction.
- Quantities are summed with the server cart and capped by the stock. An unknown or out-of-stock product is skipped;
  a server quantity above the current stock is not lowered: the cart page marks it. The answer is always 204.
- The client merges inside `CartService.getCart()`, not on a sign-in event: a guarded page reads the cart right after
  the redirect from Keycloak and must not see it before the merge. The header reads the cart at start, so the merge
  runs right after the sign-in. Concurrent reads share one request.
- `localStorage` is cleared only after a success; a failed merge is retried on the next read.

## Consequences

- The user is not told when a quantity was capped or a product skipped; the cart and the counter show the result.
- Accepted limitation: a lost response, or two tabs merging at once, can add the quantities twice, never above the
  stock. An `Idempotency-Key` as in [IdempotentCheckout](IdempotentCheckout.md) is not worth it: no order exists yet.
- Discontinued products are merged as in `POST /items`; the cart marks them and the checkout refuses them.
