---
type: topic
verified: 2026-11-10
issue: 45
---

# DevEnvironment

Related: [Docker](Docker.md), [Authentication](Authentication.md), [Testing](Testing.md), [Workflow](../Workflow.md).

Pitfalls of the local environment (Windows 11, PowerShell 5.1, Git Bash, Docker Desktop) and how to check the result.
For people and agents alike: a command that "succeeded" is checked by reading the state it changed.

## Key ideas

- **PowerShell 5.1 strips inner double quotes of native-command arguments.** `docker compose exec keycloak bash -c
  '... -s "name=OrderApi: checks ..."'` reached kcadm as `name=OrderApi:` plus loose words; kcadm printed
  "Created new client" and silently ignored every later `-s` (the secret, `serviceAccountsEnabled`) (#45).
  Commands with quoted values that contain spaces run from Git Bash; `gh api graphql` has the same problem.
- **No `&&` in PowerShell 5.1:** `A; if ($?) { B }`.
- **Piping files through PowerShell changes the encoding** of non-ASCII text: copy the file into the container
  (`docker compose cp`) and run `psql -f` there (see AGENTS.md, Database).
- **Secrets are compared, not printed.** `dotnet user-secrets list` shows values in plain text. Compare in Git Bash
  and print only the result and the lengths:
  `e=$(grep '^NAME=' .env | cut -d= -f2- | tr -d '\r'); u=$(dotnet user-secrets list --project src/EShop.ProductApi | tr -d '\r' | sed -n 's/^Section:Key = //p'); echo "${#e} ${#u}"; [ "$u" = "$e" ] && echo MATCH`
- **`csharpier format <directory>` also reformats the `.csproj` files in it.** Pass the edited `.cs` files.
- **A floating image tag is an untested dependency.** `quay.io/keycloak/keycloak:26.8` was 26.8.1 in the local cache
  and 26.8.2 on the CI runner, which pulls fresh: the tests passed locally and failed in CI (#45, a breaking security
  fix of the Admin API). Keycloak is pinned to an exact version now, so an upgrade is a visible change in a PR.
  To compare without pulling: `docker image inspect <image> --format '{{index .RepoDigests 0}}'` against
  `docker buildx imagetools inspect <image>`; a specific build can be pulled by digest without moving the local tag.
- **A 401 that "should not happen": switch on the server's debug log for authentication** in a throwaway container,
  e.g. `KC_LOG_LEVEL=info,org.keycloak.services.managers:debug,org.keycloak.services.resources.admin:debug`, and repeat
  the one request; the reason is in the log, not in the response.

## Applied in this project

- Keycloak changes for an existing realm (the file is imported only once): kcadm from Git Bash, then read back with
  `kcadm.sh get clients/<id>` and `get-roles`, or sign in as the client
  (`kcadm.sh config credentials --client <id> --secret ...`) to prove secret and roles at once.
- GitHub Project "Eshop" (number 1, owner FobarWB, id `PVT_kwHOBYkLH84AqeOS`): field ids for `gh project item-edit`:
  Status `PVTSSF_lAHOBYkLH84AqeOSzghtsWc` (Backlog `f75ad846`, Ready `61e4505c`, In progress `47fc9ee4`,
  In review `df73e18b`, Done `98236657`), Priority `PVTSSF_lAHOBYkLH84AqeOSzghtsw4` (P0 `79628723`, P1 `0a877460`,
  P2 `da944a9c`), Size `PVTSSF_lAHOBYkLH84AqeOSzghtsw8` (XS `6c6483d2`, S `f784b110`, M `7515a9f1`, L `817d0097`,
  XL `db339eb2`), Estimate `PVTF_lAHOBYkLH84AqeOSzghtsxA`, Start date `PVTF_lAHOBYkLH84AqeOSzghtsxE`,
  End date `PVTF_lAHOBYkLH84AqeOSzghtsxI`. A new issue's item id: GraphQL `issue(number:N){projectItems{nodes{id}}}`
  (from Git Bash); `gh project item-list` shows only 30 items and lags behind.

## Open questions

- (none)
