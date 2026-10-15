---
paths:
  - "**/ClientApp/src/**"
---

# Angular client

- Standalone components only; keep the existing folders: `components/`, `services/`, `models/`, `directives/`.
- Models are interfaces with the `I` prefix in `models/` (`ICart`, `ICartItem`).
- HTTP calls go through services, never directly from components.
- New code uses `inject()` and signals where the current Angular version supports them; do not mass-migrate existing code.
- Members used only by the template are `protected`; `input()`, `output()`, `model()` and queries are `readonly`.
- Use `[class.x]` / `[style.x]` bindings instead of `NgClass` / `NgStyle`.
- Name event handlers by what they do (`removeItem()`), not by the event (`onClick()`).
- Keep lifecycle hooks short: call well-named methods from `ngOnInit`; implement the hook interfaces.
- Complex template expressions move into a `computed()` signal.
- Unsubscribe from long-lived observables (`takeUntilDestroyed`, `async` pipe).
- No `console.log` in committed code.
- Follow the formatting of the file being edited (the project uses braces on new lines).
