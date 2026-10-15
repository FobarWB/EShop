---
paths:
  - "**/*.cs"
---

# C# conventions

Apply to new and changed code. Do not reformat or restyle untouched code outside the task scope.

- File-scoped namespaces (`namespace X;`).
- Private fields `_camelCase`; constructor-injected dependencies are `private readonly`.
- Async methods end with `Async` and accept a `CancellationToken` when they do I/O.
- Async all the way: never `.Result`, `.Wait()` or `async void`; do not wrap synchronous code in `Task.Run`.
- Do not use exceptions for normal control flow (e.g. "not found"); return a result instead.
- Nullable reference types are enabled: handle `null` explicitly; do not silence warnings with `!` without a reason in a comment.
- Use `var` when the type is obvious from the right-hand side.
- Comments in English and only where the intent is not obvious from the code. No commented-out code.
- Format edited files with `csharpier format <file>`.
