---
type: service
verified: 2026-10-22
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
- The `initial` budget is raised to 1 MB warning / 1.5 MB error until the bundle is reduced ([#27](https://github.com/FobarWB/EShop/issues/27)).
- Unit tests run on Vitest (`@angular/build:unit-test`, jsdom): `npm test`.
- Docker image ([Dockerfile](../../src/EShop.ClientApp/Dockerfile)): built on `node:24-alpine` (`npm ci`, `npm run build`),
  served by `nginxinc/nginx-unprivileged` on port 8080 from `dist/eshop.client-app/browser`; nginx falls back to
  `index.html` for Angular routes.
- `tsconfig.json` is solution-style: it compiles nothing and references `tsconfig.app.json` and `tsconfig.spec.json`,
  so editors use the spec config (Vitest types) for `*.spec.ts`. `rootDir` is `./src`: TypeScript 6 requires it with `outDir`.

## Cart page

The page changes its state only after the API confirms a change (remove, quantity, clear); it does not reload.
A failed request keeps the confirmed state, restores the quantity input and shows an error message.
In the anonymous (localStorage) mode `CartService` emits once after each change, like the API calls.

## Known issues

- The cart uses the API when the user is "authenticated" and `localStorage` otherwise; the flag is currently always `true` ([#24](https://github.com/FobarWB/EShop/issues/24)).