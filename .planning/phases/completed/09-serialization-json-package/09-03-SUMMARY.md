---
phase: 09-serialization-json-package
plan: 03
status: complete
started: 2026-03-14T13:42:09Z
completed: 2026-03-14T13:49:20Z
duration: 431s
---

# 09-03 Summary: Path-Aware Validation Error Handling

## Objective
Implement path-aware error handling for JSON deserialization so that constructor validation failures include context about what went wrong and where.

## Tasks Completed

### Task 1: Path-aware validation error tests and implementation (TDD)

**RED:** Created `ValidationTests.cs` (8 tests) and `ErrorMessageTests.cs` (8 tests) covering:
- Constructor validation errors (empty scorers, negative/zero weight, knapsack bucket size conflicts, missing tag weights, quota min > max, budget target > max)
- Input guard errors (null, empty string, malformed JSON, JSON literal `null`, invalid enum values)

**GREEN:** Updated `CupelJsonSerializer.Deserialize` and `DeserializeBudget`:
1. Empty string guard — throws `JsonException("JSON input cannot be empty.")`
2. Null result guard — throws `JsonException("Policy/Budget cannot be null. Received JSON literal 'null'.")`
3. `catch (ArgumentException ex)` — wraps constructor validation exceptions in `JsonException` with `$: {original message}` format
4. Preserved Plan 02's `catch (JsonException ex) when (ContainsUnknownScorerType(...))` for unknown scorer type errors

## Decisions

- **STJ does NOT wrap constructor exceptions in JsonException** — Contrary to the plan's assumption, .NET 10 STJ propagates ArgumentException/ArgumentOutOfRangeException directly from JsonConstructor-annotated constructors. The facade catches these and wraps them in JsonException.
- **Simple `$:` path prefix** — Since STJ doesn't provide path info when constructor exceptions propagate (no JsonException.Path), the facade uses a static `$:` prefix. The original exception message from domain constructors already contains parameter names and values, making errors actionable.
- **ArgumentException catches ArgumentOutOfRangeException** — Single catch block handles both since ArgumentOutOfRangeException inherits from ArgumentException.

## Deviations

1. **Plan assumed STJ wraps constructor exceptions** — The plan stated "STJ wraps constructor exceptions in JsonException during deserialization, and JsonException.Path provides the JSON path." This is incorrect for .NET 10. Constructor exceptions propagate unwrapped. The implementation catches ArgumentException directly instead of catching JsonException with an ArgumentException inner exception.

2. **Concurrent Plan 02 GREEN committed by background process** — A background process committed Plan 02's GREEN implementation (CupelJsonOptions scorer registration + unknown scorer type error handling) in commit `a1d720f` while Plan 03 was being executed. This required integrating both catch blocks (unknown scorer type as JsonException, validation as ArgumentException) in the correct order.

## Commits

- `14a58aa`: test(09-03): add failing tests for path-aware validation errors
- `bcf7025`: feat(09-03): add path-aware validation error handling for deserialization

## Verification

- `dotnet build` — zero errors, zero warnings
- `dotnet test --project tests/Wollax.Cupel.Json.Tests` — 38/38 pass (Plan 01 round-trip + Plan 02 custom scorer + Plan 03 validation)
- `dotnet test --project tests/Wollax.Cupel.Tests` — 534/534 pass (no regressions)

## Artifacts

| File | Lines | Purpose |
|------|-------|---------|
| `tests/Wollax.Cupel.Json.Tests/ValidationTests.cs` | 135 | Validation error tests with path-aware assertions |
| `tests/Wollax.Cupel.Json.Tests/ErrorMessageTests.cs` | 81 | Error message quality tests for edge cases |
| `src/Wollax.Cupel.Json/CupelJsonSerializer.cs` | 234 | Updated facade with error handling |
