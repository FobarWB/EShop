---
type: service
verified: 2026-10-20
---

# ClientApp

Angular 22 single-page application: catalog, product pages and the shopping cart.

- Project: [EShop.ClientApp](../../src/EShop.ClientApp/)
- Local URL: http://localhost:4200 (`npm start`)

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
API base URLs are hardcoded in `src/app/services/*.service.ts`.

## Build

- Builder `@angular/build:application` (esbuild); `ng build` writes to `dist/eshop.client-app`.
- Components set `changeDetection: ChangeDetectionStrategy.Eager` (added by the Angular 22 migration to keep
  the pre-22 behavior): the code changes state inside `subscribe` and relies on it. New components default to `OnPush`.
- `provideHttpClient(withXhr())` keeps the XHR backend; Angular 22 defaults to `fetch`.
- The `initial` budget is raised to 1 MB warning / 1.5 MB error until the bundle is reduced ([#27](https://github.com/FobarWB/EShop/issues/27)).
- Unit tests run on Vitest (`@angular/build:unit-test`, jsdom): `npm test`.

## Known issues

- The cart uses the API when the user is "authenticated" and `localStorage` otherwise; the flag is currently always `true` ([#24](https://github.com/FobarWB/EShop/issues/24)).