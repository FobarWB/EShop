---
type: decision
status: accepted
verified: 2026-10-16
issue: 14
---

# Use PostgreSQL instead of SQL Server

## Context

The services use SQL Server through one shared DbContext. Only LocalDB is available locally, full SQL Server is heavy
to install, and its free editions have limits. Learning PostgreSQL is one of the goals of the project.

## Decision

Replace SQL Server with PostgreSQL (Npgsql EF Core provider) after the .NET 10 / EF Core 10 upgrade.

## Consequences

- Code-first: the schema comes from EF Core migrations (one `InitialCreate` replaces the SQL Server migrations);
  Northwind rows are loaded from [db/northwind-data.sql](../../db/northwind-data.sql), generated from the archived dump.
- Npgsql provider with `EFCore.NamingConventions`: tables, columns, keys and indexes are snake_case.
- Type mapping: `money` → `numeric(19,4)`, `datetime` → `timestamp without time zone`, `ntext` → `text`,
  `image` → `bytea`, `nchar` → `varchar`; `Cart.CreatedAt` is `timestamptz` (a UTC instant).
- The Northwind views and stored procedures are not ported: nothing used them. Their SQL stays in `archive/`.
- The connection string comes from configuration (user-secrets locally); the app connects as the `eshop` role,
  which owns the `eshop` database.
- PostgreSQL runs natively first and moves to Docker later.
- The last SQL Server version is tagged `sqlserver-final`; SQL Server-era code that is not ported lives in `archive/`.
