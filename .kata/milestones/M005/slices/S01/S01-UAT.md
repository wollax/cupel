# S01: Crate scaffold + chain plumbing — UAT

**Milestone:** M005
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S01 is crate scaffolding with no runtime behavior — the only testable outcome is that the crate compiles, the trait plumbing works, and the smoke test passes. All verification is via `cargo` commands.

## Preconditions

- Rust toolchain installed (edition 2024, MSRV 1.85)
- Working directory is the repo root

## Smoke Test

```bash
cd crates/cupel-testing && cargo test --all-targets
```
Expected: 1 test passes (`should_returns_assertion_chain`).

## Test Cases

### 1. Crate compiles independently

1. `cd crates/cupel-testing && cargo check`
2. **Expected:** Compiles with no errors.

### 2. Clippy clean across both crates

1. `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings`
2. `cd crates/cupel && cargo clippy --all-targets -- -D warnings`
3. **Expected:** No warnings or errors in either crate.

### 3. Existing cupel tests unaffected

1. `cd crates/cupel && cargo test --all-targets`
2. **Expected:** 158 tests pass with no regressions.

### 4. Trait import and chain construction

1. Open `crates/cupel-testing/tests/smoke.rs`
2. Verify the test imports `SelectionReportAssertions` and calls `.should()` on a `SelectionReport`
3. **Expected:** The test compiles and runs — type system proves the chain struct is returned.

## Edge Cases

### Empty SelectionReport

1. The smoke test already uses an empty report (no items, zeroed totals)
2. **Expected:** `.should()` returns a chain without panicking — the chain struct does not validate report contents at construction time.

## Failure Signals

- `cargo check` or `cargo test` fails in `crates/cupel-testing/` — crate scaffold is broken
- `cargo test` fails in `crates/cupel/` — existing functionality regressed
- Clippy warnings in either crate — lint cleanliness violated

## Requirements Proved By This UAT

- R060 (partial) — proves the crate exists, compiles, and the `should()` entry point works. Does not prove assertion patterns (S02) or publish readiness (S03).

## Not Proven By This UAT

- R060 assertion pattern correctness — requires S02 (13 assertion methods with positive/negative tests)
- R060 structured panic messages — requires S02
- R060 publish readiness — requires S03 (`cargo package` success)

## Notes for Tester

This is a minimal scaffold slice. The interesting testing comes in S02 when assertion methods are added. The main thing to verify here is that the crate structure is sound and doesn't break the existing `cupel` crate.
