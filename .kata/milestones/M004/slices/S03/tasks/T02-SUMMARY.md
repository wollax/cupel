---
id: T02
parent: S03
milestone: M004
provides:
  - KindQuotaUtilization struct (kind, mode, require, cap, actual, utilization)
  - quota_utilization free function accepting &SelectionReport, &dyn QuotaPolicy, &ContextBudget
  - Integration tests for both QuotaSlice (percentage) and CountQuotaSlice (count) policies
  - Re-exports of KindQuotaUtilization and quota_utilization from lib.rs
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/quota_utilization.rs
key_decisions: []
patterns_established:
  - "quota_utilization follows the same pattern as budget_utilization/kind_diversity — pure function in analytics.rs consuming report + policy + budget"
  - "Per-kind stats pre-aggregated via HashMap for single-pass efficiency"
observability_surfaces:
  - none
duration: 8min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T02: Rust quota_utilization function + tests

**Added KindQuotaUtilization struct and quota_utilization function computing per-kind utilization against QuotaPolicy constraints, with 4 integration tests covering percentage, count, empty, and absent-kind cases**

## What Happened

Added `KindQuotaUtilization` struct to `analytics.rs` with fields: `kind`, `mode`, `require`, `cap`, `actual`, `utilization`. The `quota_utilization` free function accepts `&SelectionReport`, `&dyn QuotaPolicy`, and `&ContextBudget`, pre-aggregates included items by kind into (token_sum, count) pairs, then maps each policy constraint to a utilization entry. For percentage mode, `actual` = `token_sum / target_tokens * 100.0`. For count mode, `actual` = item count as f64. Utilization = `actual / cap` clamped to [0.0, 1.0], with 0.0 when cap is zero. Results sorted by kind name for determinism.

Created 4 integration tests: percentage mode with two kinds, count mode with two kinds, empty report returning zeros, and kind present in policy but absent from report returning actual=0.0.

## Verification

- `cargo test --all-targets` — 149 tests passed (8 suites), including 4 new quota_utilization tests
- `cargo clippy --all-targets -- -D warnings` — clean, no warnings

### Slice-level checks (partial, T02):
- ✅ `cargo test --all-targets` — passes (149 tests)
- ✅ `cargo clippy --all-targets -- -D warnings` — clean
- ⏳ `dotnet test` — not yet applicable (T03/T04 scope)
- ⏳ `dotnet build` — not yet applicable (T03/T04 scope)
- ⏳ `grep -c "IQuotaPolicy"` in PublicAPI — not yet applicable (T03 scope)

## Diagnostics

None — pure analytics function with no runtime behavior.

## Deviations

- `ContextKind` doesn't implement `Ord`, so sorted results by `kind.as_str()` instead of direct `cmp` — same deterministic outcome.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — Added KindQuotaUtilization struct, quota_utilization function, and imports for QuotaConstraintMode/QuotaPolicy
- `crates/cupel/src/lib.rs` — Added KindQuotaUtilization and quota_utilization to re-export block
- `crates/cupel/tests/quota_utilization.rs` — 4 integration tests covering percentage mode, count mode, empty report, and absent kind
