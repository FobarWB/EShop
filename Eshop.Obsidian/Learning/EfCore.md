---
type: topic
verified: 2026-11-02
issue: 33
---

# EfCore

Related: [Services](../Services/Services.md), [PostgreSql](PostgreSql.md), [Testing](Testing.md).
Rules for EF Core code: [.claude/rules/ef-core.md](../../.claude/rules/ef-core.md).

## Key ideas

- **`Update()` attaches the whole graph.** `DbContext.Update(entity)` tracks every reachable object: a related entity
  with a set generated key becomes `Modified` (an `UPDATE` of all its columns), one without a key `Added` (an `INSERT`),
  one without a generated key (composite `cart_items`) always `Modified`. Binding an entity from a request and calling
  `Update()` therefore lets a client write related rows (overposting / mass assignment).
- **Change tracking writes only what changed.** Load the entity in the same context (`FindAsync`), change properties,
  `SaveChangesAsync`: the `UPDATE` lists only the changed columns, and a removed collection item becomes a `DELETE`.
  `Update()` on an already tracked entity marks every column modified for nothing.
- **Affected rows are not success.** `SaveChangesAsync()` returns the number of written rows; a failed write throws.
  Clearing an empty cart or setting the same quantity writes 0 rows and is still a success, so `> 0` is the wrong test.
- **Projections.** `Select(x => new Dto(...))` before `ToListAsync` becomes a `SELECT` of only those columns, with JOINs
  for navigations (`i.Product.Category.CategoryName`, a LEFT JOIN when optional) and nested collections, in one query,
  without `Include` and without tracking. A projection must be an expression: a C# method called inside `Select` is run
  after loading, so EF reads whole rows. Here the projection is an extension of `IQueryable<T>` (`ToResponse()`).
- **Order before projecting.** `OrderBy` on a property of a record built by its constructor cannot be translated;
  sort the entity query, then project.
- **Cancellation.** Async methods take a `CancellationToken`; Npgsql then cancels the running statement on the server.
- **Interceptors.** `SaveChangesInterceptor.SavingChangesAsync` runs before the commands are sent; added per test with
  `services.ConfigureDbContext<TContext>(o => o.AddInterceptors(...))` (EF Core 9+).
- **Concurrency token.** A property marked `[Timestamp]` / `IsConcurrencyToken()` is added to the `WHERE` of UPDATE and
  DELETE with the value read; no matched row means `DbUpdateConcurrencyException` (`DbUpdateException` without a
  database error inside). In PostgreSQL the token is the system column `xmin` (`uint`), read back after each write.
- **One SaveChanges, one transaction.** Inserts, updates and deletes of one `SaveChangesAsync` run in one implicit
  transaction: any failure leaves the database as it was, no explicit `BeginTransaction` needed.
- **`ExecuteUpdateAsync` bypasses the change tracker** and runs at once, outside `SaveChanges`: one `UPDATE` for many
  rows, with expressions over the row (`x => x.Stock + …`). Together with `SaveChanges` it needs an explicit
  `BeginTransactionAsync` to be atomic.
- **Enums as text.** `HasConversion<string>()` stores the name; a private setter (`get; private set;`) is still mapped,
  so the entity can guard its own state.
- **Generated migrations are a draft.** Read the SQL (`dotnet ef migrations script <from> <to>`) before applying: EF does
  not know how to fill a new NOT NULL column or convert existing values.

## Applied in this project

- DTOs and mappings per API (`Dtos/*Mappings.cs`), repositories with `FindAsync` + `ApplyTo()` + `SaveChangesAsync`;
  `Create` reads the new row back through the same projection instead of keeping a second (compiled) mapping (#33).
- `ToPageAsync()` in `EShop.ServiceDefaults`: `CountAsync`, then `Skip`/`Take` on an ordered query.
- EF Core wraps database errors in `DbUpdateException`; the PostgreSQL error is its `InnerException`
  (`PostgresException.SqlState`), see `ForeignKeyViolationHandler`.
- `Product.Version` → `xmin`; `ConcurrencyConflictHandler` maps the exception to 409; the checkout writes order, stock
  and cart in one `SaveChangesAsync` (#42, [OptimisticConcurrencyWithXmin](../Decisions/OptimisticConcurrencyWithXmin.md)).
- `Order.Version` → `xmin` too; the cancel saves the order, then `ExecuteUpdateAsync`s the stock, in one explicit
  transaction (#43, [OrderLifecycle](../Decisions/OrderLifecycle.md)).

## Outbox and background services (#50)

- **`SaveChanges` inside an explicit transaction uses a savepoint.** On a failure EF rolls back to the savepoint, so
  the transaction stays usable: after a unique violation (23505) the checkout still reads the concurrent order in the
  same transaction. Expected "current transaction is aborted" (25P02) was wrong; the test showed it.
- **Change tracker vs direct SQL.** `Add`/property changes are only tracked until `SaveChanges`; `ExecuteUpdate`
  runs SQL at once and saves nothing tracked. An entity added after the last `SaveChanges` is never stored.
- **Ids from the database** (identity) exist only after `SaveChanges`: the checkout saves twice in one transaction
  because the event needs the order id. Ids generated in code (`Guid.CreateVersion7()`) are known before the insert;
  Npgsql 9+ also generates UUIDv7 for `Guid` keys by default.
- **`FromSql($"… {value} …")`** turns the interpolated values into SQL parameters (`LIMIT @p0`): no injection. Used for
  `FOR UPDATE SKIP LOCKED`, which LINQ cannot express.
- **Captive dependency.** A hosted service is a singleton; `EShopDbContext` is scoped. Injected into the constructor, one
  DbContext would live for hours (growing change tracker, broken after a lost connection), and DI validation refuses it
  in Development. Background work creates a scope per pass with `IServiceScopeFactory`.
- **Composite key by attribute**: `[PrimaryKey(nameof(Consumer), nameof(MessageId))]` (`InboxMessage`); a filtered
  index needs the fluent API (`HasIndex(…).HasFilter("processed_at IS NULL")`, raw SQL names in snake_case).

## Open questions

- Two requests that load and save the same cart can overwrite each other's quantity (last write wins); products have
  an `xmin` token since #42, carts and cart items do not.
