---
title: "Document AddCupel additive/chaining behavior"
area: docs
priority: low
source: phase-10-pr-review
---

# Document AddCupel Additive/Chaining Behavior

`AddCupel` follows standard `IOptions` behavior where multiple calls chain configure actions rather than overwriting. This is non-obvious to consumers unfamiliar with the pattern and warrants explicit documentation.

## Suggestion

Add a `<remarks>` XML doc (or inline comment in the summary) to `AddCupel` explaining that multiple calls are additive — each `configure` delegate is appended to the options pipeline, not a replacement. Point consumers to `IOptions` documentation if they need the full contract.

Example:

```csharp
/// <remarks>
/// Calling <c>AddCupel</c> multiple times is additive. Each <paramref name="configure"/>
/// action is appended to the options configuration chain rather than replacing previous registrations.
/// </remarks>
```

## Files

- `src/Wollax.Cupel.Json/ServiceCollectionExtensions.cs` (or equivalent `AddCupel` location)
