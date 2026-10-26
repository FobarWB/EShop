---
type: topic
verified: 2026-10-26
issue: 41
---

# Authentication

Related: [UseKeycloakForAuthentication](../Decisions/UseKeycloakForAuthentication.md), [ClientApp](../Services/ClientApp.md),
[ShoppingCart](../Services/ShoppingCart.md), [Docker](Docker.md).

## Key ideas

- **OpenID Connect splits the roles.** The identity provider (Keycloak) checks the password and issues tokens;
  the APIs only validate them; the browser app never sees a password.
- **Authorization Code flow with PKCE.** The app sends the browser to the provider's login page; the provider returns
  a one-time `?code=...`, which the app exchanges for tokens. PKCE (a random secret whose hash goes with the first
  request) makes a stolen code useless; a public browser client has no client secret.
- **Three tokens.** Access token (JWT, 5 min here): sent to the APIs as `Authorization: Bearer`. ID token: who signed in,
  for the app itself. Refresh token (30 min of inactivity here): gets a new access token without the login page.
  Tokens are credentials until they expire: do not paste them into chats or logs; decode only the payload.
- **Validating a JWT needs no call per request.** The API downloads the provider's metadata
  (`/.well-known/openid-configuration`) and signing keys (JWKS) once, then checks signature, issuer (`iss`),
  audience (`aud`) and expiry locally.
- **Hiding a button is not protection.** The client hides admin actions for convenience; the APIs refuse them (403).

## Applied in this project

- Keycloak 26 in compose (http://localhost:8180, `start-dev`), its own database `keycloak` in our PostgreSQL.
  The realm `eshop` is imported from [eshop-realm.json](../../keycloak/realms/eshop-realm.json) on the first start only;
  later changes made in the admin console stay in the database.
- Realm contents: role `admin`; client `eshop-client` (public, Code + PKCE, redirect URIs for :4200 and :8080,
  audience mapper `eshop-api`); client `eshop-dev` (password grant, tokens from the command line);
  development users `admin`/`admin` and `customer`/`customer`. Development only: the server realm comes in #59.
- Two different admins: the Keycloak administrator (master realm, admin console, password in `.env`) and the shop user
  `admin` (realm `eshop`, role `admin`).
- APIs: [EShopAuthenticationExtensions](../../src/EShop.ServiceDefaults/EShopAuthenticationExtensions.cs) in
  `EShop.ServiceDefaults`. `MapInboundClaims = false` keeps JWT claim names (`sub`, not a long .NET claim type);
  Keycloak's `realm_access.roles` is copied into one `role` claim per role, so `[Authorize(Roles = "admin")]` works;
  a fallback policy requires a signed-in user on every endpoint not marked `[AllowAnonymous]`.
- Access: product and category reads are public, writes need `admin`; every cart endpoint needs a signed-in user and
  works on the cart of the token's `sub` (`/api/carts/me`).
- Client: `angular-auth-oidc-client` (certified, provider-neutral). `authority` and `clientId` are read from
  `/config.json` at start, so one build runs everywhere; the interceptor adds the token only to `/api/` requests;
  the token is renewed 30 s before it expires (verified: a new token 273 s after sign-in, `jti` prefixed `onrtrt:`).
- API tests do not need Keycloak: `EShopApiFactory` replaces JwtBearer with `TestAuthHandler`
  (user and roles from the `X-Test-User` / `X-Test-Roles` headers, see [Testing](Testing.md)).
- A token for manual API calls (development only):
  `curl -d "grant_type=password&client_id=eshop-dev&username=customer&password=customer" http://localhost:8180/realms/eshop/protocol/openid-connect/token`.

## Debugging lessons

- **"The issuer is invalid" can mean "the metadata was not downloaded".** In a container `localhost:8180` is the
  container itself (connection refused); JwtBearer could not read the metadata and reported the issuer.
  Fix: `Authentication__MetadataAddress=http://keycloak:8080/...` for the APIs in compose, while the issuer stays
  `http://localhost:8180` (`KC_HOSTNAME`; `KC_HOSTNAME_BACKCHANNEL_DYNAMIC` lets the key and token URLs follow the
  internal address).
- **Seconds of delay from the IDE on Windows** were connections, not queries: see `::1` in [Docker](Docker.md).

## Open questions

- Production realm, Keycloak production mode and public URLs (#59).
- Moving the anonymous cart to the server on sign-in (#24); customer records on first sign-in (#44).
