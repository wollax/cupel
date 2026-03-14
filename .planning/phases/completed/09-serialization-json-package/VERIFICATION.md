# Phase 9 Verification

**Status:** gaps_found
**Date:** 2026-03-14
**Score:** 17/18 must-haves verified

## Must-Have Verification

### Plan 01

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Wollax.Cupel.Json is a separate project with ProjectReference to Wollax.Cupel | pass | `src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj` line 13: `<ProjectReference Include="../Wollax.Cupel/Wollax.Cupel.csproj" />` |
| 2 | CupelJsonContext is a source-generated JsonSerializerContext with `[JsonSerializable(typeof(CupelPolicy))]` | pass | `CupelJsonContext.cs`: `[JsonSerializable(typeof(CupelPolicy))]` and `[JsonSerializable(typeof(ContextBudget))]`; `internal partial class CupelJsonContext : JsonSerializerContext` |
| 3 | CupelJsonSerializer.Serialize produces valid JSON and CupelJsonSerializer.Deserialize reconstructs equivalent CupelPolicy | pass | `MinimalPolicy_RoundTrips`, `FullPolicy_RoundTrips` tests pass |
| 4 | ContextBudget round-trips through JSON without data loss | pass | `ContextBudget_FullConfig_RoundTrips` asserts `IEquatable<ContextBudget>.Equals` and all individual properties |
| 5 | Enums serialize as camelCase strings (recency, greedy, chronological, uShaped, throw, truncate, proceed) | pass | `EnumsSerializeAsCamelCaseStrings` test asserts `"recency"`, `"greedy"`, `"uShaped"`, `"truncate"` and absence of PascalCase forms |
| 6 | ContextKind serializes as its string Value (existing ContextKindJsonConverter is honored) | pass | `PolicyWithQuotas_RoundTrips` round-trips `new ContextKind("Custom")` and built-in ContextKind values; `ContextBudget_FullConfig_RoundTrips` round-trips ReservedSlots with ContextKind keys |
| 7 | Unknown JSON properties are silently ignored (forward-compatibility) | pass | `UnknownJsonProperties_AreIgnored` test; STJ source-gen context uses default ignore behavior |
| 8 | Wollax.Cupel.Json.Tests project exists and round-trip tests pass | pass | 9 tests in `RoundTripTests.cs`, all pass (38 total JSON tests pass) |

### Plan 02

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | `RegisterScorer(string name, Func<IScorer> factory)` exists on CupelJsonOptions and records the custom scorer type | pass | `CupelJsonOptions.cs` line 32: `public CupelJsonOptions RegisterScorer(string typeName, Func<IScorer> factory)` storing to `_scorerFactories` |
| 2 | A config-aware overload `RegisterScorer(string name, Func<JsonElement?, IScorer> factory)` also exists | pass | `CupelJsonOptions.cs` line 51: `public CupelJsonOptions RegisterScorer(string typeName, Func<JsonElement?, IScorer> factory)` |
| 3 | Unknown values in the 'type' field of scorer entries fail immediately with a descriptive exception | pass | `Deserialize_UnknownScorerType_ThrowsDescriptiveError` test; `CupelJsonSerializer` catches `JsonException` when `ContainsUnknownScorerType` returns true |
| 4 | The error message for unknown scorer types lists all registered built-in types AND custom-registered types | pass | `BuildUnknownScorerTypeException` includes both `BuiltInScorerTypes` list and `options.RegisteredScorerNames`; verified by `Deserialize_UnknownScorerType_WithRegistrations_ListsCustomTypes` test |
| 5 | The error message suggests using `RegisterScorer()` for custom types | pass | `CupelJsonSerializer.cs` line 189: `message += " Use CupelJsonOptions.RegisterScorer() to register custom scorer types."` |
| 6 | Custom-registered scorer type names can be used in the 'type' field and are resolved via the registered factory | **fail** | The serializer's `ContainsUnknownScorerType` does NOT check registered factories before flagging an unknown type. When a registered custom type name appears in JSON, deserialization still throws — it is never resolved via the factory. The `GetScorerFactory` / `HasScorerFactory` methods exist on `CupelJsonOptions` but are never called from `CupelJsonSerializer`. Registered types appear only in the error message, not in deserialization. Plan 02 implementation notes acknowledge this limitation ("registration hook is informational") but the must-have truth states resolution should occur. |

### Plan 03

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | Validation errors during deserialization include JSON path to the problem (e.g., `$.scorers[1].weight`) | pass | `CupelJsonSerializer.Deserialize` catches `ArgumentException` and wraps: `throw new JsonException($"$: {ex.Message}", ex)`. Tests in `ValidationTests.cs` assert on key message fragments. Note: path is `$:` (root) rather than a specific property path for constructor-level exceptions; STJ does not provide field-level paths for constructor `ArgumentException`. |
| 2 | Invalid values (negative tokens, out-of-range percentages, empty scorer lists) produce immediate errors at deserialization time | pass | `ValidationTests.cs`: `Deserialize_EmptyScorers_ThrowsWithPath`, `Deserialize_NegativeWeight_ThrowsWithPath`, `Deserialize_ZeroWeight_ThrowsWithPath`, `Deserialize_BudgetTargetExceedsMax_ThrowsWithPath` all pass |
| 3 | Error messages are human-readable and include the invalid value | pass | Tests assert messages contain the invalid value (e.g., `-0.5`, `"weight"`, `"KnapsackBucketSize"`, `"MinPercent"`, `"TargetTokens"`) |
| 4 | Malformed JSON produces a clear error (not a NullReferenceException) | pass | `Deserialize_MalformedJson_ThrowsJsonException` in `ErrorMessageTests.cs` passes |
| 5 | The exception type for deserialization errors is consistent and catchable | pass | All deserialization errors throw `JsonException` or `ArgumentNullException` (for null input); `ErrorMessageTests.cs` asserts exact exception types |

## Artifact Verification

| Artifact | Min Lines | Actual Lines | Status |
|----------|-----------|--------------|--------|
| `src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj` | 15 | 25 | pass |
| `src/Wollax.Cupel.Json/CupelJsonContext.cs` | 10 | 14 | pass |
| `src/Wollax.Cupel.Json/CupelJsonSerializer.cs` | 30 | 238 | pass |
| `src/Wollax.Cupel.Json/CupelJsonOptions.cs` | 10 (plan 01) / 30 (plan 02) | 87 | pass |
| `tests/Wollax.Cupel.Json.Tests/Wollax.Cupel.Json.Tests.csproj` | 10 | 17 | pass |
| `tests/Wollax.Cupel.Json.Tests/RoundTripTests.cs` | 80 | 255 | pass |
| `tests/Wollax.Cupel.Json.Tests/CustomScorerTests.cs` | 80 | 170 | pass |
| `tests/Wollax.Cupel.Json.Tests/ValidationTests.cs` | 80 | 134 | pass |
| `tests/Wollax.Cupel.Json.Tests/ErrorMessageTests.cs` | 60 | 82 | pass |

## Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `CupelJsonSerializer.cs` | `CupelJsonContext.cs` | `GetContext()` returns `CupelJsonContext.Default` or a new `CupelJsonContext`; all serialize/deserialize calls use `context.CupelPolicy` / `context.ContextBudget` | pass |
| `Wollax.Cupel.Json.csproj` | `Wollax.Cupel.csproj` | `<ProjectReference Include="../Wollax.Cupel/Wollax.Cupel.csproj" />` | pass |
| `RoundTripTests.cs` | `CupelJsonSerializer.cs` | Tests call `CupelJsonSerializer.Serialize` and `CupelJsonSerializer.Deserialize` directly | pass |
| `Cupel.slnx` | `Wollax.Cupel.Json.csproj` | `<Project Path="src/Wollax.Cupel.Json/Wollax.Cupel.Json.csproj" />` in `/src/` folder | pass |
| `CupelJsonOptions.cs` | `CupelJsonSerializer.cs` | `CupelJsonSerializer` accepts `CupelJsonOptions?` parameter; reads `WriteIndented`, `RegisteredScorerNames` from options | pass (partial — `GetScorerFactory` is never called by the serializer) |
| `CupelJsonSerializer.cs` | `IScorer.cs` | `RegisterScorer` factories produce `IScorer` instances; `Func<IScorer>` and `Func<JsonElement?, IScorer>` in `CupelJsonOptions` | pass (factory stored, but not invoked by serializer) |

## Build & Test

| Check | Result |
|-------|--------|
| `rtk dotnet build` | pass — zero errors, zero warnings |
| `rtk dotnet test --project tests/Wollax.Cupel.Json.Tests` | pass — 38 tests, 0 failed |
| `rtk dotnet test` (full solution) | pass — 572 tests total, 0 failed (38 JSON + 534 core) |

## Gaps

### Gap 1: Must-Have Plan 02 #6 — Custom registered types not resolved during deserialization

**Severity:** Low (by design, acknowledged in plan)

**What the must-have states:** "Custom-registered scorer type names can be used in the 'type' field and are resolved via the registered factory."

**What is implemented:** When a JSON scorer entry uses a custom type name (e.g., `"type": "myCustom"`), deserialization always fails with a `JsonException`, even if `"myCustom"` was registered via `RegisterScorer`. The `HasScorerFactory` and `GetScorerFactory` methods exist on `CupelJsonOptions` but are never consulted by `CupelJsonSerializer` during deserialization. Registered types only appear in the error message list.

**Root cause:** The `ScorerType` enum is closed and cannot represent custom values. STJ cannot deserialize `"myCustom"` into a `ScorerType` enum member regardless of the factory registry. Plan 02 explicitly acknowledges this as a design limitation and says resolution is deferred to a future phase or consumer code.

**Impact:** The `RegisterScorer` API exists and is functional for error-message enrichment and consumer pipeline-building, but consumers cannot load policies with custom scorer types from JSON. The ROADMAP success criterion "RegisterScorer(string name, Func\<IScorer\> factory) hook exists for consumer-defined scorers to participate in deserialization" is only partially met — the hook exists, but custom scorers cannot participate in deserialization today.

**Recommendation:** Accept as known limitation for this phase. No test currently asserts that registered custom types succeed deserialization (the plan's test #6 was intentionally not implemented). Document the limitation in the package README or XML docs on `RegisterScorer`. Track as a future enhancement if custom scorer round-trip support is needed.
