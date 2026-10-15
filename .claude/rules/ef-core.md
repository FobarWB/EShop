---
paths:
  - "**/*Context*.cs"
  - "**/Repositories/**/*.cs"
  - "**/Migrations/**/*.cs"
  - "**/EntityModels*/**/*.cs"
---

# EF Core

- Read-only queries use `AsNoTracking()`.
- Filter, project and aggregate in the database (`Where`/`Select`/`Sum` before materializing); never `ToList()` and then filter in memory.
- Project to DTOs with `Select` when only some columns are needed.
- Load related data explicitly with `Include`; avoid N+1 queries in loops. Several collection `Include`s in one query → consider `AsSplitQuery()` to avoid cartesian explosion.
- No lazy loading (no proxies package).
- Only async APIs (`ToListAsync`, `SaveChangesAsync`); do not mix sync and async calls.
- Columns used in filters and joins get an index (e.g. `Cart.UserId` is already unique-indexed); check the query plan for slow queries.
- DbContext is registered as scoped (the default). Do not use `Transient` in new code.
- Connection strings come from configuration / user-secrets, never from code.
- Model change → new migration (`dotnet ef migrations add <Name>`); never edit a migration that has been applied or merged.
- Review both `Up()` and `Down()` of a generated migration before committing and explain what SQL it will run.
