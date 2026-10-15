---
type: decision
status: accepted
verified: 2026-10-15
issue: 16
---

# Keep ProductApi and CategoryApi separate for now

## Context

ProductApi and CategoryApi share one database and one data model, so as separate services they bring little benefit.
Merging them into one Catalog service is a common alternative.

## Decision

Keep both services separate during the cleanup and renaming. Revisit when RabbitMQ is introduced,
together with the database-per-service split.

## Consequences

- Renaming keeps two projects: `EShop.ProductApi` and `EShop.CategoryApi`.
- The question of a Catalog service stays open until the messaging phase.
