---
title: "Document WithTokenCount overwrite behavior"
area: docs
priority: low
source: phase-10-pr-review
---

# Document WithTokenCount Overwrite Behavior

`WithTokenCount` silently overwrites any previously set token count on a `ContextItem`. This behavior is not documented and can surprise consumers who call it multiple times or use it in combination with auto-counting.

## Suggestion

Add a `<remarks>` XML doc to `WithTokenCount` stating explicitly that it overwrites any existing token count, including one previously assigned by an auto-counting step.

Example:

```csharp
/// <remarks>
/// Calling this method overwrites any previously assigned token count, including
/// counts computed automatically by a registered <c>ITokenCounter</c>.
/// </remarks>
```

## Files

- `src/Wollax.Cupel/ContextItem.cs` (or `ContextItemBuilder` — wherever `WithTokenCount` is defined)
