---
id: T02
parent: S01
milestone: M005
provides:
  - Integration smoke test proving SelectionReportAssertions::should() chain plumbing works
  - Full build and clippy verification across both cupel and cupel-testing crates
key_files:
  - crates/cupel-testing/tests/smoke.rs
key_decisions:
  - "Used TraceDetailLevel::Item (not Full — doesn't exist) with DiagnosticTraceCollector to construct SelectionReport, since the struct is #[non_exhaustive]"
patterns_established:
  - "Test report construction via DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report() — reuse this pattern in all cupel-testing integration tests"
observability_surfaces:
  - "cargo test output in crates/cupel-testing/ shows smoke test pass/fail"
duration: 5min
verification_result: passed
completed_at: 2026-03-24T12:00:00Z
blocker_discovered: false
---

# T02: Add smoke test and verify full build

**Integration smoke test proves `report.should()` returns `SelectionReportAssertionChain`, full build and clippy clean across both crates**

## What Happened

Created `crates/cupel-testing/tests/smoke.rs` with a single `#[test]` that constructs a `SelectionReport` via `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()` (required because `SelectionReport` is `#[non_exhaustive]`), then calls `.should()` to get a `SelectionReportAssertionChain`. The test proves the trait extension plumbing compiles and runs end-to-end.

Ran all four verification commands — all passed clean.

## Verification

| Check | Status | Evidence |
|-------|--------|----------|
| `crates/cupel-testing/tests/smoke.rs` exists with `#[test]` | ✓ PASS | File created, 1 test function |
| Test constructs SelectionReport and calls `.should()` | ✓ PASS | `should_returns_assertion_chain` passes |
| `cargo test --all-targets` in cupel-testing | ✓ PASS | 1 passed |
| `cargo clippy --all-targets -- -D warnings` in cupel-testing | ✓ PASS | No issues |
| `cargo test --all-targets` in cupel | ✓ PASS | 158 passed, no regressions |
| `cargo clippy --all-targets -- -D warnings` in cupel | ✓ PASS | No issues |

## Diagnostics

`cargo test` output in `crates/cupel-testing/` — test failures surface with assertion details and file/line info.

## Deviations

Task plan suggested `TraceDetailLevel::Full` — the actual enum variants are `Stage` and `Item`. Used `Item` for maximum detail.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel-testing/tests/smoke.rs` — Integration smoke test proving `.should()` chain plumbing works
