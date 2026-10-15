# EShop

Learning pet project: an online shop built from small ASP.NET Core Web API services and an Angular client,
on top of the Northwind sample database. The owner uses it to learn PostgreSQL, Docker and RabbitMQ:
explain the reasoning behind a change, not only the change itself.

Roadmap: issue #17. Planning: GitHub Project "Eshop" (owner `FobarWB`).

## Repository map

| Path | Purpose | Local URL |
|---|---|---|
| `Northwind.ProductApi/` | Products REST API | https://localhost:5002 |
| `Northwind.CategoryApi/` | Categories REST API | https://localhost:5003 |
| `Northwind.ShoppingCart/` | Shopping cart REST API | https://localhost:5004 |
| `Northwind.ClientApp/` | Angular 18 SPA | http://localhost:4200 |
| `Northwind.Common.DataContext.SqlServer/` | EF Core `NorthwindContext`, migrations | — |
| `Northwind.Common.EntityModels.SqlServer/` | EF Core entities (namespace `EShop.Shared`) | — |
| `Northwind.Mvc/` | **Archived** legacy MVC app. Keep it building; no feature work. | https://localhost:5001 |
| `Eshop.Obsidian/` | Obsidian knowledge vault: service notes, decisions, learning notes | — |

- All APIs share one database through the common DbContext.
- Ports are hardcoded in two places: `builder.WebHost.UseUrls(...)` in each API `Program.cs` (overrides `launchSettings.json`)
  and `apiUrl` in `Northwind.ClientApp/src/app/services/*.service.ts`. Change both sides together; CORS allows `http://localhost:4200`.

### Planned changes (update this section when an issue is done)

.NET 10 + EF Core 10 (#12) → rename data layer to `EShop.Data` (#13) → PostgreSQL (#14);
Angular 22 (#15); rename services to `EShop.*` and move `Northwind.Mvc` to `archive/` (#16); tests (#9); CI (#10).

## Environment

- Windows 11, PowerShell 5.1 (no `&&`); Git Bash is also available.
- .NET SDK 8.0 and 9.0 (projects target `net8.0`); Node 24, npm 11; `gh` CLI; `csharpier`.
- Database today: SQL Server. Only LocalDB (`(localdb)\MSSQLLocalDB`) exists on this machine, so connection strings
  pointing to `Server=.` do not work. PostgreSQL replaces SQL Server in #14.
- Not installed yet: .NET 10 SDK, `dotnet-ef`, PostgreSQL, Docker.

## Commands

```powershell
dotnet build EShop.sln                              # whole solution
dotnet run --project Northwind.ProductApi           # same for CategoryApi, ShoppingCart
cd Northwind.ClientApp; npm ci; npm start           # Angular dev server on :4200
csharpier format <file>                             # format edited C# files
```

There are no automated tests yet (#9). After a change, build the affected projects and report which checks actually ran.

## Approval rules

This is a learning project: the owner reviews every change before it is applied.

- Reading, searching, building and other read-only checks may proceed without asking.
- Before creating, editing or deleting files: show the proposed content or diff with a short explanation, then wait for approval.
  Approval covers exactly the shown scope; new substantive changes need a new approval.
- Before any operation that changes Git or GitHub state (branch, stage, commit, push, PR, merge, issue, label, project item):
  show the exact commands and their effect, then wait for approval.
- Approval of edits does not authorize commits or pushes.

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
