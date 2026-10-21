---
type: topic
verified: 2026-10-21
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
- The Windows service `postgresql-x64-18` is stopped and set to Manual; the container uses port 5432, so connection
  strings in user-secrets did not change.
- Moved the data of the Windows service: `pg_dump -Fc` (77 products, 830 orders, 1 cart) → `pg_restore --no-owner`;
  the pending migration from #31 was applied afterwards.
- No restart policy on purpose: the containers run only when started (`docker compose up -d` before working on the
  APIs or running integration tests). The launch configurations (`.vscode/launch.json`, `launchSettings.json`) did not
  change: the APIs still connect to `localhost:5432`.
- Northwind data is loaded with the container's `psql` (`docker compose cp` + `exec psql -f`), so a new developer needs
  no local PostgreSQL.
- Useful commands: `docker compose up -d`, `ps`, `logs -f postgres`, `stop`/`start`, `down`, `down -v`,
  `exec postgres psql -U eshop -d eshop`, `pull`.

## Open questions

- Pin exact image versions (`postgres:18.x`) or follow the major tag; who updates them (Dependabot can).
- Apply EF migrations automatically when the APIs run in compose (#48) or keep `dotnet ef database update` manual.
