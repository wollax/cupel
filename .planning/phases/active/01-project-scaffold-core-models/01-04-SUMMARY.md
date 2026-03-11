---
phase: 01-project-scaffold-core-models
plan: 04
type: tdd
status: complete
started: 2026-03-11T11:00:00Z
completed: 2026-03-11T11:15:00Z
duration: ~15m
tasks_completed: 1/1
deviations: 1

dependency_graph:
  depends_on: ["01-02"]
  provides_to: ["01-05"]

tech:
  dotnet: "10"
  test_framework: "TUnit"
  serialization: "System.Text.Json"

files:
  created:
    - src/Wollax.Cupel/ContextBudget.cs
    - tests/Wollax.Cupel.Tests/Models/ContextBudgetTests.cs
  modified:
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt

decisions:
  - id: "01-04-D1"
    decision: "TUnit exception assertions use Throws<T>() / ThrowsExactly<T>() instead of ThrowsException().OfType<T>()"
    rationale: "ThrowsException().OfType<T>() does not exist in TUnit. The correct API is Throws<T>() for base-type matching and ThrowsExactly<T>() for exact type matching."

metrics:
  tests_added: 19
  tests_passing: 92
  warnings: 0
  errors: 0
---

# 01-04 SUMMARY: ContextBudget Validated Model

Implemented `ContextBudget` as a sealed class with constructor validation via TDD — the budget constraint model that controls how much context the pipeline can select. Includes a custom `JsonConverter` for `Dictionary<ContextKind, int>` keys since STJ cannot natively handle non-string dictionary keys.

## Task 1: TDD — ContextBudget validated model

| Step | Commit | Description |
|------|--------|-------------|
| RED | `b38bbb9` | Failing tests for ContextBudget (19 tests) |
| GREEN | `034fcf9` | Implement ContextBudget sealed class, custom converter, fix TUnit assertion API, update PublicAPI |

**Tests:** 19 covering minimal construction, full construction with all 5 parameters, 3 default value checks, 6 validation error paths (negative MaxTokens/TargetTokens/OutputReserve, margin < 0, margin > 100, TargetTokens > MaxTokens), 3 edge cases (zero/zero valid, margin boundaries), and 4 JSON tests (round-trip with all properties, round-trip with defaults, camelCase property names, ContextKind string keys in ReservedSlots).

## Deviations

1. **TUnit assertion API mismatch** — The plan suggested `ThrowsException().OfType<T>()` which does not exist in TUnit. The correct API is `ThrowsExactly<T>()` for exact exception type matching and `Throws<T>()` for base-type matching. Similarly, `HasCount().EqualTo(n)` is obsolete; replaced with direct `.Count` property assertion.

## Verification

All success criteria met:
- `ContextBudget` is a sealed class (not record) with constructor validation
- All 5 PIPE-02 properties present and read-only (MaxTokens, TargetTokens, OutputReserve, ReservedSlots, EstimationSafetyMarginPercent)
- Every invalid input path throws the appropriate exception type
- JSON round-trip works including ReservedSlots with ContextKind dictionary keys
- `[JsonConstructor]` enables deserialization through the validated constructor
- Custom `ContextKindDictionaryConverter` handles ContextKind as dictionary key serialization/deserialization
- Sensible defaults: OutputReserve=0, ReservedSlots=empty, EstimationSafetyMarginPercent=0
- Zero warnings under `TreatWarningsAsErrors`
- PublicAPI.Unshipped.txt updated with full API surface
