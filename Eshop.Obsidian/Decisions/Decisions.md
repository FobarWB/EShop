---
type: index
verified: 2026-10-27
---

# Decisions

[Home](../Home.md)

Architecture decision records: the context, the decision and its consequences. New records use `_templates/Decision.md`.

- [UsePostgreSql](UsePostgreSql.md): replace SQL Server with PostgreSQL.
- [SeparateProductAndCategoryApis](SeparateProductAndCategoryApis.md): keep ProductApi and CategoryApi separate for now.
- [TestAgainstRealPostgreSql](TestAgainstRealPostgreSql.md): integration tests use real PostgreSQL, one database per test project.
- [RelativeApiUrlsBehindProxy](RelativeApiUrlsBehindProxy.md): the client calls `/api/...`; the dev server or nginx forwards to the APIs.
- [UseKeycloakForAuthentication](UseKeycloakForAuthentication.md): Keycloak (OIDC) issues the tokens, the APIs only validate them.
- [ShoppingCartReadsCatalogData](ShoppingCartReadsCatalogData.md): the cart returns product details with the current price,
  read from the shared `products` and `categories` tables.
- [PagedListResponses](PagedListResponses.md): lists are paged with `{ items, page, pageSize, totalCount }` and offset paging.
- [OptimisticConcurrencyWithXmin](OptimisticConcurrencyWithXmin.md): products carry PostgreSQL's `xmin` as a concurrency
  token; a concurrent change becomes 409 instead of a lost update.
- [IdempotentCheckout](IdempotentCheckout.md): the client sends one `Idempotency-Key` per checkout attempt; a retry
  returns the same order.
