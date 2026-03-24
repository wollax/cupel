---
estimated_steps: 5
estimated_files: 5
---

# T01: Implement MatchSnapshot with JSON serialization and snapshot file I/O

**Slice:** S04 — Snapshot testing in Cupel.Testing
**Milestone:** M004

## Description

Implement the core snapshot assertion method on `SelectionReportAssertionChain`, including JSON serialization of `SelectionReport`, snapshot file path resolution via `[CallerFilePath]`, the `CUPEL_UPDATE_SNAPSHOTS=1` env var support, and a dedicated `SnapshotMismatchException`. This task delivers the full implementation; T02 adds tests.

## Steps

1. Create `src/Wollax.Cupel.Testing/SnapshotSerializer.cs` — an `internal static` class with a `Serialize(SelectionReport)` method returning indented JSON. Configure `JsonSerializerOptions` with camelCase naming policy, `JsonStringEnumConverter` for all enums, and `WriteIndented = true`. Handle all nested types: `ContextItem` (has `[JsonPropertyName]` attributes), `IncludedItem`, `ExcludedItem`, `TraceEvent`, `CountRequirementShortfall`, enums (`ExclusionReason`, `InclusionReason`, `PipelineStage`, `ContextKind`, `ContextSource`). The serializer must produce stable, deterministic JSON for snapshot comparison.

2. Create `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs` — a `public sealed class` inheriting from `SelectionReportAssertionException`. Constructor takes `string snapshotName, string snapshotPath, string expected, string actual`. Message format: `"MatchSnapshot(\"{name}\") failed: snapshot mismatch at {path}.\n\nExpected:\n{expected}\n\nActual:\n{actual}"`. Truncate expected/actual to first 500 chars each if longer.

3. Add `MatchSnapshot` to `SelectionReportAssertionChain` as two overloads: a `public` method `MatchSnapshot(string name, [CallerFilePath] string callerFilePath = "")` that delegates to an `internal` method `MatchSnapshotCore(string name, string callerFilePath)`. The internal overload enables tests to pass explicit temp-directory paths without `[CallerFilePath]` interference. Logic in `MatchSnapshotCore`:
   - Resolve snapshot path: `Path.Combine(Path.GetDirectoryName(callerFilePath)!, "__snapshots__", $"{name}.json")`
   - Serialize the current report via `SnapshotSerializer.Serialize(_report)`
   - If snapshot file does not exist: create directory if needed, write serialized JSON, return `this`
   - If snapshot file exists: read existing content
     - If content matches serialized JSON (string equality): return `this`
     - If `Environment.GetEnvironmentVariable("CUPEL_UPDATE_SNAPSHOTS") == "1"`: overwrite file, return `this`
     - Otherwise: throw `SnapshotMismatchException` with snapshot name, path, existing content (expected), serialized JSON (actual)

4. Update `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` with:
   - `Wollax.Cupel.Testing.SelectionReportAssertionChain.MatchSnapshot(string! name, string! callerFilePath = "") -> Wollax.Cupel.Testing.SelectionReportAssertionChain!`
   - `Wollax.Cupel.Testing.SnapshotMismatchException`
   - `Wollax.Cupel.Testing.SnapshotMismatchException.SnapshotMismatchException(string! snapshotName, string! snapshotPath, string! expected, string! actual) -> void`

5. Verify: `dotnet build --configuration Release` — 0 errors, 0 warnings across all 14 projects.

## Must-Haves

- [ ] `SnapshotSerializer.Serialize(SelectionReport)` produces stable indented JSON with camelCase + enum strings
- [ ] `SnapshotMismatchException` inherits from `SelectionReportAssertionException` with snapshot name, path, expected, and actual in message
- [ ] `MatchSnapshot` resolves path via `[CallerFilePath]` + `__snapshots__/{name}.json`
- [ ] First call with no existing file creates the snapshot
- [ ] Matching file returns without throwing
- [ ] Mismatched file throws `SnapshotMismatchException`
- [ ] `CUPEL_UPDATE_SNAPSHOTS=1` overwrites instead of throwing
- [ ] `PublicAPI.Unshipped.txt` updated
- [ ] `dotnet build --configuration Release` — 0 errors, 0 warnings

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `grep -c "MatchSnapshot" src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — returns 1
- `grep -c "SnapshotMismatchException" src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — returns ≥1

## Observability Impact

- Signals added/changed: `SnapshotMismatchException` carries structured fields (snapshotName, snapshotPath, expected, actual) for programmatic inspection
- How a future agent inspects this: read the exception message or catch `SnapshotMismatchException` to access snapshot path and diff
- Failure state exposed: mismatch exception includes full expected/actual JSON for diagnosis

## Inputs

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — existing chain to extend
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — base exception to inherit from
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — the type being serialized
- D106 (JSON format, CUPEL_UPDATE_SNAPSHOTS=1 env var)
- D107 (Rust out of scope — .NET only)
- D041 (no FluentAssertions dependency — superseded for snapshot by D106)

## Expected Output

- `src/Wollax.Cupel.Testing/SnapshotSerializer.cs` — JSON serialization for SelectionReport
- `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs` — dedicated exception with diff output
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — `MatchSnapshot` method added
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — new entries for MatchSnapshot and SnapshotMismatchException
