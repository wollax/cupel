---
title: "TiktokenTokenCounter thread-safety documentation"
area: docs
priority: low
source: phase-10-pr-review
---

# TiktokenTokenCounter Thread-Safety Documentation

`TiktokenTokenCounter` is commonly registered as a singleton in DI containers. There is no documentation indicating whether it is thread-safe, which is a critical concern for singleton usage.

## Suggestion

Add a `<remarks>` XML doc to `TiktokenTokenCounter` explicitly stating its thread-safety contract. If the underlying `Tiktoken.Encoder` is thread-safe, document that. If not, document the limitation so consumers know to register it as scoped or use a lock.

Example:

```csharp
/// <remarks>
/// This class is thread-safe and suitable for singleton registration in dependency injection containers.
/// The underlying Tiktoken encoder is initialized once and shared across calls.
/// </remarks>
```

## Files

- `src/Wollax.Cupel.Json/TiktokenTokenCounter.cs` (or equivalent location)
