# S04: Snapshot testing in Cupel.Testing — Research

**Date:** 2026-03-23
**Domain:** .NET testing infrastructure, JSON serialization, file I/O
**Confidence:** HIGH

## Summary

S04 adds snapshot assertion methods to `Wollax.Cupel.Testing` that serialize `SelectionReport` to JSON, compare against stored `.json` files, and support `CUPEL_UPDATE_SNAPSHOTS=1` for in-place updates. The implementation is straightforward — `System.Text.Json` is already used throughout the Cupel core for `ContextItem` and `ContextBudget`; extending it to diagnostic types is additive.

The main technical challenge is JSON serialization of `SelectionReport` and its nested types (`IncludedItem`, `ExcludedItem`, `TraceEvent`, `CountRequirementShortfall`). These diagnostic types currently have NO `[JsonPropertyName]` attributes (unlike `ContextItem` which is fully annotated). The serializer must handle `ContextKind` (custom `JsonConverter` already exists), `ContextSource` (custom converter exists), `ExclusionReason`/`InclusionReason`/`PipelineStage` (plain enums — string converter needed), and `IReadOnlyDictionary<string, object?>` metadata on `ContextItem`.

Snapshot file path resolution uses `[CallerFilePath]` to locate the test source file and place `.json` snapshots alongside it. This is the standard .NET pattern and requires no external dependencies.

## Recommendation

Use `System.Text.Json` with `JsonSerializerOptions` configured for camelCase, indented output, and string enum conversion. Do NOT use the existing `CupelJsonContext` source-gen context (it's in `Wollax.Cupel.Json` and only covers `CupelPolicy`/`ContextBudget`). Instead, configure `JsonSerializerOptions` directly in the snapshot code — the testing package does not need AOT compatibility.

The `MatchSnapshot` method should:
1. Serialize the actual `SelectionReport` to indented JSON
2. Resolve snapshot path from `[CallerFilePath]` + snapshot name
3. If file doesn't exist: create it (first run)
4. If `CUPEL_UPDATE_SNAPSHOTS=1`: overwrite the file
5. Otherwise: read expected JSON, compare strings (or deserialize and use equality)

String comparison of normalized JSON is simpler and avoids deserialization edge cases. Serialize both actual and expected with the same options for deterministic comparison.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| JSON serialization of ContextItem | `System.Text.Json` + existing `[JsonPropertyName]` attrs + `ContextKindJsonConverter` + `ContextSourceJsonConverter` | Already on `ContextItem`; reuse the converters |
| Enum-to-string serialization | `JsonStringEnumConverter` in `System.Text.Json` | Built-in; no custom code needed for `ExclusionReason`, `InclusionReason`, `PipelineStage` |
| Snapshot file diffing | String comparison of normalized JSON | Sufficient for deterministic pipelines; no need for semantic JSON diff library |

## Existing Code and Patterns

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — 13 assertion patterns; `MatchSnapshot` will be pattern 14, following the same return-`this` chain pattern
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — `Should()` entry point; no changes needed
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — Throw on mismatch; may want a `SnapshotMismatchException` subclass or reuse this
- `src/Wollax.Cupel/ContextItem.cs` — Full `[JsonPropertyName]` annotations on all properties; `ContextKindJsonConverter` and `ContextSourceJsonConverter` handle custom types
- `src/Wollax.Cupel/ContextBudget.cs` — `ContextKindDictionaryConverter` for `IReadOnlyDictionary<ContextKind, int>`
- `src/Wollax.Cupel.Json/CupelJsonContext.cs` — Source-gen context for `CupelPolicy`/`ContextBudget` only; NOT reusable for snapshot serialization (wrong type set, lives in separate package)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — sealed record with `IEquatable<SelectionReport>`; no JSON annotations
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — sealed record; no JSON annotations
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — sealed record with nullable `DeduplicatedAgainst`; no JSON annotations
- `src/Wollax.Cupel/Diagnostics/TraceEvent.cs` — readonly record struct; `Duration` is `TimeSpan`, `Message` is nullable
- `src/Wollax.Cupel/Diagnostics/CountRequirementShortfall.cs` — positional record `(Kind, RequiredCount, SatisfiedCount)`
- `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` — Existing test patterns using direct `SelectionReport` construction (D091)

## Constraints

- **No new package dependencies on `Wollax.Cupel.Testing`** — `System.Text.Json` is part of the .NET BCL (net10.0), no NuGet reference needed
- **`Wollax.Cupel.Testing` gains `System.IO` usage** — acceptable for a testing package (noted in M004-CONTEXT.md)
- **Diagnostic types have no `[JsonPropertyName]` attributes** — serializer must use reflection-based naming (camelCase policy) or we add attributes. Adding `[JsonPropertyName]` to diagnostic types in `Wollax.Cupel` core is an option but adds `System.Text.Json` coupling to the diagnostics namespace. Better to rely on `JsonSerializerOptions.PropertyNamingPolicy = CamelCase` in the snapshot serializer.
- **`ContextKind` has a custom `JsonConverter`** — already works with `System.Text.Json`; snapshot serializer inherits it automatically
- **`ContextSource` has a custom `JsonConverter`** — same; inherited automatically
- **`IReadOnlyDictionary<string, object?>` metadata** — `System.Text.Json` serializes `object?` values as `JsonElement` on round-trip. This means deserialized metadata won't have `double` values — they'll be `JsonElement`. This affects equality comparison if we deserialize-and-compare. **String comparison sidesteps this entirely.**
- **`TimeSpan` serialization** — `System.Text.Json` in .NET 10 serializes `TimeSpan` as ISO 8601 duration string by default. Verify format is stable and readable.
- **PublicAPI analyzers** — new public methods on `SelectionReportAssertionChain` must be added to `PublicAPI.Unshipped.txt`
- **Boundary map contract** — S04 produces `MatchSnapshot(string name)` method; consumes S01's `SelectionReport` equality

## Common Pitfalls

- **Metadata round-trip fidelity** — `IReadOnlyDictionary<string, object?>` with mixed value types (`double`, `string`, `null`) deserializes all values as `JsonElement` when using `System.Text.Json` reflection. If we deserialize the snapshot JSON back to `SelectionReport` and use `==` for comparison, metadata equality will fail. **Solution: compare serialized JSON strings, not deserialized objects.** Normalize both by serializing actual report and reading expected file, then compare text.
- **Snapshot path resolution across different test runners** — `[CallerFilePath]` returns the absolute source path at compile time. Different machines have different paths, but the snapshot file is *relative* to the test file — compute the directory from `CallerFilePath` and append the snapshot name. The snapshot `.json` file lives in the source tree, not in `bin/`.
- **Line ending differences (Windows vs Unix)** — Normalize line endings before comparison, or use `JsonSerializerOptions` that produce consistent output. `System.Text.Json` uses `\n` in indented output on all platforms in .NET 6+.
- **Snapshot name collisions** — Two tests calling `MatchSnapshot("same-name")` from different files produce different paths (different `CallerFilePath`). Two tests in the same file with the same name would collide. Document that names must be unique within a test file.
- **First-run behavior** — On first run, snapshot file doesn't exist. The method should create it and pass (not fail). This matches `insta` crate behavior and the spec requirement.

## Open Risks

- **Enum string representation stability** — If `ExclusionReason` or `InclusionReason` enum members are renamed in a future version, all snapshots break. This is inherent to snapshot testing and acceptable — `CUPEL_UPDATE_SNAPSHOTS=1` is the recovery mechanism.
- **`ContextItem.Metadata` value types** — If a caller stores a `double` in metadata, serialization produces `1.5`; if they store a `string`, it produces `"1.5"`. The snapshot captures the exact runtime type. Changing metadata value types between test runs would cause snapshot mismatches. This is correct behavior (the snapshot should capture the exact state).

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET / C# | n/a | Core language — no skill needed |
| System.Text.Json | n/a | BCL — well-documented, no skill needed |

No external skills are relevant — this is pure .NET BCL work with no third-party dependencies.

## Sources

- `src/Wollax.Cupel/ContextItem.cs` — existing JSON annotation pattern (source: codebase)
- `src/Wollax.Cupel/ContextKind.cs` — custom JsonConverter pattern (source: codebase)
- D106: JSON format with `CUPEL_UPDATE_SNAPSHOTS=1` env var (source: decisions register)
- D107: Rust snapshot testing out of scope (source: decisions register)
- D041: No FluentAssertions dependency (source: decisions register)
- D091: Direct SelectionReport construction for test data (source: decisions register)
- R053: Active requirement — snapshot testing in Cupel.Testing (source: requirements)
