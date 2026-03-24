---
id: S01
parent: M005
milestone: M005
provides:
  - cupel-testing crate with Cargo.toml (edition 2024, MSRV 1.85, MIT, cupel path dep)
  - SelectionReportAssertions trait with should() entry point
  - SelectionReportAssertionChain<'a> struct holding &'a SelectionReport
  - impl SelectionReportAssertions for SelectionReport
  - Integration smoke test proving chain plumbing works end-to-end
requires:
  - slice: none
    provides: first slice — no upstream dependencies
affects:
  - S02
  - S03
key_files:
  - crates/cupel-testing/Cargo.toml
  - crates/cupel-testing/src/lib.rs
  - crates/cupel-testing/src/chain.rs
  - crates/cupel-testing/tests/smoke.rs
key_decisions:
  - "D126: Separate cupel-testing crate (not feature flag)"
  - "D127: Fluent chain API (report.should())"
  - "D128: Panic on failure (not Result)"
  - "D129: S01 verification strategy — contract-level (compile + test + clippy)"
patterns_established:
  - "Trait extension pattern: SelectionReportAssertions trait + impl for SelectionReport, chain struct with pub(crate) constructor"
  - "Test report construction via DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report() — reuse in all cupel-testing integration tests"
observability_surfaces:
  - "cargo test output in crates/cupel-testing/ shows smoke test pass/fail"
  - "cargo clippy surfaces lint/compilation errors with file/line numbers"
drill_down_paths:
  - .kata/milestones/M005/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M005/slices/S01/tasks/T02-SUMMARY.md
duration: 10min
verification_result: passed
completed_at: 2026-03-24T12:00:00Z
---

# S01: Crate scaffold + chain plumbing

**`cupel-testing` crate scaffolded with `SelectionReportAssertions` trait, `SelectionReportAssertionChain` plumbing, and smoke test proving `report.should()` compiles and runs**

## What Happened

Created the `cupel-testing` crate at `crates/cupel-testing/` following the same conventions as the `cupel` crate (edition 2024, MSRV 1.85, MIT license, `cupel` path dependency). The crate provides two public types: `SelectionReportAssertions` trait defining `fn should(&self) -> SelectionReportAssertionChain<'_>`, and `SelectionReportAssertionChain<'a>` struct holding `&'a SelectionReport` with a `pub(crate)` constructor. The trait is implemented for `SelectionReport`, so callers import the trait and call `.should()` directly.

An integration smoke test in `tests/smoke.rs` constructs a `SelectionReport` via `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()` (required because `SelectionReport` is `#[non_exhaustive]`) and calls `.should()` to prove the chain plumbing works end-to-end. The `#[allow(dead_code)]` annotation on the chain's `report` field keeps clippy clean until S02 adds assertion methods that read it.

## Verification

| Check | Status | Evidence |
|-------|--------|----------|
| `cargo test --all-targets` in cupel-testing | ✓ PASS | 1 test passed |
| `cargo clippy --all-targets -- -D warnings` in cupel-testing | ✓ PASS | Clean |
| `cargo test --all-targets` in cupel | ✓ PASS | 158 tests passed, no regressions |
| `cargo clippy --all-targets -- -D warnings` in cupel | ✓ PASS | Clean |

## Requirements Advanced

- R060 — S01 delivers the crate scaffold and chain plumbing that S02 will fill with 13 assertion methods. Entry point (`should()`) and chain struct are ready; no assertion logic yet.

## Requirements Validated

- None — S01 is scaffolding; R060 validation requires S02 (assertion methods) and S03 (integration + publish).

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- T02 plan referenced `TraceDetailLevel::Full` — the actual enum variants are `Stage` and `Item`. Used `Item` for maximum detail. No impact on behavior.

## Known Limitations

- `SelectionReportAssertionChain` has no assertion methods yet — S02 adds all 13 patterns.
- `chain.report` field has `#[allow(dead_code)]` — removed in S02 when assertion methods read it.
- Crate is not publishable yet — S03 adds `cargo package` readiness.

## Follow-ups

- None — S02 and S03 are already planned.

## Files Created/Modified

- `crates/cupel-testing/Cargo.toml` — Crate metadata with cupel path dependency
- `crates/cupel-testing/src/lib.rs` — SelectionReportAssertions trait, impl for SelectionReport, re-exports
- `crates/cupel-testing/src/chain.rs` — SelectionReportAssertionChain struct with pub(crate) constructor
- `crates/cupel-testing/tests/smoke.rs` — Integration smoke test proving `.should()` chain plumbing works

## Forward Intelligence

### What the next slice should know
- `SelectionReportAssertionChain` holds `&'a SelectionReport` — assertion methods access `self.report.included`, `self.report.excluded`, etc.
- Each assertion method should return `&mut Self` for chaining and panic on failure (D128).
- The `#[allow(dead_code)]` on `report` field must be removed when the first assertion method reads it.
- Test report construction pattern: `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()` — reuse this in all S02 tests.

### What's fragile
- Nothing — the crate is minimal scaffolding with no complex logic.

### Authoritative diagnostics
- `cargo test --all-targets` in `crates/cupel-testing/` — single source of truth for test pass/fail.
- `cargo clippy --all-targets -- -D warnings` — catches dead code and lint issues.

### What assumptions changed
- `TraceDetailLevel::Full` doesn't exist — use `TraceDetailLevel::Item` for test report construction.
