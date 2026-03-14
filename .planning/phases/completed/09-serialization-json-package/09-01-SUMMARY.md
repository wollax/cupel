---
phase: 09-serialization-json-package
plan: 01
subsystem: serialization
tags: [json, stj, source-gen, aot, serialization]
dependency-graph:
  requires: [phase-08]
  provides: [Wollax.Cupel.Json package, CupelJsonSerializer, CupelJsonContext, round-trip tests]
  affects: [09-02, 09-03, phase-10]
tech-stack:
  added: []
  patterns: [source-generated JsonSerializerContext, facade pattern for serialization API]
key-files:
  created:
    - src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj
    - src/Wollax.Cupel.Json/CupelJsonContext.cs
    - src/Wollax.Cupel.Json/CupelJsonSerializer.cs
    - src/Wollax.Cupel.Json/CupelJsonOptions.cs
    - src/Wollax.Cupel.Json/PublicAPI.Shipped.txt
    - src/Wollax.Cupel.Json/PublicAPI.Unshipped.txt
    - tests/Wollax.Cupel.Json.Tests/Wollax.Cupel.Json.Tests.csproj
    - tests/Wollax.Cupel.Json.Tests/RoundTripTests.cs
  modified:
    - Cupel.slnx
    - src/Wollax.Cupel/ScorerType.cs
    - src/Wollax.Cupel/SlicerType.cs
    - src/Wollax.Cupel/PlacerType.cs
    - src/Wollax.Cupel/Diagnostics/OverflowStrategy.cs
    - src/Wollax.Cupel/ContextKind.cs
    - src/Wollax.Cupel/ContextBudget.cs
    - src/Wollax.Cupel/PublicAPI.Unshipped.txt
decisions:
  - "Made ContextKindJsonConverter and ContextKindDictionaryConverter public — required for cross-assembly source generation"
  - "Added ReadAsPropertyName/WriteAsPropertyName to ContextKindJsonConverter — required for Dictionary<ContextKind,T> serialization"
  - "Added [JsonStringEnumMemberName] to all enum members — explicit camelCase names for AOT-compatible source gen"
  - "CupelJsonSerializer uses separate overloads (no optional params) to satisfy RS0026 backcompat analyzer"
metrics:
  duration: ~8 minutes
  completed: 2026-03-14
---

# Phase 09 Plan 01: JSON Package Scaffold & Round-Trip Serialization Summary

Source-generated Wollax.Cupel.Json companion package with CupelJsonSerializer facade proving full-fidelity CupelPolicy and ContextBudget round-trips via AOT-compatible STJ source generation.

## What Was Done

### Task 1: Project Scaffold
- Created `Wollax.Cupel.Json.csproj` as a packable library with ProjectReference to `Wollax.Cupel`
- Created `Wollax.Cupel.Json.Tests.csproj` with TUnit and references to both JSON and core libraries
- Registered both projects in `Cupel.slnx`
- Included PublicApiAnalyzers with shipped/unshipped API tracking

### Task 2: CupelJsonContext & Serializer Facade
- `CupelJsonContext` — source-generated `JsonSerializerContext` with CamelCase naming, WhenWritingNull ignore, and string enum converter
- `CupelJsonSerializer` — public static facade with `Serialize`/`Deserialize` for `CupelPolicy` and `Serialize`/`DeserializeBudget` for `ContextBudget`
- `CupelJsonOptions` — options object with `WriteIndented` support (creates new context instance when indented)

### Task 3: Round-Trip Tests (TDD)
- 9 test cases covering:
  - Minimal policy round-trip (defaults preserved)
  - Full policy round-trip (all properties, kindWeights, tagWeights, quotas, metadata)
  - Policy with quotas (ContextKind, minPercent, maxPercent, custom kinds)
  - Enum camelCase verification (`"recency"`, `"greedy"`, `"uShaped"`, `"truncate"`)
  - Unknown JSON properties silently ignored (forward-compatibility)
  - Null optional fields omitted from serialized JSON
  - ContextBudget full config round-trip (reservedSlots with ContextKind keys)
  - ContextBudget empty reservedSlots round-trip
  - ContextBudget minimal config round-trip (defaults preserved)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] ContextKindJsonConverter and ContextKindDictionaryConverter made public**
- **Found during:** Task 2
- **Issue:** Source gen in Wollax.Cupel.Json could not access `internal` converters in Wollax.Cupel, producing SYSLIB1220 errors
- **Fix:** Changed both converters from `internal` to `public`, added to PublicAPI.Unshipped.txt
- **Files modified:** `src/Wollax.Cupel/ContextKind.cs`, `src/Wollax.Cupel/ContextBudget.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`

**2. [Rule 1 - Bug] Added ReadAsPropertyName/WriteAsPropertyName to ContextKindJsonConverter**
- **Found during:** Task 3 (RED phase)
- **Issue:** `IReadOnlyDictionary<ContextKind, double>` (ScorerEntry.KindWeights) failed serialization — STJ requires dictionary key converters to implement property name methods
- **Fix:** Added `ReadAsPropertyName` and `WriteAsPropertyName` overrides to `ContextKindJsonConverter`
- **Files modified:** `src/Wollax.Cupel/ContextKind.cs`

**3. [Rule 1 - Bug] Added [JsonStringEnumMemberName] to all enum members**
- **Found during:** Task 3 (RED phase)
- **Issue:** `UseStringEnumConverter = true` in source gen options produced PascalCase names (`"Recency"` instead of `"recency"`); naming policy does not apply to enum values in source-generated converters
- **Fix:** Added explicit `[JsonStringEnumMemberName("camelCase")]` attributes to all members of ScorerType, SlicerType, PlacerType, OverflowStrategy
- **Files modified:** `src/Wollax.Cupel/ScorerType.cs`, `src/Wollax.Cupel/SlicerType.cs`, `src/Wollax.Cupel/PlacerType.cs`, `src/Wollax.Cupel/Diagnostics/OverflowStrategy.cs`

**4. [Rule 3 - Blocking] CupelJsonSerializer overload pattern changed for RS0026 compliance**
- **Found during:** Task 2
- **Issue:** Multiple overloads with optional parameters violate PublicApiAnalyzers RS0026 backcompat requirement
- **Fix:** Changed from `options = null` default to separate overloads without defaults

## Test Results
- **New tests:** 9 passed (Wollax.Cupel.Json.Tests)
- **Existing tests:** 534 passed (Wollax.Cupel.Tests)
- **Total:** 543 passed, 0 failed

## Next Phase Readiness
Plan 09-02 (custom scorer registration and extensibility) can proceed. The CupelJsonContext and CupelJsonSerializer are ready to be extended with additional serializable types and custom converters.
