---
type: decision
status: accepted
verified: 2026-11-18
issue: 16
---

# Keep ProductApi and CategoryApi separate

## Context

ProductApi and CategoryApi share one database and one data model, so as separate services they bring little benefit.
Merging them into one Catalog service is a common alternative.

## Decision

Keep both services separate during the cleanup and renaming. Revisit when RabbitMQ is introduced,
together with the database-per-service split.

## Consequences

- Renaming keeps two projects: `EShop.ProductApi` and `EShop.CategoryApi`.
- Revisited in #51 (below).

## Revisited in #51

The database per service split kept them separate: the client uses them independently (the home and category pages
call only `/api/categories`, products are loaded on their own, ProductApi does not join categories). Their tables stay
together in one catalog database shared by both APIs, see [DatabasePerService](DatabasePerService.md).
