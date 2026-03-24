---
id: S04
parent: M004
milestone: M004
provides:
  - MatchSnapshot(name) method on SelectionReportAssertionChain with CallerFilePath-based path resolution
  - SnapshotSerializer internal class for stable JSON serialization of SelectionReport (camelCase, indented, enum strings)
  - SnapshotMismatchException with structured fields (snapshotName, snapshotPath, expected, actual)
  - CUPEL_UPDATE_SNAPSHOTS=1 env var support for in-place snapshot rewrite
  - 5 snapshot lifecycle tests proving create→match→fail→update→no-update cycle
requires:
  - slice: S01
    provides: SelectionReport equality (enables meaningful snapshot comparison)
affects:
  - S05
key_files:
  - src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs
  - src/Wollax.Cupel.Testing/SnapshotSerializer.cs
  - src/Wollax.Cupel.Testing/SnapshotMismatchException.cs
  - src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt
  - src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj
  - tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs
key_decisions:
  - "D119: MatchSnapshot uses internal MatchSnapshotCore for testability (CallerFilePath workaround)"
  - "D120: Snapshot JSON uses System.Text.Json with camelCase + enum strings + indented (no Cupel.Json dep)"
  - "D121: Snapshot tests use [NotInParallel] due to process-global CUPEL_UPDATE_SNAPSHOTS env var"
  - "SelectionReportAssertionException unsealed to allow SnapshotMismatchException inheritance"
patterns_established:
  - "Snapshot files stored at {callerDir}/__snapshots__/{name}.json — standard convention matching JS/Rust snapshot tools"
  - "Internal *Core method pattern for testing CallerFilePath-dependent methods"
  - "Temp-directory isolation pattern for file I/O tests with per-test GUIDs and finally-block cleanup"
observability_surfaces:
  - SnapshotMismatchException carries structured fields (SnapshotName, SnapshotPath, Expected, Actual) for programmatic inspection
drill_down_paths:
  - .kata/milestones/M004/slices/S04/tasks/T01-SUMMARY.md
  - .kata/milestones/M004/slices/S04/tasks/T02-SUMMARY.md
duration: 27min
verification_result: passed
completed_at: 2026-03-23T14:15:00Z
---

# S04: Snapshot testing in Cupel.Testing

**Added MatchSnapshot assertion to Wollax.Cupel.Testing with JSON serialization, CallerFilePath path resolution, SnapshotMismatchException, CUPEL_UPDATE_SNAPSHOTS env var, and 5 lifecycle tests proving the full create→match→fail→update cycle**

## What Happened

Two tasks delivered snapshot testing as a new capability in the `Wollax.Cupel.Testing` package.

**T01 (Implementation):** Created `SnapshotSerializer` (internal static class using `System.Text.Json` with camelCase, indented, enum string converter), `SnapshotMismatchException` (public sealed, inherits from unsealed `SelectionReportAssertionException`, carries `SnapshotName`, `SnapshotPath`, `Expected`, `Actual` fields), and `MatchSnapshot` + `MatchSnapshotCore` methods on `SelectionReportAssertionChain`. The public method uses `[CallerFilePath]` for automatic path resolution; the internal overload accepts explicit paths for testability. Snapshot logic: create if missing, compare string equality if exists, throw on mismatch unless `CUPEL_UPDATE_SNAPSHOTS=1`. Updated `PublicAPI.Unshipped.txt`.

**T02 (Verification):** Created 5 lifecycle tests in `SnapshotTests.cs` exercising create (file written with valid JSON), match (identical report passes), fail (different report throws `SnapshotMismatchException` with correct fields), update (env var overwrites stale snapshot), and no-update (without env var, mismatch throws and file is unchanged). Added `InternalsVisibleTo` to csproj. Applied `[NotInParallel]` to prevent env var leakage across parallel tests.

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings (14 projects)
- `dotnet test --configuration Release` — 777 passed, 0 failed (31 in Testing.Tests: 26 existing + 5 new)
- `cargo test --all-targets` — 149 passed, 0 failed (regression clean)
- `grep -c "MatchSnapshot" PublicAPI.Unshipped.txt` — 1
- Snapshot lifecycle: create→match→fail→update all proved by tests

## Requirements Advanced

- R053 — Snapshot testing in Cupel.Testing: fully implemented with MatchSnapshot method, JSON serialization, CUPEL_UPDATE_SNAPSHOTS env var, and 5 lifecycle tests; ready for validation

## Requirements Validated

- R053 — MatchSnapshot creates/reads/updates JSON snapshot files; CUPEL_UPDATE_SNAPSHOTS=1 rewrites snapshots; 5 tests prove the full create→match→fail→update cycle; PublicAPI clean; 777 .NET tests pass; no Rust regressions

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- Unsealed `SelectionReportAssertionException` to allow `SnapshotMismatchException` inheritance — non-breaking since nothing is shipped yet
- `ContextKind` serializes as PascalCase (`"Document"`) not camelCase — it's a class with its own `JsonConverter`, not an enum; test adjusted accordingly
- Added `[NotInParallel]` to `SnapshotTests` — required due to process-global env var leakage in parallel execution

## Known Limitations

- `ContextKind` serializes as PascalCase in snapshots due to its custom `JsonConverter`; all other enums use camelCase via `JsonStringEnumConverter`. This is cosmetically inconsistent but functionally correct — snapshots compare string equality, so the format just needs to be stable.

## Follow-ups

- none

## Files Created/Modified

- `src/Wollax.Cupel.Testing/SnapshotSerializer.cs` — Internal static JSON serializer for SelectionReport
- `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs` — Public exception with structured diff fields
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — Added MatchSnapshot + MatchSnapshotCore methods
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — Unsealed (removed `sealed` keyword)
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — New entries for MatchSnapshot and SnapshotMismatchException
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — Added InternalsVisibleTo for test project
- `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs` — 5 snapshot lifecycle tests

## Forward Intelligence

### What the next slice should know
- S05 (Rust budget simulation) is independent of S04 — no snapshot testing infrastructure is consumed by Rust. The Rust crate has `insta` for snapshots (D107).

### What's fragile
- Nothing — snapshot testing is self-contained within `Wollax.Cupel.Testing` with no cross-package dependencies beyond `System.Text.Json` (BCL).

### Authoritative diagnostics
- `dotnet test --configuration Release` is the authoritative verification — 5 snapshot lifecycle tests in `SnapshotTests.cs` exercise all code paths.

### What assumptions changed
- Original plan assumed `ContextKind` would serialize as camelCase via `JsonStringEnumConverter` — it's actually a class with a custom `JsonConverter` that preserves PascalCase. Functionally irrelevant for snapshot comparison (string equality) but worth noting for snapshot readability.
