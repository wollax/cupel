---
estimated_steps: 4
estimated_files: 1
---

# T02: Add smoke test and verify full build

**Slice:** S01 — Crate scaffold + chain plumbing
**Milestone:** M005

## Description

Write an integration test that exercises the chain plumbing end-to-end: construct a `SelectionReport`, call `.should()`, get a `SelectionReportAssertionChain` back. Then verify both `cupel` and `cupel-testing` crates pass `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings`.

## Steps

1. Create `crates/cupel-testing/tests/smoke.rs`:
   - Import `cupel::{SelectionReport, ...}` and `cupel_testing::SelectionReportAssertions`
   - Construct a minimal `SelectionReport` with empty vecs and zeroed counts (use struct literal — `SelectionReport` is `#[non_exhaustive]` but constructable with all fields in external crates? Check and use `DiagnosticTraceCollector` + `into_report()` if direct construction is blocked)
   - Call `report.should()` and let-bind the result to prove the type resolves to `SelectionReportAssertionChain`
   - Add a `#[test]` annotation
2. Run `cargo test --all-targets` in `crates/cupel-testing/` — smoke test passes
3. Run `cargo clippy --all-targets -- -D warnings` in `crates/cupel-testing/` — clean
4. Run `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings` in `crates/cupel/` — no regressions

## Must-Haves

- [ ] `crates/cupel-testing/tests/smoke.rs` exists with at least one `#[test]` function
- [ ] Test constructs a `SelectionReport` and calls `.should()` successfully
- [ ] `cargo test --all-targets` passes in `crates/cupel-testing/`
- [ ] `cargo clippy --all-targets -- -D warnings` clean in `crates/cupel-testing/`
- [ ] `cargo test --all-targets` passes in `crates/cupel/` (no regressions)
- [ ] `cargo clippy --all-targets -- -D warnings` clean in `crates/cupel/`

## Verification

- `cd crates/cupel-testing && cargo test --all-targets` — 1+ test passes
- `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` — exit 0
- `cd crates/cupel && cargo test --all-targets` — all existing tests pass
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` — exit 0

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: `cargo test` output in `crates/cupel-testing/`
- Failure state exposed: Test failure messages with assertion details

## Inputs

- `crates/cupel-testing/src/lib.rs` — the `SelectionReportAssertions` trait to import (from T01)
- `crates/cupel-testing/src/chain.rs` — the `SelectionReportAssertionChain` struct (from T01)
- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport` is `#[non_exhaustive]`, so direct struct construction from an external crate may be blocked; may need `DiagnosticTraceCollector::new(detail_level).into_report()` to obtain a report instance
- `crates/cupel/src/diagnostics/trace_collector.rs` — `DiagnosticTraceCollector` for constructing test reports

## Expected Output

- `crates/cupel-testing/tests/smoke.rs` — integration test proving `should()` chain plumbing works
