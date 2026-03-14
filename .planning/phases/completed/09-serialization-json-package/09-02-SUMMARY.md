---
phase: 09-serialization-json-package
plan: 02
subsystem: Wollax.Cupel.Json
tags: [serialization, custom-scorer, registration, error-handling]
depends_on:
  requires: [09-01]
  provides: [custom-scorer-registration, unknown-type-errors]
  affects: [CupelJsonOptions, CupelJsonSerializer]
tech-stack:
  added: []
  patterns: [fluent-api, factory-registry, catch-and-rethrow]
key-files:
  created:
    - tests/Wollax.Cupel.Json.Tests/CustomScorerTests.cs
  modified:
    - src/Wollax.Cupel.Json/CupelJsonOptions.cs
    - src/Wollax.Cupel.Json/CupelJsonSerializer.cs
    - src/Wollax.Cupel.Json/PublicAPI.Unshipped.txt
decisions:
  - RegisterScorer uses fluent API pattern (returns this) for chaining
  - Factory registry stores Func<JsonElement?, IScorer> internally, parameterless overload wraps
  - Unknown scorer type detection uses JsonDocument.Parse on raw JSON to extract unknown type names
  - Built-in scorer type names hardcoded as string array matching [JsonStringEnumMemberName] values
  - Error messages include all built-in types, custom registered types, and RegisterScorer suggestion
metrics:
  duration: 5m30s
  completed: 2026-03-14
---

# Phase 09 Plan 02: Custom Scorer Registration & Unknown-Type Errors Summary

RegisterScorer API added to CupelJsonOptions with fluent chaining, factory registry for custom scorer types, and descriptive error messages when unknown scorer types appear in JSON.

## What Was Done

### Task 1: Custom Scorer Registration and Unknown-Type Errors (TDD)

**RED:** Created `CustomScorerTests.cs` with 13 test cases covering registration API, duplicate name overwrites, config-aware factories, unknown type error messages, input validation, and factory query methods.

**GREEN:** Extended `CupelJsonOptions` with:
- `RegisterScorer(string, Func<IScorer>)` — parameterless factory overload
- `RegisterScorer(string, Func<JsonElement?, IScorer>)` — config-aware factory overload
- `GetScorerFactory(string)` — retrieve registered factory by name
- `HasScorerFactory(string)` — check if factory exists
- `RegisteredScorerNames` — enumerate all registered custom type names
- Input validation: null/empty/whitespace type names throw `ArgumentException`, null factories throw `ArgumentNullException`

Updated `CupelJsonSerializer.Deserialize` to catch `JsonException` from STJ enum parsing failures, detect unknown scorer types by parsing the raw JSON, and re-throw with a descriptive error message listing all known built-in types, custom registered types, and suggesting `RegisterScorer()`.

### Task 2: PublicAPI Tracking

Updated `PublicAPI.Unshipped.txt` with all new public API surface (5 new members). Build produces zero RS0016/RS0017 warnings.

## Verification Results

- `dotnet build` — zero errors, zero warnings
- `dotnet test --project tests/Wollax.Cupel.Json.Tests` — 25/25 tests pass
- `dotnet test` (full solution) — 559/559 tests pass, zero regressions

## Deviations

None. Plan executed as specified.

## Decisions Made

1. **Factory storage:** All factories stored as `Func<JsonElement?, IScorer>` internally; the parameterless overload wraps via `_ => factory()`. This simplifies the internal dictionary to a single type.

2. **Unknown type detection:** Uses `JsonDocument.Parse` on the raw JSON string to extract scorer type values and compare against the known built-in set. This approach is reliable regardless of STJ's internal error message format across versions.

3. **Built-in type names hardcoded:** The array `["recency", "priority", "kind", "tag", "frequency", "reflexive"]` mirrors the `[JsonStringEnumMemberName]` attributes on `ScorerType`. This avoids needing to serialize each enum value through the source-gen context at static init time.

4. **PublicAPI tracking done in Task 1:** Since the build requires `PublicAPI.Unshipped.txt` to be current for compilation (RS0016 is an error, not a warning), the API entries were added alongside the implementation rather than in a separate task.
