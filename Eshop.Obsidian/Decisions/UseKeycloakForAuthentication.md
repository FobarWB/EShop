---
type: decision
status: accepted
verified: 2026-10-23
issue: 41
---

# Use Keycloak for authentication

## Context

The APIs trust a `userId` sent by the client. Several services must validate the same token; users must also be
created from code (guest checkout, #45); sign-up, password reset and email confirmation are planned (#44).
ASP.NET Core Identity's `MapIdentityApi()` issues tokens protected by Data Protection, not JWTs: other services
cannot validate them without sharing the keys. Issuing JWTs ourselves means writing a token server (signing keys,
refresh tokens, rotation, revocation): security-critical code. Considered: own IdentityApi with hand-written JWT,
OpenIddict (an OIDC server on ASP.NET Core), Keycloak.

## Decision

Keycloak in Docker is the identity provider (OpenID Connect). The APIs only validate its JWTs (`AddJwtBearer`
with the realm as `Authority`, signing keys from JWKS); the client signs in with Authorization Code + PKCE
on the Keycloak login page.

Keycloak is the source of truth for sign-in data (credentials, email, name, roles, sessions); our database holds
customer data (addresses, phone, carts, orders), keyed by the token's `sub`.

## Consequences

- No security-critical token code of our own; the APIs depend only on the OIDC standard
  (another provider means another `Authority`).
- Sign-up, password reset, email confirmation, 2FA and the account console come from Keycloak;
  the shop's profile page (#44) covers only shop data.
- Email and name are read from the token, not copied. A customer record is created on the first authenticated
  request (just-in-time); guest checkout (#45) creates the user through the Keycloak Admin API.
- The login page is Keycloak's (styled with a theme), not an Angular form.
- One more service: its own database and role in our PostgreSQL; the realm configuration lives in the repository
  and is imported at start.
- Resources: Keycloak needs about 0.5–1 GB of RAM; the server in #59 needs about 2 GB.
- Open: the Northwind `customers` table (5-character ids) vs new customer records, decided in #44.
