---
phase: 01-project-scaffold-core-models
plan: 02
type: tdd
status: complete
started: 2026-03-11T09:31:03Z
completed: 2026-03-11T09:36:50Z
duration: ~6m
tasks_completed: 2/2
deviations: 1

dependency_graph:
  depends_on: ["01-01"]
  provides_to: ["01-03", "01-04", "01-05"]

tech:
  dotnet: "10"
  test_framework: "TUnit"
  serialization: "System.Text.Json"

files:
  created:
    - src/Wollax.Cupel/ContextKind.cs
    - src/Wollax.Cupel/ContextSource.cs
    - tests/Wollax.Cupel.Tests/Models/ContextKindTests.cs
    - tests/Wollax.Cupel.Tests/Models/ContextSourceTests.cs
  modified:
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt

decisions:
  - id: "01-02-D1"
    decision: "STJ returns null for JSON literal `null` on reference types (converter not invoked) — test asserts null return rather than JsonException"
    rationale: "System.Text.Json handles null before calling custom converter Read method for reference types"

metrics:
  tests_added: 54
  tests_passing: 55
  warnings: 0
  errors: 0
---

# 01-02 SUMMARY: Smart Enums (ContextKind, ContextSource)

Implemented two extensible type-safe smart enums via TDD — `ContextKind` (5 well-known values) and `ContextSource` (3 well-known values) — with case-insensitive equality, `IEquatable<T>`, operator overloads, and plain-string JSON serialization via custom `JsonConverter<T>`.

## Task 1: TDD — ContextKind smart enum

| Step | Commit | Description |
|------|--------|-------------|
| RED | `d5654e5` | Failing tests for ContextKind (28 tests) |
| GREEN | `8006963` | Implement ContextKind sealed class + ContextKindJsonConverter |

**Tests:** 28 covering well-known values, case-insensitive equality, GetHashCode consistency, operator overloads, ToString, constructor validation, custom kinds, JSON serialize/deserialize/round-trip.

## Task 2: TDD — ContextSource smart enum

| Step | Commit | Description |
|------|--------|-------------|
| RED | `420f77f` | Failing tests for ContextSource (26 tests) |
| GREEN | `23f168a` | Implement ContextSource sealed class + ContextSourceJsonConverter |

**Tests:** 26 covering the same patterns as ContextKind with ContextSource-specific values (Chat, Tool, Rag).

## Deviations

1. **STJ null handling** — The plan's context suggested testing that `Deserialize<ContextKind>("null")` throws `JsonException`. In practice, STJ returns `null` for JSON literal `null` on reference types without invoking the custom converter. Tests were adjusted to assert `null` return instead.

## Verification

All success criteria met:
- Zero warnings under `TreatWarningsAsErrors`
- Case-insensitive equality works across all test cases
- JSON round-trip preserves equality for both types
- Custom user-defined values construct and compare correctly
- Constructor rejects null, empty, and whitespace input
- PublicAPI.Unshipped.txt updated with full API surface
