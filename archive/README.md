# Archive

SQL Server-era code kept for reference. It is not part of `EShop.sln` and is neither built nor maintained.

- `Northwind.Mvc/`: legacy ASP.NET Core MVC client.
- `Northwind4SQLServer.sql`: T-SQL script that creates and fills the Northwind database on SQL Server.
  It does not run on PostgreSQL.

The last working SQL Server version of the whole solution is the git tag `sqlserver-final`.
To run it next to the current code:

```powershell
git worktree add ../EShop-sqlserver sqlserver-final
```
