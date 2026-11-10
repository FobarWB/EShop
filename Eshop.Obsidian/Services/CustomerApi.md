---
type: service
verified: 2026-11-10
issue: 44
---

# CustomerApi

REST API for the signed-in user's shop data: phone and saved shipping addresses. Email and name belong to Keycloak
(see [AccountLifecycleInKeycloak](../Decisions/AccountLifecycleInKeycloak.md)). Since #45 it also creates the Keycloak
accounts of guest orders, as a consumer of RabbitMQ (see [Guest accounts](#guest-accounts)).

- Project: [EShop.CustomerApi](../../src/EShop.CustomerApi/)
- Local URL: https://localhost:5006 (API reference: `/scalar`, OpenAPI document: `/openapi/v1.json`)
- Container: service `customerapi`, through nginx at http://localhost:8080/api/customers ([Dockerfile](../../src/EShop.CustomerApi/Dockerfile))

## Endpoints (`api/customers/me`)

Every endpoint needs a signed-in user and works on that user's data: the id is the token's `sub`, never a request value.
401 without a token. Errors are ProblemDetails.

| Method | Route | Action |
| --- | --- | --- |
| GET | `/` | `CustomerProfileResponse`: `email`, `firstName`, `lastName` from the token, `phone` stored; never creates the profile |
| PUT | `/` | `{ "phone": "+49 30 1234567" }`, null or empty removes it; 200 with the profile, 400 for an invalid phone |
| GET | `/addresses` | The user's addresses, default first, then newest (at most 10) |
| GET | `/addresses/{id}` | One address; 404 for a missing one and for another user's |
| POST | `/addresses` | `AddressRequest`; 201 with `Location`. The first address becomes the default. 409 at 10 addresses |
| PUT | `/addresses/{id}` | 200 with the address, 404, 409 when a concurrent request changed the default |
| DELETE | `/addresses/{id}` | 204, 404. Deleting the default leaves no default |

## Contract

- `AddressRequest` / `AddressResponse`: `recipientName` (40), `address` (60), `city` (15), `region` (optional, 15),
  `postalCode` (10), `country` (15), `isDefault`: the lengths of `PlaceOrderRequest`, so a saved address always fits a checkout.
- Phone: digits, spaces, parentheses and hyphens with an optional leading `+`, 5–24 characters; not a full number check.

## Data

`customer_profiles` (`user_id` = `sub` as primary key, `phone`, `created_at`, `updated_at`) and `customer_addresses`
(`address_id`, `user_id` with a cascading foreign key to the profile, the address, `is_default`, `created_at`).
The Northwind `customers` table (5-character ids, required `company_name`) is not used.

- The profile is created just-in-time on the user's first write (PUT of the profile, first address). Two concurrent
  first writes: the second insert fails on `pk_customer_profiles` (23505) and reads the row the first committed.
- At most one default address per user: partial unique index `ix_customer_addresses_user_id_default … WHERE is_default`.
  A new default first clears the previous one (`ExecuteUpdate`), then saves, in one transaction. A concurrent request that
  set another default commits first: this save fails with 23505 → 409, the transaction rolls back, nothing changes.
- `ix_customer_addresses_user_id` serves the address list. Both indexes have explicit names: `HasIndex` without a name
  returns the same index for the same column, and the snake case convention would name the second one `…_user_id1`.

## Guest accounts

See [GuestCheckout](../Decisions/GuestCheckout.md). `GuestOrderPlacedHandler` (folder `GuestOrders/`) on the queue
`customers.guest-orders` (consumer `customers`, `order.guest-placed` from [OrderApi](OrderApi.md), retries 5 s / 30 s /
2 min, then the DLQ), through the shared `MessageConsumer` of `EShop.Messaging`:

1. `KeycloakAccountProvisioner.FindOrCreateAsync` (Admin API as `eshop-customerapi`: `view-users`, `manage-users`):
   `GET users?email=…&exact=true`; none → `POST users` (`username` = `email`, `enabled`, `emailVerified: false`, no
   password), the id from `Location`; 409 (a parallel delivery created it) → search again.
2. `NeedsPassword`: the user has no credential of type `password`. Not "just created": a retry after a failed email
   sends it again.
3. `SendSetPasswordEmailAsync`: `PUT users/{id}/execute-actions-email` with `VERIFY_EMAIL` and `UPDATE_PASSWORD`,
   `client_id` and `redirect_uri` from `GuestAccounts:*` (`eshop-client`, `http://localhost:8080/`), lifespan 24 h.
4. `customer.guest-account-ready` (`GuestAccountReady(Email, UserId)`, `EShop.CustomerApi.Contracts`) into the outbox
   (producer `customerapi`), saved by the consumer with the inbox row; this service's `OutboxPublisher` sends it.

Keycloak cannot join the transaction: a crash after the email and before the commit sends the email again.

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); JWT validation, `User.GetUserId()`, `GetUserEmail()`,
`GetGivenName()`, `GetFamilyName()`, error handling and the Keycloak Admin API client (section
`KeycloakAdmin:CustomerApi`, secret in user-secrets and `CUSTOMERAPI_KEYCLOAK_SECRET`) from `EShop.ServiceDefaults`;
outbox, consumer and RabbitMQ from `EShop.Messaging` (in compose it waits for a healthy RabbitMQ); contracts of
`EShop.OrderApi.Contracts` and `EShop.CustomerApi.Contracts`. Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.CustomerApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.CustomerApi.Tests.Integration/)
(repository and API, database `eshop_test_customerapi`) tests. Races are played with `BeforeSaveChangesInterceptor`:
a concurrent first profile insert, and a concurrent new default address between this request's UPDATE and its save
(the user has no default before, so the UPDATE locks no row and the concurrent insert does not wait for it).
Guest accounts: `KeycloakAccountProvisioner` against a Keycloak container that imports the realm file (new email,
the same email again, concurrently, a user with a password, and `eshop-orderapi` getting 403 for a new user);
`GuestOrderPlacedHandler` with a fake provisioner. The set-password email needs SMTP and was checked by hand on compose.

## Known issues

- The limit of 10 addresses is checked with a COUNT before the insert: two concurrent inserts can make 11.
- Email and name are changed in the Keycloak account console (http://localhost:8180/realms/eshop/account), not here.
