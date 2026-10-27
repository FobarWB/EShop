---
type: service
verified: 2026-10-27
---

# ClientApp

Angular 22 single-page application: catalog, product pages, the shopping cart and the checkout.

- Project: [EShop.ClientApp](../../src/EShop.ClientApp/)
- Local URL: http://localhost:4200 (`npm start`); in containers http://localhost:8080 (service `client`)

## Routes

| Route | Page |
| --- | --- |
| `/` | Home |
| `/privacy` | Privacy |
| `/cart` | Shopping cart |
| `/checkout` | Checkout (sign-in required) |
| `/orders/:id` | Order confirmation (sign-in required) |
| `/:categoryName/c{id}` | Category page |
| `/:productName/p{id}` | Product page |

## Dependencies

Calls [ProductApi](ProductApi.md), [CategoryApi](CategoryApi.md), [ShoppingCart](ShoppingCart.md) and [OrderApi](OrderApi.md).
The services call relative URLs (`/api/products`, `/api/categories`, `/api/carts`, `/api/orders`): `ng serve` forwards them with
[proxy.conf.json](../../src/EShop.ClientApp/proxy.conf.json) (`secure: false`: Node does not trust the Windows
certificate store), nginx with [nginx.conf](../../src/EShop.ClientApp/nginx.conf).
See [RelativeApiUrlsBehindProxy](../Decisions/RelativeApiUrlsBehindProxy.md).

## Build

- Builder `@angular/build:application` (esbuild); `ng build` writes to `dist/eshop.client-app`.
- Components set `changeDetection: ChangeDetectionStrategy.Eager` (added by the Angular 22 migration to keep
  the pre-22 behavior): the code changes state inside `subscribe` and relies on it. New components default to `OnPush`.
- `provideHttpClient(withXhr())` keeps the XHR backend; Angular 22 defaults to `fetch`.
- The `initial` budget is raised to 1 MB warning / 1.5 MB error until the bundle is reduced ([#27](https://github.com/FobarWB/EShop/issues/27));
  it is 1.14 MB after `angular-auth-oidc-client` was added (#41) and 1.16 MB after the checkout pages (#42).
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
- `/checkout` and `/orders/:id` use the library's `autoLoginPartialRoutesGuard`: an anonymous visitor is sent to
  Keycloak and brought back to the requested route after the sign-in (checked by hand). Other pages need no sign-in
  (the cart works for anonymous users too, edits open in modals).

## Lists

The APIs return pages (`IPage<T>`: `items`, `page`, `pageSize`, `totalCount`). The home page and the category page
ask for the largest page (`maxPageSize = 100`): the category page filters, sorts and computes the price range in the
browser, so it needs the whole category in one page (Northwind has at most 13 products per category). When a category
does not fit, the page says "Showing the first N of M products". Server-side filters, sorting and paging:
[#61](https://github.com/FobarWB/EShop/issues/61).

## Cart page

`CartService` is the one cart for the components: signed-in users use the server cart (`/api/carts/me`), anonymous
users `LocalCartService` (`localStorage`). Both return the items with product name, category name and current price,
so `CartComponent` makes no request per item (#33).

- Server cart: the API returns the details (one SQL query); a 404 (no cart yet) is shown as an empty cart.
- Anonymous cart: `localStorage` keeps only `productId` and `quantity`, so prices never go stale; `LocalCartService`
  loads each product and its category when the cart is read. A product deleted since it was added drops out of the cart;
  carts stored in the older format (with `cartId`) are still read.

The page changes its state only after a change is confirmed (remove, quantity, clear); it does not reload.
A failed request keeps the confirmed state, restores the quantity input and shows an error message.
In the anonymous mode `CartService` emits once after each change, like the API calls.

- Each item shows a mark when it cannot be ordered as it is (`cartItemProblem()` in `cart-item.model.ts`, the same
  rules as OrderApi): "No longer available" (discontinued or no price; quantity controls disabled) or "Only N left" /
  "Out of stock" ("+" stops at the stock). CHECKOUT is disabled for an empty cart and while an item is marked.

## Checkout

- `/checkout` shows the cart summary (items, shipping 5, total) with the same marks and a reactive address form
  (the lengths of OrderApi). "Place order" is disabled while the form is invalid, an item is marked or a request is pending.
- One `crypto.randomUUID()` per page visit is the `Idempotency-Key` of every try, so a retry after an error cannot
  place a second order ([IdempotentCheckout](../Decisions/IdempotentCheckout.md)).
- 409: a message, the cart is reloaded (current stock), and the items named in `productIds` are marked even if the
  reloaded cart looks fine. 400 (empty cart): a message and a reload.
- Success: the API has emptied the cart; the client opens `/orders/:id`, which loads the order by id (works after a reload).

## Known issues

- The anonymous cart is not moved to the server on sign-in ([#24](https://github.com/FobarWB/EShop/issues/24)):
  an anonymous visitor who signs in for the checkout finds an empty server cart.
- The category page filters and sorts in the browser (see [Lists](#lists), #61).