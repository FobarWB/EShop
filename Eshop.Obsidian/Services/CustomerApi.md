---
type: service
verified: 2026-11-03
issue: 44
---

# CustomerApi

REST API for the signed-in user's shop data: phone and saved shipping addresses. Email and name belong to Keycloak
(see [AccountLifecycleInKeycloak](../Decisions/AccountLifecycleInKeycloak.md)).

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

## Dependencies

Shared DbContext (`EShopDbContext` in `EShop.Data`); JWT validation, `User.GetUserId()`, `GetUserEmail()`,
`GetGivenName()`, `GetFamilyName()` and error handling from `EShop.ServiceDefaults`. Called by [ClientApp](ClientApp.md).

## Tests

[Unit](../../tests/EShop.CustomerApi.Tests.Unit/) (controller) and [integration](../../tests/EShop.CustomerApi.Tests.Integration/)
(repository and API, database `eshop_test_customerapi`) tests. Races are played with `BeforeSaveChangesInterceptor`:
a concurrent first profile insert, and a concurrent new default address between this request's UPDATE and its save
(the user has no default before, so the UPDATE locks no row and the concurrent insert does not wait for it).

## Known issues

- The limit of 10 addresses is checked with a COUNT before the insert: two concurrent inserts can make 11.
- Email and name are changed in the Keycloak account console (http://localhost:8180/realms/eshop/account), not here.
