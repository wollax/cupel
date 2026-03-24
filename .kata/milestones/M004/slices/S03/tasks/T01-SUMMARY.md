---
id: T01
parent: S03
milestone: M004
provides:
  - QuotaConstraintMode enum (Percentage, Count)
  - QuotaConstraint struct (kind, mode, require, cap)
  - QuotaPolicy trait with quota_constraints() method
  - QuotaPolicy impl for QuotaSlice (percentage mode)
  - QuotaPolicy impl for CountQuotaSlice (count mode)
key_files:
  - crates/cupel/src/slicer/mod.rs
  - crates/cupel/src/slicer/quota.rs
  - crates/cupel/src/slicer/count_quota.rs
  - crates/cupel/src/lib.rs
key_decisions: []
patterns_established:
  - "QuotaPolicy trait as the shared abstraction for quota-based slicers — analytics functions consume this instead of concrete types"
observability_surfaces:
  - none
duration: 5min
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T01: Rust QuotaPolicy trait, QuotaConstraint, and implementations

**Defined QuotaPolicy trait with QuotaConstraint/QuotaConstraintMode types and implemented it for both QuotaSlice (percentage) and CountQuotaSlice (count)**

## What Happened

Added three new public types to `crates/cupel/src/slicer/mod.rs`:
- `QuotaConstraintMode` enum with `Percentage` and `Count` variants
- `QuotaConstraint` struct with `kind`, `mode`, `require`, `cap` fields
- `QuotaPolicy` trait with a single `quota_constraints(&self) -> Vec<QuotaConstraint>` method

Implemented `QuotaPolicy` for `QuotaSlice` in `quota.rs` — iterates `self.quotas` and returns constraints with `mode: Percentage`, using the entry's require/cap percentages directly.

Implemented `QuotaPolicy` for `CountQuotaSlice` in `count_quota.rs` — iterates `self.entries` and returns constraints with `mode: Count`, casting `require_count`/`cap_count` to `f64`.

All new types re-exported from `crates/cupel/src/lib.rs`.

## Verification

- `cargo test --all-targets` — 145 tests passed across 7 suites
- `cargo clippy --all-targets -- -D warnings` — clean, no warnings
- `rg "impl QuotaPolicy for" crates/cupel/src/slicer/` — returns 2 results (QuotaSlice + CountQuotaSlice)

### Slice-level checks (partial, T01):
- ✅ `cargo test --all-targets` — passes
- ✅ `cargo clippy --all-targets -- -D warnings` — clean
- ⏳ `dotnet test` — not yet applicable (T03 scope)
- ⏳ `dotnet build` — not yet applicable (T03 scope)
- ⏳ `grep -c "IQuotaPolicy"` in PublicAPI — not yet applicable (T03 scope)

## Diagnostics

None — pure data types and trait with no runtime behavior.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/slicer/mod.rs` — Added QuotaConstraintMode, QuotaConstraint, QuotaPolicy trait; added ContextKind import
- `crates/cupel/src/slicer/quota.rs` — Added `impl QuotaPolicy for QuotaSlice`; updated imports
- `crates/cupel/src/slicer/count_quota.rs` — Added `impl QuotaPolicy for CountQuotaSlice`; updated imports
- `crates/cupel/src/lib.rs` — Added QuotaConstraint, QuotaConstraintMode, QuotaPolicy to re-export block
