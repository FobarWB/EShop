# EShop

Learning pet project: an online shop built from small ASP.NET Core Web API services and an Angular client,
on top of the Northwind sample database. The owner uses it to learn PostgreSQL, Docker and RabbitMQ:
explain the reasoning behind a change, not only the change itself.

Roadmap: issue #17. Planning: GitHub Project "Eshop" (owner `FobarWB`).

## Repository map

| Path | Purpose | Local URL |
|---|---|---|
| `src/EShop.ProductApi/` | Products REST API; publishes `product.changed` / `product.deleted`; reserves and returns the stock of orders (consumes `order.placed` / `order.cancelled`) | https://localhost:5002 |
| `src/EShop.CategoryApi/` | Categories REST API | https://localhost:5003 |
| `src/EShop.ShoppingCart/` | Shopping cart REST API; keeps a product replica (`cart_products`) fed by product events; takes confirmed orders out of the cart (`order.confirmed`) | https://localhost:5004 |
| `src/EShop.OrderApi/` | Orders REST API: checkout with the client's lines priced from its product replica (`order_products`), guest checkout with an email; confirms or rejects orders on the stock answer; consumes `customer.guest-account-ready` | https://localhost:5005 |
| `src/EShop.CustomerApi/` | Customer REST API: the signed-in user's phone and saved shipping addresses; creates the Keycloak accounts of guest orders (consumes `order.guest-placed`) | https://localhost:5006 |
| `src/EShop.ClientApp/` | Angular 22 SPA; `nginx.conf` and `Dockerfile` of the client container | http://localhost:4200 (`ng serve`), http://localhost:8080 (compose profile `app`) |
| `src/EShop.Catalog.Data/` | The catalog's EF Core `CatalogDbContext` (schema `catalog`), its entities and migrations: one bounded context, used by ProductApi and CategoryApi | — |
| `src/EShop.Persistence/` | The EF Core setup of a service's DbContext: `AddServiceDbContext<TContext>()`, `UseServiceNpgsql` (its schema, migrations history and search path), `IServiceDbContext` | — |
| `src/EShop.NotificationService/` | Worker: consumes order events from RabbitMQ, sends emails through Mailpit (SMTP localhost:1025); `Dockerfile` of the worker container | — |
| `src/EShop.Messaging/` | Shared event infrastructure for all services: publishing through the transactional outbox (one producer name per service), consuming with inbox, retries and DLQ (`MessageConsumer` + one `IMessageHandler` per service), RabbitMQ connection and topology | — |
| `src/EShop.OrderApi.Contracts/` | Events published by OrderApi (`OrderPlaced`, `OrderConfirmed`, `OrderRejected`, `OrderCancelled`, `GuestOrderPlaced`): the contract its consumers deserialize | — |
| `src/EShop.CustomerApi.Contracts/` | Events published by CustomerApi (`GuestAccountReady`) | — |
| `src/EShop.ProductApi.Contracts/` | Events published by ProductApi (`ProductChanged`, `ProductDeleted`, `StockReserved`, `StockRejected`) | — |
| `src/EShop.ServiceDefaults/` | Shared API setup: JWT validation `AddEShopAuthentication()`, `User.GetUserId()` (token `sub`), Keycloak Admin API client `AddKeycloakAdminClient()` (service account, one settings section per API) | — |
| `tests/` | xUnit unit and integration tests per service, shared helpers `EShop.Tests.Common`, `coverage.ps1` | — |
| `docker-compose.yml` | Local environment: PostgreSQL 18, pgAdmin, Keycloak, RabbitMQ and Mailpit; with the profile `app` also the migrator, the five APIs, the notification service and the client (settings in `.env`, template `.env.example`) | pgAdmin: http://localhost:5050, Keycloak: http://localhost:8180, RabbitMQ: http://localhost:15672, Mailpit: http://localhost:8025 |
| `keycloak/realms/` | Realm `eshop`, imported on Keycloak's first start: clients, role `admin`, development users `admin`/`admin1234` and `customer`/`customer1234` (local only, never on a server) | — |
| `db/` | Northwind data for PostgreSQL, one file per schema (`northwind-catalog.sql`, `northwind-ordering.sql`), and their generator `convert-northwind.ps1`; the migrator `db/migrator/`; container init script `db/init/`, pgAdmin server list `db/pgadmin/` | — |
| `archive/` | **Archived** SQL Server-era code: `Northwind.Mvc` (not in `EShop.sln`, not built) and `Northwind4SQLServer.sql`. Do not change; see `archive/README.md`. | — |
| `Eshop.Obsidian/` | Obsidian knowledge vault: service notes, decisions, learning notes | — |

- One database, a schema per service (`catalog`, `cart`, `ordering`, `customer`, `notification`), each with its own
  DbContext and migrations (#77); separate databases follow in #78. A service's DbContext, entities and migrations
  live in its `Data/` folder, the catalog's in `src/EShop.Catalog.Data/`.
- The Angular services call relative URLs (`/api/products`); the server that delivered the page forwards them:
  `ng serve` through `src/EShop.ClientApp/proxy.conf.json`, the client container through `nginx.conf`.
- API ports are set by `applicationUrl` in each project's `Properties/launchSettings.json` and duplicated
  in `proxy.conf.json`. Change both sides together. A new route prefix (`/api/orders`) needs an entry in
  `proxy.conf.json` and in `nginx.conf`.
- In containers the APIs listen on HTTP port 8080 inside the compose network and are not published to the host:
  only nginx (`client`) is. Each API maps `GET /health` (database connectivity) for the container health checks.
- CORS origins come from `Cors:AllowedOrigins` in each API's `appsettings.json` (`http://localhost:4200`);
  our client is same-origin through the proxies and does not need it.
- Authentication: Keycloak issues the tokens, the APIs validate them (`Authentication:Authority`). Product and category
  reads are public, writes need the realm role `admin`; every cart, order and customer endpoint needs a signed-in user (`/api/carts/me`, `/api/orders`, `/api/customers/me`),
  except the guest checkout `POST /api/orders/guest` (anonymous, rate-limited).
  OrderApi and CustomerApi also call Keycloak's Admin API as service accounts (`eshop-orderapi`, `eshop-customerapi`).
  The client reads `authority` and `clientId` from `src/EShop.ClientApp/public/config.json` at start.
- In containers the APIs read the Keycloak metadata from `keycloak:8080` (`Authentication__MetadataAddress`);
  the token issuer stays `http://localhost:8180`.

### Project status

GitHub is the source of truth for progress; do not copy task status into files.
- Roadmap: `gh issue view 17`; open work: `gh issue list`; board: `gh project item-list 1 --owner FobarWB`.
- What is merged: `git log --oneline master`.

## Environment

- Windows 11, PowerShell 5.1 (no `&&`); Git Bash is also available.
- .NET SDK 10 (pinned by `global.json`; `net10.0` is set for all projects in `Directory.Build.props`);
  Node 24, npm 11; `gh` CLI; `csharpier`.
- Package versions live only in `Directory.Packages.props` (central package management);
  `PackageReference` items have no `Version`. `nuget.config` restricts package sources to nuget.org.
- Local .NET tools (ReportGenerator, dotnet-ef) are pinned in `dotnet-tools.json`: `dotnet tool restore`.
  The dotnet-ef version matches the EF Core packages; the migrator image restores it too.
- .NET tests run on Microsoft.Testing.Platform (`global.json`), not VSTest.
- Docker Desktop (WSL2 backend), installed per user in `%LOCALAPPDATA%\Programs\DockerDesktop`.
- Database: PostgreSQL 18 in Docker (`docker-compose.yml`, see Database). The Windows service `postgresql-x64-18`
  is stopped (startup type Manual); its client tools (`psql`, `pg_dump`) in `C:\Program Files\PostgreSQL\18\bin`
  (not on `PATH`) still work against the container.
- The APIs read `ConnectionStrings:EShop` from configuration. Locally it comes from user-secrets
  (`UserSecretsId` `eshop`, shared by the five APIs and NotificationService); it is never committed.
  The same user-secrets hold `RabbitMq:Password` (the value of `RABBITMQ_PASSWORD` in `.env`) for ProductApi, ShoppingCart, OrderApi, CustomerApi and NotificationService,
  and the Keycloak client secrets `KeycloakAdmin:OrderApi:ClientSecret` and `KeycloakAdmin:CustomerApi:ClientSecret`
  (the values of `ORDERAPI_KEYCLOAK_SECRET` and `CUSTOMERAPI_KEYCLOAK_SECRET` in `.env`; Keycloak reads them on realm import).
- Windows PowerShell may block `.ps1` scripts (execution policy): use `npm.cmd` or
  `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.

## Commands

The APIs need the database: start Docker Desktop and `docker compose up -d` first
(the containers do not start on their own). Integration tests need only a running Docker Desktop: they start
their own containers. Unit tests and Angular tests need neither.

```powershell
docker compose up -d                                # PostgreSQL + pgAdmin + Keycloak + RabbitMQ + Mailpit; docker compose stop when done
docker compose --profile app up -d --build --wait   # the whole shop in containers: http://localhost:8080
                                                    # from CI images instead: ESHOP_TAG=latest (or sha-<commit>),
                                                    # docker compose --profile app pull; up -d --no-build --wait
docker compose --profile app down                   # stop the shop and the infrastructure (data stays)
dotnet build EShop.sln                              # whole solution
dotnet run --project src/EShop.ProductApi           # same for CategoryApi, ShoppingCart, OrderApi, CustomerApi
dotnet run --project src/EShop.NotificationService  # worker: order events → emails in Mailpit (http://localhost:8025)
                                                    # API reference (Development): https://localhost:<port>/scalar
cd src/EShop.ClientApp; npm ci; npm start           # Angular dev server on :4200
dotnet test                                         # all .NET tests; integration tests need Docker (Test databases, below)
powershell -ExecutionPolicy Bypass -File tests/coverage.ps1   # tests + coverage report in TestResults/coverage-report
cd src/EShop.ClientApp; npm test                    # Angular unit tests (Vitest)
csharpier format <file>                             # format edited C# files
```

An access token for manual API calls (client `eshop-dev`, development only; use `curl.exe` in PowerShell 5.1):

```powershell
curl.exe -d "grant_type=password&client_id=eshop-dev&username=customer&password=customer1234" http://localhost:8180/realms/eshop/protocol/openid-connect/token
```

CI (`.github/workflows/ci.yml`) runs the same build and tests on every pull request and push to `master`,
then builds the eight Docker images (job `images`); only on `master` it pushes them to ghcr.io as
`ghcr.io/fobarwb/eshop-<productapi|categoryapi|shoppingcart|orderapi|customerapi|notificationservice|client|migrator>` with the tags `sha-<commit>` and `latest`.
The packages are public (they inherit the visibility of this public repository): `docker pull` needs no login.
Test rules: `.claude/rules/testing.md`. After a change, build and test the affected projects and report which checks actually ran.

## Database

Each schema is owned by the EF Core migrations of its DbContext; Northwind data is loaded separately.

| DbContext | Project (`--project`) | Schema | Migrations |
|---|---|---|---|
| `CatalogDbContext` | `src/EShop.Catalog.Data` | `catalog` | `Migrations/` |
| `CartDbContext` | `src/EShop.ShoppingCart` | `cart` | `Data/Migrations/` |
| `OrderingDbContext` | `src/EShop.OrderApi` | `ordering` | `Data/Migrations/` |
| `CustomerDbContext` | `src/EShop.CustomerApi` | `customer` | `Data/Migrations/` |
| `NotificationDbContext` | `src/EShop.NotificationService` | `notification` | `Data/Migrations/` |

`dotnet ef` needs no `--startup-project`: each project has an `IDesignTimeDbContextFactory`, so no service starts.
Commands that connect (`database update`) need `--connection '<connection string>'`; without it the factory uses
`Host=localhost;Database=eshop` with no password.

First-time setup (the first `docker compose up` creates the role `eshop` with `CREATEDB` and the database `eshop`,
and the role and database `keycloak`, see `db/init/`):

```powershell
Copy-Item .env.example .env          # set the passwords; ESHOP_DB_PASSWORD is the password of the role eshop
docker compose up -d                 # PostgreSQL + pgAdmin + Keycloak + RabbitMQ + Mailpit; docker compose ps shows them as "healthy"
dotnet user-secrets set "ConnectionStrings:EShop" 'Host=localhost;Port=5432;Database=eshop;Username=eshop;Password=<password>' --project src/EShop.ProductApi
dotnet user-secrets set "RabbitMq:Password" '<RABBITMQ_PASSWORD from .env>' --project src/EShop.ProductApi
docker compose run --rm --build migrator                                                                   # the five schemas
docker compose cp db/northwind-catalog.sql postgres:/tmp/northwind-catalog.sql                             # empty schemas only
docker compose cp db/northwind-ordering.sql postgres:/tmp/northwind-ordering.sql
docker compose exec postgres psql -U eshop -d eshop -v ON_ERROR_STOP=1 -f /tmp/northwind-catalog.sql
docker compose exec postgres psql -U eshop -d eshop -v ON_ERROR_STOP=1 -f /tmp/northwind-ordering.sql
```

The data is loaded with the container's `psql` (no local PostgreSQL needed); copying the files avoids piping them through
PowerShell 5.1, which would change the encoding of non-ASCII Northwind names.

The product replicas (`cart_products`, `order_products`) are filled by events, not by the SQL file: once ShoppingCart and
OrderApi have run (they declare their queues), call `POST /api/products/events/republish` as an admin (see
`Eshop.Obsidian/Services/ProductApi.md`). Checkout answers 409 for every product until `order_products` is filled.

- `docker compose down` keeps the data (named volume `eshop_postgres-data`); `docker compose down -v` deletes it,
  and the next `up` runs `db/init/` again.
- A volume created before Keycloak was added has no `keycloak` database (init scripts run only on an empty volume):
  `docker compose up -d postgres; docker compose exec postgres bash /docker-entrypoint-initdb.d/02-create-keycloak.sh`.
- Keycloak keeps its state in the database `keycloak`: the realm file is imported only when the realm does not exist,
  so later changes in the admin console stay, and changes of the file need the realm deleted (or `down -v`).
- Model change → `dotnet ef migrations add <Name> --project <project>`, with `--output-dir Data/Migrations` for the
  services (table above). One context on the local database:
  `dotnet ef database update --project <project> --connection '<connection string>'`.
- In the profile `app` the one-off service `migrator` (`db/migrator/`: one EF Core migrations bundle per DbContext,
  run in turn by `migrate.sh`) applies pending migrations before the APIs start; it runs on every `up` and stops at the
  first failing schema. Migrations are compiled into its image: after a new migration use `--build`.
  Northwind data is still loaded manually (above).
- `db/northwind-catalog.sql` and `db/northwind-ordering.sql` are generated from `archive/Northwind4SQLServer.sql`;
  do not edit them by hand: `powershell -ExecutionPolicy Bypass -File db/convert-northwind.ps1`. Columns the dump does
  not have get the values the migrations of the shared database gave the loaded rows (`status` `Delivered`,
  `revision` 0, `product_name` from the products).

### Test databases

Each integration test project starts its own throwaway `postgres:18` container (Testcontainers, `TestDatabase`),
creates its database there, `eshop_test_<project>` (e.g. `eshop_test_shoppingcart`), applies its service's
migrations (`TestDatabase<TContext>`) and empties the tables of that schema before each test. The container is removed after the run; no test database or user-secret is needed, locally or in CI.

Fallback for a machine without Docker, or a quick loop on one project: the environment variable
`ConnectionStrings__EShopTests` points the tests at another server instead (on compose the role `eshop` has `CREATEDB`, `db/init/`).
Only the environment variable is read, not user-secrets, so a forgotten setting cannot silently replace the container:

```powershell
$env:ConnectionStrings__EShopTests = 'Host=localhost;Port=5432;Database=eshop_test;Username=eshop;Password=<password>'
dotnet test --project tests/EShop.CategoryApi.Tests.Integration
Remove-Item Env:ConnectionStrings__EShopTests
```

The database name must end with `_test`; the tests refuse any other database.

## Approval rules

This is a learning project: the owner reviews every change before it is applied.

- Reading, searching, building and other read-only checks may proceed without asking.
- Before creating, editing or deleting files: show the proposed content or diff with a short explanation, then wait for approval.
  Approval covers exactly the shown scope; new substantive changes need a new approval.
- Before any operation that changes Git or GitHub state (branch, stage, commit, push, PR, merge, issue, label, project item):
  show the exact commands and their effect, then wait for approval.
- Approval of edits does not authorize commits or pushes.
- Commits and pull requests: by default the agent prepares the list of files to stage, the commit message
  and the PR title/body (following the template); the owner commits, pushes and opens the PR.
  The agent runs `git commit` or `gh pr create` only when the owner explicitly asks,
  and never runs `git push` without the owner's explicit approval.

## Git and GitHub

- Default branch: `master`. One branch per issue: `<type>/<issue>-<slug>`,
  types `feature`, `fix`, `refactor`, `upgrade`, `docs`, `test`, `chore` (e.g. `upgrade/12-dotnet10`).
- Starting an issue (before any file changes):
  1. Assign `FobarWB`.
  2. In the "Eshop" project set Status `In progress`, Priority (`P0`–`P2`), Size (`XS`–`XL`), Estimate and Start date.
  3. Create the branch from the issue so it is linked in its Development section:
     `gh issue develop <N> --name <type>/<N>-<slug> --base master --checkout`.
- Finishing an issue: the PR closes it (`Closes #N`); set End date and Status `Done` in the project.
- Pull requests into `master` use `.github/pull_request_template.md` and link the issue (`Closes #N`).
- Commit messages: English, imperative mood, short subject line.
- GitHub account: `FobarWB` only. Check `gh auth status` before GitHub operations; stop if another account is active.
- Labels describe type and area only (`area: *`, `upgrade`, `refactoring`, `tests`, `epic`, `learning`, defaults).
  Priority, size and status are fields of the "Eshop" project, not labels.

## Language

Write all repository content in English: code, comments, documentation, commits, issues and pull requests.

## Knowledge vault

`Eshop.Obsidian/` is an Obsidian vault with service notes, architecture decisions and learning notes.
Read `Eshop.Obsidian/Workflow.md` before changing notes, and the note of the affected service before changing its code.
When a change alters documented behavior or a decision is made, update the notes in the same pull request.
