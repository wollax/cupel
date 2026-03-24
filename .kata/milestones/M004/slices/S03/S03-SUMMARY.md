---
id: S03
parent: M004
milestone: M004
provides:
  - QuotaPolicy trait (Rust) / IQuotaPolicy interface (.NET) — shared abstraction for quota-based slicers
  - QuotaConstraintMode enum (Percentage, Count) in both languages
  - QuotaConstraint struct/record with kind, mode, require, cap fields
  - KindQuotaUtilization struct/record with kind, mode, require, cap, actual, utilization
  - quota_utilization free function (Rust) / QuotaUtilization extension method (.NET) computing per-kind utilization
requires:
  - slice: S01
    provides: SelectionReport equality (pattern for report-based analytics)
affects:
  - S04
  - S05
key_files:
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/quota.rs
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/quota_utilization.rs
  - src/Wollax.Cupel/Slicing/IQuotaPolicy.cs
  - src/Wollax.Cupel/Slicing/QuotaConstraint.cs
  - src/Wollax.Cupel/Slicing/QuotaConstraintMode.cs
  - src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs
  - src/Wollax.Cupel/Slicing/QuotaSlice.cs
  - src/Wollax.Cupel/Slicing/CountQuotaSlice.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs
key_decisions:
  - "D105: IQuotaPolicy abstraction over direct config — shared interface for extensibility"
  - "D116: QuotaConstraint uses f64 for both percentage and count modes — uniform utilization ratio computation"
  - "D117: quota_utilization requires explicit ContextBudget parameter — percentage mode needs target_tokens denominator"
patterns_established:
  - "QuotaPolicy trait / IQuotaPolicy interface as the shared abstraction for quota-based slicers — analytics functions consume this instead of concrete types"
  - "quota_utilization follows the same analytics.rs / SelectionReportExtensions pattern as budget_utilization, kind_diversity, timestamp_coverage"
  - "Per-kind stats pre-aggregated via HashMap for single-pass efficiency, sorted by kind name for deterministic output"
observability_surfaces:
  - none — pure analytics types and functions with no runtime behavior
drill_down_paths:
  - .kata/milestones/M004/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M004/slices/S03/tasks/T02-SUMMARY.md
  - .kata/milestones/M004/slices/S03/tasks/T03-SUMMARY.md
duration: 25min
verification_result: passed
completed_at: 2026-03-23T12:30:00Z
---

# S03: IQuotaPolicy abstraction + QuotaUtilization

**Extracted QuotaPolicy trait / IQuotaPolicy interface from both quota slicers and added quota_utilization analytics returning per-kind utilization in both Rust and .NET**

## What Happened

Extracted a shared quota policy abstraction from both quota-based slicers and built a per-kind utilization analytics function on top of it, in both languages.

**T01 (Rust trait + types):** Defined `QuotaConstraintMode` enum (Percentage/Count), `QuotaConstraint` struct, and `QuotaPolicy` trait with `quota_constraints()` method in `slicer/mod.rs`. Implemented on `QuotaSlice` (percentage mode) and `CountQuotaSlice` (count mode). All types re-exported from `lib.rs`.

**T02 (Rust analytics + tests):** Added `KindQuotaUtilization` struct and `quota_utilization` free function to `analytics.rs`. Function pre-aggregates included items by kind into (token_sum, count) pairs, maps each policy constraint to a utilization entry. Percentage mode: `actual = token_sum / target_tokens * 100.0`. Count mode: `actual = item_count as f64`. Utilization = `actual / cap` clamped to [0.0, 1.0]. Four integration tests cover percentage mode, count mode, empty report, and absent kind.

**T03 (.NET interface + types + analytics + tests):** Created `IQuotaPolicy` interface, `QuotaConstraintMode` enum, `QuotaConstraint` sealed record, and `KindQuotaUtilization` sealed record. Implemented `IQuotaPolicy` on both `QuotaSlice` and `CountQuotaSlice` — additive changes only. Added `QuotaUtilization` extension method to `SelectionReportExtensions`. Five tests cover GetConstraints for both types plus percentage/count/empty utilization scenarios. PublicAPI.Unshipped.txt updated with all new surface.

## Verification

- `cargo test --all-targets` — 149 passed (8 suites), including 4 new quota_utilization tests
- `cargo clippy --all-targets -- -D warnings` — clean
- `dotnet test --configuration Release` — 772 passed, 0 failed
- `dotnet build --configuration Release` — 0 errors, 0 warnings
- `grep -c "IQuotaPolicy" PublicAPI.Unshipped.txt` — 3 entries confirmed
- QuotaSlice + CountQuotaSlice both exercise quota_utilization through the shared abstraction

## Requirements Advanced

- R052 — IQuotaPolicy abstraction + QuotaUtilization: fully implemented in both languages with tests; ready for validation

## Requirements Validated

- R052 — Rust: QuotaPolicy trait implemented by both QuotaSlice and CountQuotaSlice; quota_utilization returns correct per-kind data; 4 integration tests pass. .NET: IQuotaPolicy implemented by both slicers; QuotaUtilization extension method returns correct data; 5 tests pass; PublicAPI clean; no breaking changes. Both languages verified with full test suites.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None.

## Known Limitations

- `ContextKind` in Rust doesn't implement `Ord`, so utilization results are sorted by `kind.as_str()` — same deterministic outcome but uses string comparison rather than kind-level ordering.

## Follow-ups

- none

## Files Created/Modified

- `crates/cupel/src/slicer/mod.rs` — QuotaConstraintMode, QuotaConstraint, QuotaPolicy trait
- `crates/cupel/src/slicer/quota.rs` — QuotaPolicy impl for QuotaSlice
- `crates/cupel/src/slicer/count_quota.rs` — QuotaPolicy impl for CountQuotaSlice
- `crates/cupel/src/analytics.rs` — KindQuotaUtilization, quota_utilization function
- `crates/cupel/src/lib.rs` — re-exports for all new types
- `crates/cupel/tests/quota_utilization.rs` — 4 integration tests
- `src/Wollax.Cupel/Slicing/IQuotaPolicy.cs` — interface definition
- `src/Wollax.Cupel/Slicing/QuotaConstraintMode.cs` — enum
- `src/Wollax.Cupel/Slicing/QuotaConstraint.cs` — sealed record
- `src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs` — sealed record
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — IQuotaPolicy implementation added
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — IQuotaPolicy implementation added
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — QuotaUtilization extension method
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — new API surface entries
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs` — 5 tests

## Forward Intelligence

### What the next slice should know
- The analytics.rs / SelectionReportExtensions pattern is now well-established: free function taking `(&SelectionReport, &dyn Trait, &ContextBudget)` in Rust, extension method on `SelectionReport` in .NET. S04 snapshot testing will serialize SelectionReport — the new QuotaUtilization types are not part of SelectionReport itself.

### What's fragile
- Nothing — all new types are pure data with no runtime dependencies or mutable state.

### Authoritative diagnostics
- `cargo test --all-targets` and `dotnet test --configuration Release` are the authoritative verification — no conformance vectors for QuotaUtilization (no spec chapter).

### What assumptions changed
- No assumptions changed — the slice executed exactly as planned.
