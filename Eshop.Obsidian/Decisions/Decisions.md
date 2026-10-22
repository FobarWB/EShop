---
type: index
verified: 2026-10-22
---

# Decisions

[Home](../Home.md)

Architecture decision records: the context, the decision and its consequences. New records use `_templates/Decision.md`.

- [UsePostgreSql](UsePostgreSql.md): replace SQL Server with PostgreSQL.
- [SeparateProductAndCategoryApis](SeparateProductAndCategoryApis.md): keep ProductApi and CategoryApi separate for now.
- [TestAgainstRealPostgreSql](TestAgainstRealPostgreSql.md): integration tests use real PostgreSQL, one database per test project.
- [RelativeApiUrlsBehindProxy](RelativeApiUrlsBehindProxy.md): the client calls `/api/...`; the dev server or nginx forwards to the APIs.
