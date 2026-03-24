---
id: T03
parent: S03
milestone: M004
provides:
  - IQuotaPolicy interface with GetConstraints() method
  - QuotaConstraintMode enum (Percentage, Count)
  - QuotaConstraint sealed record (Kind, Mode, Require, Cap)
  - KindQuotaUtilization sealed record (Kind, Mode, Require, Cap, Actual, Utilization)
  - QuotaUtilization extension method on SelectionReport
key_files:
  - src/Wollax.Cupel/Slicing/IQuotaPolicy.cs
  - src/Wollax.Cupel/Slicing/QuotaConstraintMode.cs
  - src/Wollax.Cupel/Slicing/QuotaConstraint.cs
  - src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs
  - tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs
key_decisions:
  - "QuotaConstraint uses double for Require/Cap (matching Rust f64 parity, even for count mode)"
patterns_established:
  - "IQuotaPolicy as the .NET shared abstraction for quota-based slicers — analytics consume this instead of concrete types, matching the Rust QuotaPolicy trait pattern"
  - "QuotaUtilization follows the same extension method pattern as BudgetUtilization/KindDiversity/TimestampCoverage in SelectionReportExtensions"
observability_surfaces:
  - none — pure analytics types with no runtime behavior
duration: 12min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T03: .NET IQuotaPolicy, QuotaConstraint, implementations, QuotaUtilization + tests

**Implemented IQuotaPolicy interface, QuotaConstraint/QuotaConstraintMode types, KindQuotaUtilization record, and QuotaUtilization extension method with 5 tests covering both percentage and count modes**

## What Happened

Created the full .NET-side quota policy abstraction matching the Rust QuotaPolicy trait:

1. Added `QuotaConstraintMode` enum (Percentage=0, Count=1), `QuotaConstraint` sealed record, and `IQuotaPolicy` interface in `Slicing/`.
2. Implemented `IQuotaPolicy` on both `QuotaSlice` (builds percentage-mode constraints from QuotaSet) and `CountQuotaSlice` (builds count-mode constraints from CountQuotaEntry list) — additive changes, no breaking modifications.
3. Added `KindQuotaUtilization` sealed record in `Diagnostics/` and `QuotaUtilization` extension method on `SelectionReport` in `SelectionReportExtensions.cs` — same algorithm as Rust: pre-aggregates per-kind token sums and counts, computes actual values (percentage of target tokens or item count), and clamps utilization to [0.0, 1.0]. Results sorted by kind for determinism.
4. Updated PublicAPI.Unshipped.txt with all new public API surface.
5. Wrote 5 tests: QuotaSlice GetConstraints, CountQuotaSlice GetConstraints, percentage-mode utilization, count-mode utilization, and empty report returning zero utilization.

## Verification

- `dotnet build --configuration Release` — 0 errors, 0 warnings (14 projects)
- `dotnet test --configuration Release` — 772 passed, 0 failed, 0 skipped
- `grep -c "IQuotaPolicy" src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 3 entries confirmed
- All 5 QuotaUtilizationTests pass, covering both QuotaSlice and CountQuotaSlice through IQuotaPolicy abstraction

## Diagnostics

None — pure analytics types and extension method with no runtime behavior.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Slicing/QuotaConstraintMode.cs` — new enum (Percentage, Count)
- `src/Wollax.Cupel/Slicing/QuotaConstraint.cs` — new sealed record (Kind, Mode, Require, Cap)
- `src/Wollax.Cupel/Slicing/IQuotaPolicy.cs` — new interface with GetConstraints()
- `src/Wollax.Cupel/Diagnostics/KindQuotaUtilization.cs` — new sealed record for per-kind utilization
- `src/Wollax.Cupel/Slicing/QuotaSlice.cs` — added IQuotaPolicy implementation
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — added IQuotaPolicy implementation
- `src/Wollax.Cupel/Diagnostics/SelectionReportExtensions.cs` — added QuotaUtilization extension method
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — all new API surface entries
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs` — 5 tests for quota utilization
