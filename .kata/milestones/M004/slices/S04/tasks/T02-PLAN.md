---
estimated_steps: 4
estimated_files: 2
---

# T02: Add snapshot lifecycle tests proving create→match→fail→update cycle

**Slice:** S04 — Snapshot testing in Cupel.Testing
**Milestone:** M004

## Description

Write integration tests exercising the full snapshot lifecycle: create (first run writes file), match (identical report passes), fail (different report throws `SnapshotMismatchException` with diff), update (`CUPEL_UPDATE_SNAPSHOTS=1` overwrites). Tests use temp directories to isolate file I/O and avoid test pollution. This task proves R053 is met.

## Steps

1. Create `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs`. Use a test helper that constructs a `SelectionReport` directly (D091 pattern) and writes a fake "caller file" to a temp directory to control `[CallerFilePath]` resolution. Since `[CallerFilePath]` is compile-time, tests should instead call `MatchSnapshot` from within the test file itself and use real `__snapshots__/` directories — then clean up in test teardown. Alternatively, use a separate internal test helper that accepts the caller path explicitly for testability.

   Design choice: Add an `internal` overload `MatchSnapshot(string name, string callerFilePath)` (no `[CallerFilePath]` attribute, just a plain string parameter) that the public method delegates to. Tests call the internal overload with a temp directory path. This avoids file system pollution in the test project directory.

2. Write test cases:
   - **Create**: call `MatchSnapshot("create-test", tempCallerPath)` with no existing snapshot → assert file exists at `{tempDir}/__snapshots__/create-test.json`, assert file content is valid JSON containing expected report fields
   - **Match**: pre-write a snapshot file by calling MatchSnapshot once, then call again with identical report → assert no exception
   - **Fail**: pre-write a snapshot from report A, then call with report B (different `TotalCandidates` or different included items) → catch `SnapshotMismatchException`, assert message contains snapshot name and diff content
   - **Update**: set `CUPEL_UPDATE_SNAPSHOTS=1` via env var, pre-write stale snapshot, call with new report → assert file now contains new report JSON, unset env var in teardown
   - **No-update without env var**: pre-write stale snapshot, ensure env var is NOT set, call with different report → assert throws (does NOT silently update)

3. Add `[assembly: InternalsVisibleTo("Wollax.Cupel.Testing.Tests")]` to the Testing project if not already present, so tests can access the internal overload.

4. Run verification: `dotnet test --configuration Release` — all tests pass. `cargo test --all-targets` — regression clean.

## Must-Haves

- [ ] Create test: first `MatchSnapshot` call writes snapshot file with valid JSON content
- [ ] Match test: identical report on second call does not throw
- [ ] Fail test: different report throws `SnapshotMismatchException` with informative diff
- [ ] Update test: `CUPEL_UPDATE_SNAPSHOTS=1` overwrites stale snapshot with new content
- [ ] No-update test: without env var, mismatch throws rather than silently updating
- [ ] Tests use temp directories (no file pollution in test project)
- [ ] `dotnet test --configuration Release` — all tests pass
- [ ] `cargo test --all-targets` — regression clean

## Verification

- `dotnet test --configuration Release` — all tests pass including new snapshot lifecycle tests
- `cargo test --all-targets` — regression check, all pass
- `dotnet build --configuration Release` — 0 errors, 0 warnings

## Observability Impact

- Signals added/changed: None — tests validate the observability surface built in T01
- How a future agent inspects this: read test output for pass/fail; snapshot files in temp directories cleaned up after test run
- Failure state exposed: None — this is verification code

## Inputs

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — `MatchSnapshot` method from T01
- `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs` — exception type from T01
- `src/Wollax.Cupel.Testing/SnapshotSerializer.cs` — JSON serialization from T01
- `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` — existing test helpers (`MakeItem`, `MakeIncluded`, `MakeExcluded`, `MakeReport`)

## Expected Output

- `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs` — 5+ lifecycle tests covering create/match/fail/update/no-update
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — may need `InternalsVisibleTo` attribute if not already present
- All test suites green in both languages
