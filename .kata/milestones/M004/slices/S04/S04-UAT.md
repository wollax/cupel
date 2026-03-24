# S04: Snapshot testing in Cupel.Testing — UAT

**Milestone:** M004
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: Snapshot testing is a developer tool exercised entirely through automated tests; the full lifecycle (create/match/fail/update) is mechanically checkable with no UI or runtime service

## Preconditions

- .NET 10 SDK installed
- Repository checked out with all S04 changes

## Smoke Test

Run `dotnet test --configuration Release` — 777 tests pass including 5 new snapshot lifecycle tests.

## Test Cases

### 1. Snapshot creation on first run

1. Create a test file that calls `report.Should().MatchSnapshot("my-snapshot")`
2. Run the test
3. **Expected:** A `__snapshots__/my-snapshot.json` file appears alongside the test file containing indented JSON with camelCase field names

### 2. Snapshot match on identical report

1. Run the same test again without changing the report
2. **Expected:** Test passes — no exception thrown

### 3. Snapshot mismatch detection

1. Change the report (e.g., different `TotalCandidates` value)
2. Run the test
3. **Expected:** `SnapshotMismatchException` thrown with message containing the snapshot name, expected JSON, and actual JSON

### 4. Snapshot update via env var

1. Set `CUPEL_UPDATE_SNAPSHOTS=1` in the environment
2. Run the test with the changed report
3. **Expected:** Test passes, snapshot file is overwritten with the new report JSON

### 5. No silent update without env var

1. Unset `CUPEL_UPDATE_SNAPSHOTS`
2. Change the report again
3. **Expected:** `SnapshotMismatchException` thrown — the snapshot file is NOT silently modified

## Edge Cases

### Empty report snapshot

1. Create a report with empty `Included`, `Excluded`, and `Events` lists
2. Call `MatchSnapshot("empty-report")`
3. **Expected:** Valid JSON snapshot created with empty arrays; subsequent identical call matches

### Long snapshot content in exception message

1. Create a report with many included items (producing JSON > 500 chars)
2. Trigger a mismatch
3. **Expected:** Exception message truncates expected/actual to 500 chars each with `...` suffix; full content available via `Expected` and `Actual` properties

## Failure Signals

- `SnapshotMismatchException` not thrown when reports differ → comparison logic broken
- Snapshot file not created on first run → path resolution or directory creation broken
- `CUPEL_UPDATE_SNAPSHOTS=1` still throws → env var check broken
- Snapshot file modified without env var set → update guard broken

## Requirements Proved By This UAT

- R053 — Snapshot testing in Cupel.Testing: MatchSnapshot creates/reads/updates JSON snapshots; CUPEL_UPDATE_SNAPSHOTS=1 triggers rewrite; full lifecycle mechanically verified

## Not Proven By This UAT

- Snapshot behavior with real pipeline-produced reports (tests use direct `SelectionReport` construction per D091)
- Cross-platform path resolution (tested on macOS only; `[CallerFilePath]` is compiler-provided)
- Package installability of updated `Wollax.Cupel.Testing` from NuGet feed (consumption test not re-run for snapshot addition)

## Notes for Tester

- All test cases above are already automated in `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs`. Manual testing is optional.
- `ContextKind` values appear as PascalCase in snapshot JSON (e.g., `"Document"`) — this is by design (custom `JsonConverter`), not a bug.
