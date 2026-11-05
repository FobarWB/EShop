---
type: index
verified: 2026-11-05
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
- [OrderLifecycle](OrderLifecycle.md): order statuses with transitions in one place, stored as text with a CHECK,
  an append-only status journal; cancelling returns the stock in the same transaction.
- [TransactionalOutbox](TransactionalOutbox.md): events are stored with the change in one transaction and published by a
  polling publisher; consumers keep an inbox (at least once, idempotent consumers).
- [UseRabbitMqClient](UseRabbitMqClient.md): `RabbitMQ.Client` with a thin shared layer instead of MassTransit.
- [MessageContractsOwnedByProducer](MessageContractsOwnedByProducer.md): each producer owns a contracts project;
  fields are only added.
- [DelayedRetryWithTtlQueues](DelayedRetryWithTtlQueues.md): retries through wait queues with a TTL that dead-letter
  back to the work queue; then a dead letter queue.
- [AccountLifecycleInKeycloak](AccountLifecycleInKeycloak.md): sign-up, email confirmation, password reset and the
  password policy are realm settings; the password is set after the email is confirmed.
- [AnonymousCartMerge](AnonymousCartMerge.md): the anonymous cart moves to the server in one transaction, capped by
  the stock, when the signed-in client first reads the cart.
