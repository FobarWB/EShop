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

- Existing SQL Server migrations are replaced by a fresh initial migration.
- SQL Server types (`money`, `image`, `ntext`, `nvarchar`) map to PostgreSQL types; snake_case naming.
- PostgreSQL runs natively first and moves to Docker later.
- The last SQL Server version is tagged `sqlserver-final`; SQL Server-era code that is not ported lives in `archive/`.
