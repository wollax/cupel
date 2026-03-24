# S04: Snapshot testing in Cupel.Testing

**Goal:** Add `MatchSnapshot(name)` to `SelectionReportAssertionChain` that serializes `SelectionReport` to JSON, compares against stored snapshot files, and supports `CUPEL_UPDATE_SNAPSHOTS=1` for in-place rewrite.
**Demo:** A test proves the full create→match→fail→update cycle: first run creates snapshot, second run matches, third run detects diff, env var rewrites snapshot.

## Must-Haves

- `MatchSnapshot(string name)` method on `SelectionReportAssertionChain` using `[CallerFilePath]` for snapshot path resolution
- `SnapshotMismatchException` (or reuse `SelectionReportAssertionException`) with clear diff output showing expected vs actual
- JSON serialization of `SelectionReport` for snapshot format (camelCase, indented, enum strings)
- `CUPEL_UPDATE_SNAPSHOTS=1` environment variable triggers in-place snapshot rewrite instead of throwing
- Snapshot files stored in `__snapshots__/` directory alongside the test file
- A test exercises the full create→match→fail→update lifecycle
- `PublicAPI.Unshipped.txt` updated with new public surface
- `dotnet test --configuration Release` passes with all new tests
- `cargo test --all-targets` passes (no Rust changes, regression only)

## Proof Level

- This slice proves: integration (real file I/O with snapshot create/read/update cycle)
- Real runtime required: yes (filesystem I/O during test execution)
- Human/UAT required: no

## Verification

- `dotnet test --configuration Release` — all tests pass including new snapshot lifecycle tests
- `cargo test --all-targets` — regression check (no Rust changes expected)
- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `grep -c "MatchSnapshot" src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — entry exists
- Snapshot lifecycle test proves: create (file appears) → match (no throw) → fail (exception with diff) → update (env var rewrites file)

## Observability / Diagnostics

- Runtime signals: `SnapshotMismatchException` message includes expected and actual JSON fragments for diff inspection
- Inspection surfaces: snapshot `.json` files on disk alongside test files in `__snapshots__/` directories
- Failure visibility: exception message shows the snapshot name, file path, and content diff
- Redaction constraints: none — `SelectionReport` contains no secrets

## Integration Closure

- Upstream surfaces consumed: `SelectionReport` equality (S01), `SelectionReportAssertionChain` (M003/S04), `System.Text.Json` (BCL)
- New wiring introduced in this slice: `MatchSnapshot` method on existing assertion chain; JSON serialization configuration for diagnostic types
- What remains before the milestone is truly usable end-to-end: S05 (Rust budget simulation parity — independent)

## Tasks

- [x] **T01: Implement MatchSnapshot with JSON serialization and snapshot file I/O** `est:30m`
  - Why: Core implementation — the snapshot assertion method, JSON serialization, file path resolution, env var support, and exception type
  - Files: `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs`, `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs`, `src/Wollax.Cupel.Testing/SnapshotSerializer.cs`, `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt`, `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj`
  - Do: Add `SnapshotSerializer` (static class) for JSON serialization of `SelectionReport` with camelCase, indented, enum strings; add `SnapshotMismatchException` inheriting from `SelectionReportAssertionException`; add `MatchSnapshot(string name, [CallerFilePath] string callerFilePath = "")` to chain; resolve snapshot path as `{callerDir}/__snapshots__/{name}.json`; on first run (no file), create directory and write snapshot; on match, compare JSON strings; on mismatch, throw with diff unless `CUPEL_UPDATE_SNAPSHOTS=1`, in which case overwrite; update PublicAPI.Unshipped.txt
  - Verify: `dotnet build --configuration Release` — 0 errors, 0 warnings
  - Done when: `MatchSnapshot` compiles, `PublicAPI.Unshipped.txt` updated, build green

- [x] **T02: Add snapshot lifecycle tests proving create→match→fail→update cycle** `est:25m`
  - Why: Proves the full lifecycle and exercises real file I/O — the slice's stopping condition
  - Files: `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs`
  - Do: Write tests using temp directories to isolate file I/O: (1) create test — call MatchSnapshot, verify `.json` file created with expected JSON content; (2) match test — write snapshot file, call MatchSnapshot with identical report, verify no throw; (3) fail test — write snapshot file, call MatchSnapshot with different report, verify `SnapshotMismatchException` with diff message; (4) update test — set `CUPEL_UPDATE_SNAPSHOTS=1`, write stale snapshot, call MatchSnapshot with new report, verify file overwritten with new content; (5) verify env var unset does NOT update
  - Verify: `dotnet test --configuration Release` — all tests pass; `cargo test --all-targets` — regression clean
  - Done when: All 5+ lifecycle tests pass, full test suites green in both languages

## Files Likely Touched

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs`
- `src/Wollax.Cupel.Testing/SnapshotMismatchException.cs`
- `src/Wollax.Cupel.Testing/SnapshotSerializer.cs`
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt`
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj`
- `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs`
