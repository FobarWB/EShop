# EShop

Learning pet project: an online shop built from small ASP.NET Core Web API services and an Angular client,
on top of the Northwind sample database. The owner uses it to learn PostgreSQL, Docker and RabbitMQ:
explain the reasoning behind a change, not only the change itself.

Roadmap: issue #17. Planning: GitHub Project "Eshop" (owner `FobarWB`).

## Repository map

| Path | Purpose | Local URL |
|---|---|---|
| `src/EShop.ProductApi/` | Products REST API | https://localhost:5002 |
| `src/EShop.CategoryApi/` | Categories REST API | https://localhost:5003 |
| `src/EShop.ShoppingCart/` | Shopping cart REST API | https://localhost:5004 |
| `src/EShop.OrderApi/` | Orders REST API: checkout from the cart | https://localhost:5005 |
| `src/EShop.ClientApp/` | Angular 22 SPA; `nginx.conf` and `Dockerfile` of the client container | http://localhost:4200 (`ng serve`), http://localhost:8080 (compose profile `app`) |
| `src/EShop.Data/` | EF Core `EShopDbContext`, `AddEShopDbContext()`, migrations (namespace `EShop.Data`); `Dockerfile` of the migrator | — |
| `src/EShop.Data.Entities/` | EF Core entities (namespace `EShop.Data.Entities`) | — |
| `src/EShop.NotificationService/` | Worker: consumes order events from RabbitMQ, sends emails through Mailpit (SMTP localhost:1025) | — |
| `src/EShop.Messaging/` | Shared messaging infrastructure: transactional outbox, inbox (idempotent consumers), RabbitMQ connection and topology | — |
| `src/EShop.OrderApi.Contracts/` | Events published by OrderApi (`OrderPlaced`, `OrderCancelled`): the contract its consumers deserialize | — |
| `src/EShop.ServiceDefaults/` | Shared API setup: JWT validation `AddEShopAuthentication()`, `User.GetUserId()` (token `sub`) | — |
| `tests/` | xUnit unit and integration tests per service, shared helpers `EShop.Tests.Common`, `coverage.ps1` | — |
| `docker-compose.yml` | Local environment: PostgreSQL 18, pgAdmin, Keycloak, RabbitMQ and Mailpit; with the profile `app` also the migrator, the four APIs and the client (settings in `.env`, template `.env.example`) | pgAdmin: http://localhost:5050, Keycloak: http://localhost:8180, RabbitMQ: http://localhost:15672, Mailpit: http://localhost:8025 |
| `keycloak/realms/` | Realm `eshop`, imported on Keycloak's first start: clients, role `admin`, development users `admin`/`admin` and `customer`/`customer` (local only, never on a server) | — |
| `db/` | Northwind data for PostgreSQL (`northwind-data.sql`) and its generator `convert-northwind.ps1`; container init script `db/init/`, pgAdmin server list `db/pgadmin/` | — |
| `archive/` | **Archived** SQL Server-era code: `Northwind.Mvc` (not in `EShop.sln`, not built) and `Northwind4SQLServer.sql`. Do not change; see `archive/README.md`. | — |
| `Eshop.Obsidian/` | Obsidian knowledge vault: service notes, decisions, learning notes | — |

- All APIs share one database through the common DbContext.
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
  reads are public, writes need the realm role `admin`; every cart and order endpoint needs a signed-in user (`/api/carts/me`, `/api/orders`).
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
  (`UserSecretsId` `eshop`, shared by the four APIs); it is never committed.
- Windows PowerShell may block `.ps1` scripts (execution policy): use `npm.cmd` or
  `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`.

## Commands

The APIs and the integration tests need the database: start Docker Desktop and `docker compose up -d` first
(the containers do not start on their own). Unit tests and Angular tests do not need it.

```powershell
docker compose up -d                                # PostgreSQL + pgAdmin + Keycloak + RabbitMQ + Mailpit; docker compose stop when done
docker compose --profile app up -d --build --wait   # the whole shop in containers: http://localhost:8080
                                                    # from CI images instead: ESHOP_TAG=latest (or sha-<commit>),
                                                    # docker compose --profile app pull; up -d --no-build --wait
docker compose --profile app down                   # stop the shop and the infrastructure (data stays)
dotnet build EShop.sln                              # whole solution
dotnet run --project src/EShop.ProductApi           # same for CategoryApi, ShoppingCart, OrderApi
                                                    # API reference (Development): https://localhost:<port>/scalar
cd src/EShop.ClientApp; npm ci; npm start           # Angular dev server on :4200
dotnet test                                         # all .NET tests; integration tests need the test database (below)
powershell -ExecutionPolicy Bypass -File tests/coverage.ps1   # tests + coverage report in TestResults/coverage-report
cd src/EShop.ClientApp; npm test                    # Angular unit tests (Vitest)
csharpier format <file>                             # format edited C# files
```

An access token for manual API calls (client `eshop-dev`, development only; use `curl.exe` in PowerShell 5.1):

```powershell
curl.exe -d "grant_type=password&client_id=eshop-dev&username=customer&password=customer" http://localhost:8180/realms/eshop/protocol/openid-connect/token
```

CI (`.github/workflows/ci.yml`) runs the same build and tests on every pull request and push to `master`,
then builds the six Docker images (job `images`); only on `master` it pushes them to ghcr.io as
`ghcr.io/fobarwb/eshop-<productapi|categoryapi|shoppingcart|orderapi|client|migrator>` with the tags `sha-<commit>` and `latest`.
The packages are public (they inherit the visibility of this public repository): `docker pull` needs no login.
Test rules: `.claude/rules/testing.md`. After a change, build and test the affected projects and report which checks actually ran.

## Database

The schema is owned by EF Core migrations in `src/EShop.Data/Migrations`; Northwind data is loaded separately.
`dotnet ef` needs `--startup-project`: the DbContext is configured in the API's `Program.cs`.

First-time setup (the first `docker compose up` creates the role `eshop` with `CREATEDB` and the database `eshop`,
and the role and database `keycloak`, see `db/init/`):

```powershell
Copy-Item .env.example .env          # set the passwords; ESHOP_DB_PASSWORD is the password of the role eshop
docker compose up -d                 # PostgreSQL + pgAdmin + Keycloak + RabbitMQ + Mailpit; docker compose ps shows them as "healthy"
dotnet user-secrets set "ConnectionStrings:EShop" 'Host=localhost;Port=5432;Database=eshop;Username=eshop;Password=<password>' --project src/EShop.ProductApi
dotnet ef database update --project src/EShop.Data --startup-project src/EShop.ProductApi
docker compose cp db/northwind-data.sql postgres:/tmp/northwind-data.sql                                   # empty database only
docker compose exec postgres psql -U eshop -d eshop -v ON_ERROR_STOP=1 -f /tmp/northwind-data.sql
```

The data is loaded with the container's `psql` (no local PostgreSQL needed); copying the file avoids piping it through
PowerShell 5.1, which would change the encoding of non-ASCII Northwind names.

- `docker compose down` keeps the data (named volume `eshop_postgres-data`); `docker compose down -v` deletes it,
  and the next `up` runs `db/init/` again.
- A volume created before Keycloak was added has no `keycloak` database (init scripts run only on an empty volume):
  `docker compose up -d postgres; docker compose exec postgres bash /docker-entrypoint-initdb.d/02-create-keycloak.sh`.
- Keycloak keeps its state in the database `keycloak`: the realm file is imported only when the realm does not exist,
  so later changes in the admin console stay, and changes of the file need the realm deleted (or `down -v`).
- Model change → `dotnet ef migrations add <Name> --project src/EShop.Data --startup-project src/EShop.ProductApi`.
- In the profile `app` the one-off service `migrator` (an EF Core migrations bundle, `src/EShop.Data/Dockerfile`)
  applies pending migrations before the APIs start; it runs on every `up`. Migrations are compiled into its image:
  after a new migration use `--build`. Northwind data is still loaded manually (above).
- `db/northwind-data.sql` is generated from `archive/Northwind4SQLServer.sql`; do not edit it by hand:
  `powershell -ExecutionPolicy Bypass -File db/convert-northwind.ps1`.

### Test databases

Integration tests create and migrate their own databases, named `<EShopTests database>_<project>`
(e.g. `eshop_test_shoppingcart`), and empty them before each test; the role `eshop` has `CREATEDB` (`db/init/`).

```powershell
dotnet user-secrets set "ConnectionStrings:EShopTests" 'Host=localhost;Port=5432;Database=eshop_test;Username=eshop;Password=<password>' --project src/EShop.ProductApi
```

The database name must end with `_test`; the tests refuse any other database. CI can set `ConnectionStrings__EShopTests` instead.

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
