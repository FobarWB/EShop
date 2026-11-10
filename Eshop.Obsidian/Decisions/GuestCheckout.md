---
type: decision
status: accepted
verified: 2026-11-10
issue: 45
---

# GuestCheckout

## Context

A visitor who is not signed in should be able to order with an email, and get an account for it. Keycloak owns
accounts ([UseKeycloakForAuthentication](UseKeycloakForAuthentication.md)); OrderApi owns orders, CustomerApi customer
data. Keycloak cannot join a database transaction. The issue asked not to reveal whether an email has an account.

## Decision

- `POST /api/orders/guest` (anonymous): email, address, the browser cart's items. OrderApi asks Keycloak whether the
  email has an account (service account `eshop-orderapi`, `view-users` only): yes → 409 `account-exists`, the visitor
  signs in and the cart moves to the server (#24). No → the order is stored with `user_id` null, `placed_as_guest`,
  and `order.guest-placed` in the outbox.
- Account creation runs through RabbitMQ (choreography): CustomerApi consumes `order.guest-placed`, finds or creates the
  Keycloak user without a password (`eshop-customerapi`: `view-users`, `manage-users`), sends Keycloak's
  execute-actions email (`VERIFY_EMAIL`, `UPDATE_PASSWORD`, 24 h) while the user has no password, and publishes
  `customer.guest-account-ready`. OrderApi consumes it and attaches the email's guest orders without an owner.
- Revealing a registered email is accepted: the sign-up form does it already
  ([AccountLifecycleInKeycloak](AccountLifecycleInKeycloak.md)), and an order never lands in an account its buyer cannot
  open. A rate limit per address (5/min, `ForwardedHeaders` behind nginx) limits guessing and flooding.
- Retries: a guest's `Idempotency-Key` is unique among guest orders (`ix_orders_guest_idempotency_key … WHERE
  placed_as_guest`), checked with the email, and still found after the order was attached.
- One Keycloak client per API, with its own settings section and secret (`KeycloakAdmin:OrderApi`,
  `KeycloakAdmin:CustomerApi`): the APIs share one user-secrets store.

## Consequences

- The checkout depends on Keycloak (503 while it is down, nothing stored); the account creation does not: it is retried
  (5 s, 30 s, 2 min), then the DLQ. Until then the order exists without an account.
- Up to three steps for a guest: the order confirmation, Keycloak's link, and at the first sign-in a form for first and
  last name (required in the realm's user profile). `VERIFY_EMAIL` in the link marks the email verified: with
  `UPDATE_PASSWORD` alone Keycloak sent a second "Verify email" at the first sign-in (checked on compose).
- A crash between Keycloak's email and the commit sends the email again; a duplicate beats a guest without an account.
- The outbox table is shared by producers: `outbox_messages.producer` keeps each publisher to its own rows and order.
- Open: Keycloak's email texts are its defaults ("Update Your Account"); a theme can change them (#59).
