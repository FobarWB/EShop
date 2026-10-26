---
type: service
verified: 2026-10-26
---

# ClientApp

Angular 22 single-page application: catalog, product pages and the shopping cart.

- Project: [EShop.ClientApp](../../src/EShop.ClientApp/)
- Local URL: http://localhost:4200 (`npm start`); in containers http://localhost:8080 (service `client`)

## Routes

| Route | Page |
| --- | --- |
| `/` | Home |
| `/privacy` | Privacy |
| `/cart` | Shopping cart |
| `/:categoryName/c{id}` | Category page |
| `/:productName/p{id}` | Product page |

## Dependencies

Calls [ProductApi](ProductApi.md), [CategoryApi](CategoryApi.md) and [ShoppingCart](ShoppingCart.md).
The services call relative URLs (`/api/products`, `/api/categories`, `/api/carts`): `ng serve` forwards them with
[proxy.conf.json](../../src/EShop.ClientApp/proxy.conf.json) (`secure: false`: Node does not trust the Windows
certificate store), nginx with [nginx.conf](../../src/EShop.ClientApp/nginx.conf).
See [RelativeApiUrlsBehindProxy](../Decisions/RelativeApiUrlsBehindProxy.md).

## Build

- Builder `@angular/build:application` (esbuild); `ng build` writes to `dist/eshop.client-app`.
- Components set `changeDetection: ChangeDetectionStrategy.Eager` (added by the Angular 22 migration to keep
  the pre-22 behavior): the code changes state inside `subscribe` and relies on it. New components default to `OnPush`.
- `provideHttpClient(withXhr())` keeps the XHR backend; Angular 22 defaults to `fetch`.
- The `initial` budget is raised to 1 MB warning / 1.5 MB error until the bundle is reduced ([#27](https://github.com/FobarWB/EShop/issues/27));
  it is 1.14 MB after `angular-auth-oidc-client` was added (#41).
- Unit tests run on Vitest (`@angular/build:unit-test`, jsdom): `npm test`. The category page tests stub
  `ResizeObserver`: jsdom has none, and the price slider (`ngx-slider`) needs it.
- Docker image ([Dockerfile](../../src/EShop.ClientApp/Dockerfile)): built on `node:24-alpine` (`npm ci`, `npm run build`),
  served by `nginxinc/nginx-unprivileged` on port 8080 from `dist/eshop.client-app/browser`; nginx falls back to
  `index.html` for Angular routes.
- `tsconfig.json` is solution-style: it compiles nothing and references `tsconfig.app.json` and `tsconfig.spec.json`,
  so editors use the spec config (Vitest types) for `*.spec.ts`. `rootDir` is `./src`: TypeScript 6 requires it with `outDir`.

## Authentication

Sign-in with Keycloak through `angular-auth-oidc-client` (see [Authentication](../Learning/Authentication.md)).

- [auth.config.ts](../../src/EShop.ClientApp/src/app/auth.config.ts): Code + PKCE, refresh token, `secureRoutes: ['/api/']`;
  `authority` and `clientId` come from [public/config.json](../../src/EShop.ClientApp/public/config.json), loaded at start
  (another environment mounts its own file over it). `withAppInitializerAuthCheck()` finishes a sign-in returning
  with `?code=...` before the app renders.
- `AuthService` wraps the library for the components: signals `isAuthenticated`, `userName` (`preferred_username`),
  `isAdmin` (realm role `admin` from the access token), `signIn()`, `signOut()`.
- The header shows "Sign in", or the user name with "Sign out". Edit and "Add new" buttons on the home and category
  pages are shown only to admins: convenience only, the APIs refuse writes without the role.
- No route guards: there are no protected routes (the cart works for anonymous users too, edits open in modals).

## Cart page

Signed-in users use the server cart (`/api/carts/me`), anonymous users the `localStorage` cart; a 404 from the API
(no cart yet) is shown as an empty cart.
The page changes its state only after the API confirms a change (remove, quantity, clear); it does not reload.
A failed request keeps the confirmed state, restores the quantity input and shows an error message.
In the anonymous (localStorage) mode `CartService` emits once after each change, like the API calls.

## Known issues

- The anonymous cart is not moved to the server on sign-in ([#24](https://github.com/FobarWB/EShop/issues/24)).