---
id: T02
parent: S04
milestone: M004
provides:
  - 5 snapshot lifecycle tests proving create→match→fail→update→no-update cycle
  - InternalsVisibleTo wiring for MatchSnapshotCore test access
  - Validation of R053 (snapshot testing in Cupel.Testing)
key_files:
  - tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs
  - src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj
key_decisions:
  - "D121: Snapshot tests use [NotInParallel] to avoid env var leakage between tests — CUPEL_UPDATE_SNAPSHOTS is process-global"
patterns_established:
  - "Temp-directory isolation pattern: each test creates a unique temp dir, passes it to MatchSnapshotCore as fake caller path, cleans up in finally block"
observability_surfaces:
  - none — this is verification code
duration: 15min
verification_result: passed
completed_at: 2026-03-23T14:00:00Z
blocker_discovered: false
---

# T02: Add snapshot lifecycle tests proving create→match→fail→update cycle

**5 integration tests validating the full snapshot lifecycle: create, match, fail with SnapshotMismatchException, env-var update, and no-silent-update guard**

## What Happened

Created `SnapshotTests.cs` with 5 tests exercising every branch of `MatchSnapshotCore`:

1. **Create**: First call writes `__snapshots__/create-test.json` with valid JSON containing camelCase fields, enum strings, and expected content values.
2. **Match**: Identical report on second call returns without throwing.
3. **Fail**: Different report throws `SnapshotMismatchException` with correct `SnapshotName`, `Expected` (original JSON), and `Actual` (new JSON) properties.
4. **Update**: With `CUPEL_UPDATE_SNAPSHOTS=1`, mismatched report overwrites the snapshot file instead of throwing.
5. **No-Update**: Without the env var, mismatched report throws and does NOT modify the snapshot file.

Added `InternalsVisibleTo("Wollax.Cupel.Testing.Tests")` to the Testing csproj to expose `MatchSnapshotCore`.

Tests use per-test temp directories with unique GUIDs to avoid file system pollution. Each test creates its directory, runs assertions, and cleans up in a `finally` block.

Applied `[NotInParallel]` to the test class because `CUPEL_UPDATE_SNAPSHOTS` is a process-global env var — parallel execution of the Update test leaked the env var into the Fail test, causing it to silently update instead of throwing.

## Verification

- `dotnet test --configuration Release` — 777 passed, 0 failed (31 in Testing.Tests: 26 existing + 5 new)
- `cargo test --all-targets` — 149 passed, 0 failed (regression clean)
- `dotnet build --configuration Release` — 0 errors, 0 warnings

**Slice-level verification (all pass — this is the final task):**
- ✅ `dotnet test --configuration Release` — 777 passed
- ✅ `cargo test --all-targets` — 149 passed
- ✅ `dotnet build --configuration Release` — 0 errors, 0 warnings
- ✅ `grep -c "MatchSnapshot" PublicAPI.Unshipped.txt` — 1
- ✅ Snapshot lifecycle: create→match→fail→update all proved by tests

## Diagnostics

None — this task is verification code. The observability surface (SnapshotMismatchException fields) was verified by the Fail test, which asserts on `SnapshotName`, `Expected`, and `Actual` properties.

## Deviations

- Added `[NotInParallel]` to `SnapshotTests` class — not in original plan. Required because `CUPEL_UPDATE_SNAPSHOTS` is a process-global env var that leaked across parallel tests. This is the correct fix (not a workaround).
- `ContextKind` serializes as `"Document"` (PascalCase) not `"document"` (camelCase) — `ContextKind` is a class with its own `JsonConverter`, not an enum. Create test adjusted accordingly.

## Known Issues

None.

## Files Created/Modified

- `tests/Wollax.Cupel.Testing.Tests/SnapshotTests.cs` — 5 snapshot lifecycle tests
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — Added `InternalsVisibleTo` for test project
