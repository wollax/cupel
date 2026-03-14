---
phase: 08-policy-system-named-presets
plan: 01
subsystem: policy-data-types
tags: [enums, sealed-class, validation, tdd, json-serialization]
dependency-graph:
  requires: [phase-01, phase-02, phase-03, phase-05, phase-06, phase-07]
  provides: [ScorerType, SlicerType, PlacerType, ScorerEntry, QuotaEntry, CupelPolicy]
  affects: [08-02, 08-03, phase-09]
tech-stack:
  added: []
  patterns: [sealed-class-constructor-validation, defensive-copy, json-property-name-attributes]
key-files:
  created:
    - src/Wollax.Cupel/ScorerType.cs
    - src/Wollax.Cupel/SlicerType.cs
    - src/Wollax.Cupel/PlacerType.cs
    - src/Wollax.Cupel/ScorerEntry.cs
    - src/Wollax.Cupel/Slicing/QuotaEntry.cs
    - src/Wollax.Cupel/CupelPolicy.cs
    - tests/Wollax.Cupel.Tests/Policy/ScorerEntryTests.cs
    - tests/Wollax.Cupel.Tests/Policy/QuotaEntryTests.cs
    - tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs
  modified:
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt
decisions:
  - id: 08-01-D1
    decision: "ScorerEntry uses IReadOnlyDictionary for KindWeights and TagWeights with defensive copies"
    rationale: "Matches ContextBudget pattern — prevent mutation after construction"
  - id: 08-01-D2
    decision: "CupelPolicy stores Scorers and Quotas as IReadOnlyList with collection expression spread copies"
    rationale: "Idiomatic C# 12+ defensive copy via [..source]"
metrics:
  duration: ~4min
  completed: 2026-03-14
---

# Phase 08 Plan 01: Core Policy Data Types Summary

Sealed classes with constructor validation for declarative pipeline configuration, following the ContextBudget pattern with [JsonPropertyName] attributes for Phase 9 serialization readiness.

## What Was Built

### Enums (3 files)
- **ScorerType** — 6 values: Recency, Priority, Kind, Tag, Frequency, Reflexive
- **SlicerType** — 2 values: Greedy, Knapsack
- **PlacerType** — 2 values: Chronological, UShaped

### Config Descriptors (2 files)
- **ScorerEntry** — sealed class with weight validation (positive, finite), TagWeights required for Tag type, defensive copies of dictionaries
- **QuotaEntry** — sealed class with min/max percent validation (0-100 range, at least one required, min <= max), in `Wollax.Cupel.Slicing` namespace

### Policy Object (1 file)
- **CupelPolicy** — sealed class tying everything together: scorers (non-empty), slicer/placer type, deduplication, overflow strategy, knapsack bucket size (only valid with Knapsack slicer), optional quotas, name, and description

## Test Coverage

40 tests across 3 test files:
- **ScorerEntryTests** (14 tests) — valid construction for all types, weight validation, Tag-specific validation, defensive copies
- **QuotaEntryTests** (11 tests) — min/max only, both, boundary values, all validation error paths
- **CupelPolicyTests** (15 tests) — minimal/full construction, defaults, null/empty scorers, knapsack validation, defensive copies

## Deviations from Plan

None — plan executed exactly as written.

## Verification

- `dotnet build --warnaserror` — zero warnings
- `dotnet test` — 40/40 tests pass
- PublicAPI.Unshipped.txt updated with all new public surface

## Next Phase Readiness

Plan 08-02 (PolicyBuilder) can proceed — all data types it depends on are in place.
