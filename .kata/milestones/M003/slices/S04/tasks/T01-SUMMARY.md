---
id: T01
parent: S04
milestone: M003
provides:
  - analytics.rs with pub fn budget_utilization, kind_diversity, timestamp_coverage in crates/cupel
  - All three functions pub use-d from crates/cupel/src/lib.rs
  - 7 unit tests covering empty-report and non-empty cases for all three functions
  - SelectionReportExtensions.cs with BudgetUtilization, KindDiversity, TimestampCoverage as extension methods on SelectionReport
  - PublicAPI.Unshipped.txt updated with 4 new entries (class + 3 methods)
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
key_decisions:
  - ContextBudget::new requires a HashMap (not Option) for reserved_slots; use Default::default() in tests
  - ContextItem.kind is private in Rust; use .kind() accessor method (not field access) in analytics
patterns_established:
  - analytics module pattern: pure free functions on &SelectionReport in crates/cupel/src/analytics.rs, pub use-d from lib.rs
  - .NET analytics pattern: static extension class in Wollax.Cupel.Diagnostics namespace alongside SelectionReport
observability_surfaces:
  - cargo test -- analytics --nocapture → 7 unit tests for Rust analytics
  - dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj → RS0016 fires if PublicAPI.Unshipped.txt is incomplete
duration: 10min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T01: Rust + .NET Analytics Extension Methods

**Added `budget_utilization`, `kind_diversity`, and `timestamp_coverage` as free functions in Rust and extension methods in .NET on `SelectionReport`, establishing the R021 analytics surface.**

## What Happened

Created `crates/cupel/src/analytics.rs` with three `pub fn` free functions operating on `&SelectionReport`:
- `budget_utilization` — divides total included token sum by `budget.max_tokens()`
- `kind_diversity` — collects kind refs into a `HashSet`, returns `.len()` as `usize`
- `timestamp_coverage` — returns `0.0` on empty, otherwise fraction with timestamps

All three re-exported from `lib.rs` via `pub use analytics::{...}`.

Added 7 unit tests (all passing): empty-report returns 0/0.0 for each function, plus non-empty cases including a partial-timestamp case and a mixed-kind deduplication case.

One non-obvious discovery: `ContextBudget::new` takes a `HashMap<ContextKind, i64>` directly (not `Option`); tests use `Default::default()`. Also, `ContextItem.kind` is a private field — the `.kind()` accessor is used in analytics.

Created `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` with the .NET counterparts using LINQ. Updated `PublicAPI.Unshipped.txt` with all 4 required entries.

## Verification

- `cargo test -- analytics --nocapture` → 7 tests pass (budget_utilization_empty_is_zero, budget_utilization_full_budget, kind_diversity_empty_is_zero, kind_diversity_counts_distinct_kinds, timestamp_coverage_empty_is_zero, timestamp_coverage_all_have_timestamps, timestamp_coverage_partial)
- `cargo test --all-targets` → all tests pass (0 failures, 0 errors, 0 warnings)
- `grep "budget_utilization\|kind_diversity\|timestamp_coverage" crates/cupel/src/lib.rs` → 1 line with all 3 exports
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj` → 0 errors, 0 warnings
- `dotnet test` → 682 tests passed, 0 failed

## Diagnostics

- **Rust analytics:** `cargo test -- analytics --nocapture` — all 7 tests with visible output
- **PublicAPI compliance:** `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep " error "` → empty output confirms compliance
- **Re-export presence:** `grep "budget_utilization\|kind_diversity\|timestamp_coverage" crates/cupel/src/lib.rs`

## Deviations

None. Plan followed exactly.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — new; 3 pub fn analytics functions + 7 unit tests (~160 lines)
- `crates/cupel/src/lib.rs` — modified; added `pub mod analytics` and `pub use analytics::{...}` (+2 lines)
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — new; 3 extension methods on SelectionReport (~45 lines)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — modified; 4 new entries (class declaration + 3 method signatures)
