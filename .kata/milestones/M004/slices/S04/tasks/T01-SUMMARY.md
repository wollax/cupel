---
id: T01
parent: S04
milestone: M004
provides:
  - MatchSnapshot(name) method on SelectionReportAssertionChain with CallerFilePath-based path resolution
  - SnapshotSerializer internal class for stable JSON serialization of SelectionReport (camelCase, indented, enum strings)
  - SnapshotMismatchException with structured fields (snapshotName, snapshotPath, expected, actual)
  - CUPEL_UPDATE_SNAPSHOTS=1 env var support for in-place snapshot rewrite
  - Internal MatchSnapshotCore overload for testability without CallerFilePath interference
key_files:
  - src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs
  - src/Wollax.Cupel.Testing/SnapshotSerializer.cs
  - src/Wollax.Cupel.Testing/SnapshotMismatchException.cs
  - src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt
key_decisions:
  - "D119: MatchSnapshot uses internal MatchSnapshotCore for testability (CallerFilePath workaround)"
  - "D120: Snapshot JSON uses System.Text.Json with camelCase + enum strings + indented (no Cupel.Json dep)"
  - "SelectionReportAssertionException unsealed to allow SnapshotMismatchException inheritance"
patterns_established:
  - "Snapshot files stored at {callerDir}/__snapshots__/{name}.json — standard convention matching JS/Rust snapshot tools"
  - "Internal *Core method pattern for testing CallerFilePath-dependent methods"
observability_surfaces:
  - SnapshotMismatchException carries structured fields (SnapshotName, SnapshotPath, Expected, Actual) for programmatic inspection
duration: 12min
verification_result: passed
completed_at: 2026-03-23T13:30:00Z
blocker_discovered: false
---

# T01: Implement MatchSnapshot with JSON serialization and snapshot file I/O

**Added MatchSnapshot assertion to SelectionReportAssertionChain with JSON serialization, CallerFilePath path resolution, SnapshotMismatchException, and CUPEL_UPDATE_SNAPSHOTS env var support**

## What Happened

Created three new files in `Wollax.Cupel.Testing`:

**SnapshotSerializer.cs** — Internal static class configuring `System.Text.Json` with `JsonNamingPolicy.CamelCase`, `WriteIndented = true`, `JsonStringEnumConverter(CamelCase)`, and `WhenWritingNull` ignore. Serializes `SelectionReport` to stable, deterministic JSON suitable for snapshot comparison.

**SnapshotMismatchException.cs** — Public sealed exception inheriting from `SelectionReportAssertionException`. Carries `SnapshotName`, `SnapshotPath`, `Expected`, and `Actual` properties. Message truncates expected/actual to 500 chars each for readability.

**MatchSnapshot + MatchSnapshotCore** — Added to `SelectionReportAssertionChain`. Public `MatchSnapshot(string name, [CallerFilePath] string callerFilePath)` delegates to internal `MatchSnapshotCore(string name, string callerFilePath)`. Logic: resolve path to `{callerDir}/__snapshots__/{name}.json`; create directory + write if file missing; compare string equality if file exists; throw `SnapshotMismatchException` on mismatch unless `CUPEL_UPDATE_SNAPSHOTS=1`.

Unsealed `SelectionReportAssertionException` to enable `SnapshotMismatchException` inheritance. Updated `PublicAPI.Unshipped.txt` with all new public surface (MatchSnapshot method, SnapshotMismatchException class + constructor + 4 properties).

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings (14 projects)
- `dotnet test --configuration Release` — 772 passed, 0 failed
- `grep -c "MatchSnapshot" PublicAPI.Unshipped.txt` → 1
- `grep -c "SnapshotMismatchException" PublicAPI.Unshipped.txt` → 6

## Diagnostics

- `SnapshotMismatchException.SnapshotPath` shows the exact file path for manual inspection
- `SnapshotMismatchException.Expected` / `.Actual` carry full JSON for diff tools
- Exception message includes truncated expected/actual inline for test runner output

## Deviations

- Unsealed `SelectionReportAssertionException` (was `sealed class`, now `class`) to allow `SnapshotMismatchException` to inherit. This is a non-breaking additive change since nothing is shipped yet.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel.Testing/SnapshotSerializer.cs` — Internal static JSON serializer for SelectionReport
- `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs` — Public exception with structured diff fields
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — Added MatchSnapshot + MatchSnapshotCore methods
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — Unsealed (removed `sealed` keyword)
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — New entries for MatchSnapshot and SnapshotMismatchException
