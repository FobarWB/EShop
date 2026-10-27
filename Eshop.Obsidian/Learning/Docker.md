---
type: topic
verified: 2026-10-26
issue: 47
---

# Docker

Related: [PostgreSql](PostgreSql.md), [Testing](Testing.md), [Services](../Services/Services.md).

## Key ideas

- **Image vs container.** An image is a read-only template with the program already installed (`postgres:18`:
  Linux + PostgreSQL + an entrypoint script), published by its authors to a registry (Docker Hub). A container is a
  running instance of an image: an isolated process with its own file system and network, not a virtual machine.
  On Windows the Linux kernel comes from WSL2 (the `docker-desktop` distribution); WSL2 needs the Windows component
  "Virtual Machine Platform".
- **Containers are disposable, data is not.** Files written inside a container disappear with it; a database keeps
  its data in a named volume, managed by Docker and mounted into the container. `docker compose down` removes
  containers and the network but keeps volumes; `down -v` removes the volumes too.
- **Ports are private until published.** `127.0.0.1:5432:5432` maps host port → container port on localhost only;
  without the address Docker publishes on all interfaces and can bypass the Windows firewall.
- **One network per compose project.** Services find each other by service name through Docker's DNS
  (pgAdmin connects to host `postgres`); `localhost` inside a container is the container itself.
- **Configuration through environment variables.** Images read settings such as `POSTGRES_PASSWORD` at start.
  Compose reads `.env` next to `docker-compose.yml` and substitutes `${VAR}`; `${VAR:?message}` stops with an error
  when the value is missing.
- **Running is not ready.** A healthcheck (`pg_isready`) tells when the database accepts connections;
  `depends_on: condition: service_healthy` makes dependent services wait for it.
- **Init scripts run once.** The postgres image runs `/docker-entrypoint-initdb.d/*` only when the data volume is
  empty. Changing a script or the passwords in `.env` later changes nothing for the existing volume: the role keeps
  the old password (seen in #47: the role got the `.env.example` placeholder). Fix: `down -v`, or `\password` inside
  the container. Windows 11 Notepad may keep edits in its session without writing the file: check `LastWriteTime`.
- **Line endings.** Scripts executed inside Linux containers must have LF endings; with `core.autocrlf=true`
  Git checks them out with CRLF unless `.gitattributes` says `*.sh text eol=lf`.
- **Compose describes, Docker Hub stores, every developer has their own data.** The repository holds
  `docker-compose.yml`, init scripts and `.env.example`; images are pulled from the registry; data and `.env`
  stay on each machine. Own images (built from a Dockerfile) are published to a registry only for our services (#48, #36).

## Applied in this project

- [docker-compose.yml](../../docker-compose.yml): `postgres:18` (volume `eshop_postgres-data` mounted at
  `/var/lib/postgresql`: since version 18 the data lives in a version subfolder, so a future major version can sit
  next to it for `pg_upgrade`) and `dpage/pgadmin4` on http://localhost:5050, both bound to `127.0.0.1`.
- [db/init/01-create-eshop.sh](../../db/init/01-create-eshop.sh) creates the role `eshop` (`LOGIN CREATEDB`) and the
  database `eshop`. The password comes from the environment and is passed as a psql variable (`:'eshop_password'`),
  so psql quotes it as a literal instead of the shell pasting it into SQL.
- [db/pgadmin/servers.json](../../db/pgadmin/servers.json) registers the server in pgAdmin on first start.
- `postgres` and `keycloak` publish their ports on `127.0.0.1` and `[::1]` (#41). On Windows `localhost` resolves to
  `::1` first; with only an IPv4 binding a refused IPv6 connection costs about 2 s before the fallback to `127.0.0.1`.
  Clients that try addresses in order (Npgsql, libpq, .NET `HttpClient`) paid it on every new connection: the first
  request after start took 4–6 s, the first after ~5 min idle (pooled connections closed) about 2 s. Measured with
  `psql -h localhost`: 2.1–2.3 s before, 33–79 ms after. Browsers try both families at once and did not notice.
- The Windows service `postgresql-x64-18` is stopped and set to Manual; the container uses port 5432, so connection
  strings in user-secrets did not change.
- Moved the data of the Windows service: `pg_dump -Fc` (77 products, 830 orders, 1 cart) → `pg_restore --no-owner`;
  the pending migration from #31 was applied afterwards.
- No restart policy for postgres and pgAdmin on purpose: they run only when started (`docker compose up -d` before
  working on the APIs or running integration tests). The launch configurations (`.vscode/launch.json`,
  `launchSettings.json`) did not change: the APIs still connect to `localhost:5432`.
- Northwind data is loaded with the container's `psql` (`docker compose cp` + `exec psql -f`), so a new developer needs
  no local PostgreSQL.
- Useful commands: `docker compose up -d`, `ps`, `logs -f postgres`, `stop`/`start`, `down`, `down -v`,
  `exec postgres psql -U eshop -d eshop`, `pull`.

## Own images (#48)

- **Dockerfile → image → container.** A Dockerfile is the recipe in Git; `docker build` makes a local image;
  `docker run` / compose starts containers from it; `docker push` to a registry is for servers (#36).
- **Multi-stage.** Build in a heavy image (`sdk:10.0`, `node:24`), copy only the result into a small runtime image
  (`aspnet:10.0`, nginx). The SDK, sources and package caches never ship: API 354 MB, migrator 389 MB, client 125 MB.
- **Layers and cache.** Each instruction is a cached layer, reused while its inputs do not change. Copy the project
  files (`*.csproj`, `Directory.*.props`, `package*.json`) and restore first, the code later: a code change rebuilds
  in seconds. The migrator starts with the same instructions as ProductApi and reuses its restore layers.
- **Build context.** Everything `docker build` may `COPY`; ours is the repository root (projects reference each
  other and the root props). `.dockerignore` keeps `bin/`, `obj/`, `node_modules/`, `.git/` and `.env` out of it.
- **Non-root.** The .NET images have the user `app` (`USER $APP_UID`) and listen on 8080; `nginx-unprivileged`
  runs as `nginx` on 8080. Ports below 1024 need root.
- **One-off containers.** The migrator applies migrations and exits; the APIs wait for
  `depends_on: condition: service_completed_successfully`, so they never run on an outdated schema. EF Core locks
  `__EFMigrationsHistory` while applying, so parallel runs wait for each other. "Exited (0)" in Docker Desktop is
  its normal state. The ▶ button reruns the old image: new migrations need `--build`.
- **Migrations bundle.** `dotnet ef migrations bundle` compiles the migrations into `efbundle`. Building and running
  it executes ProductApi's `Program.cs` up to `Build()` to get the DbContext, so the build needs a placeholder
  connection string (`RUN VAR=value cmd` sets it for that command only; `ENV` would store it in the image).
  The real one comes from the environment at run time.
- **Profiles.** Services with `profiles: [app]` start only with `--profile app`; `docker compose up -d` still starts
  only the infrastructure. Both modes share one postgres container and one database.
- **Reverse proxy.** nginx serves the Angular files and forwards `/api/...` by prefix to the API containers; the SPA
  fallback (`try_files ... /index.html`) returns `index.html` for client routes. With a plain host name in
  `proxy_pass` nginx resolves it once at start: a recreated API with a new IP gives 502, and a missing API stops nginx.
  A variable plus `resolver 127.0.0.11` (Docker DNS) resolves per request.
- **TLS termination.** HTTPS ends at the entry point (nginx, a load balancer, an ingress), which holds the
  certificate; inside the private network the services talk HTTP. The proxy passes the original scheme and client
  address in `X-Forwarded-*` headers. Services encrypt internal traffic too only under stricter requirements
  (zero trust, mTLS through a service mesh).
- **Health checks.** `GET /health` (`AddDbContextCheck`) returns 200, or 503 when the database is unreachable.
  The aspnet image has no `curl`/`wget` (smaller attack surface), so compose checks through bash `/dev/tcp`.
  `--wait` blocks until everything is healthy. Compose only marks a container `unhealthy`; restarting it is
  an orchestrator's job.
- **Kerberos message.** Npgsql first tries GSSAPI encryption and logs "Cannot load library libgssapi_krb5.so.2":
  the .NET 10 images do not include it. We do not use Kerberos: `GssEncryptionMode=Disable` in the connection string.
- **SPA has no server of its own.** `ng serve` / Vite are development tools; in production a static server serves
  the build. A Node server appears only with server-side rendering (Next.js, Angular SSR).

Applied: `src/EShop.*/Dockerfile` (four APIs, `EShop.Data` = migrator, `EShop.ClientApp` = nginx), `.dockerignore`,
services `migrator`, `productapi`, `categoryapi`, `shoppingcart`, `orderapi`, `client` in [docker-compose.yml](../../docker-compose.yml)
(`restart: unless-stopped` for the shop services). Manual cycle:
`docker build -f src/EShop.ProductApi/Dockerfile -t eshop-productapi:dev .`, `docker history`, `docker run --network
eshop_default -e ConnectionStrings__EShop=...`, `docker exec -it <name> bash`.

## Registry and CD (#36)

- **CI checks the code, CD turns it into images.** The job `images` in `ci.yml` needs the test jobs, builds the six
  images on every run (a broken Dockerfile fails the PR) and pushes them only from `master`. Nothing is built on a
  server: every machine pulls the same image.
- **Registry and tags.** ghcr.io stores the images next to the repository (Packages). `sha-<commit>` is immutable:
  it tells exactly what runs and allows a rollback; `latest` moves with `master` and is for trying things out only.
  Image names must be lowercase (`fobarwb`, not `FobarWB`).
- **Least privilege.** The workflow keeps `contents: read`; only `images` gets `packages: write`. It logs in with
  `GITHUB_TOKEN`, a token issued for one run, so no registry secret is stored anywhere.
- **Cache on a fresh machine.** Every run starts without Docker's layer cache; `cache-to/cache-from: type=gha`
  keep the layers in the GitHub Actions cache, one `scope` per image; `mode=max` also stores the build stage (restore).
- **Build or pull.** Every shop service in compose has both `build:` and
  `image: ghcr.io/fobarwb/eshop-<name>:${ESHOP_TAG:-local}`. Local builds are tagged `:local`, so they never shadow
  `latest` from the registry; `ESHOP_TAG=latest` or `sha-<commit>` with `pull` + `up --no-build` runs the CI images
  (a server needs only the compose file, `db/`, its own `.env` and access to the packages). The explicit
  `--build` / `--no-build` flags decide instead of compose.
- **Concurrency.** Runs on a PR cancel each other; runs on `master` are never cancelled, so no merge loses its images.
- **Continuous delivery, not deployment.** Every `master` commit becomes images ready to run; putting them on a
  server (HTTPS, `Production`, own database and secrets, `UseForwardedHeaders`) is the next step.
- **Package visibility.** Expected private (the ghcr default), but the packages published by this public repository's
  workflow were public from the first push: anonymous `docker pull` and `tags/list` work (checked on 2026-10-23).
  A public package cannot become private again, only be deleted. Nothing secret is inside: passwords come from `.env`
  at run time, `.env` never enters the build context, and the code is public anyway. A private package needs
  `docker login ghcr.io` with a classic token that has `read:packages`, and read access per person
  (Package settings → Manage access).

## Open questions

- Pin exact image versions (`postgres:18.x`) or follow the major tag; who updates them (Dependabot can).
- Separate roles: the migrator as the schema owner, the APIs with data rights only.
