---
id: T01
parent: S01
milestone: M004
provides:
  - PartialEq on SelectionReport, IncludedItem, ExcludedItem, TraceEvent, CountRequirementShortfall, OverflowEvent
  - PartialEq on ContextBudget (transitive dependency for OverflowEvent)
  - 15 equality tests in crates/cupel/tests/equality.rs covering all 6 diagnostic types
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/model/context_budget.rs
  - crates/cupel/tests/equality.rs
key_decisions:
  - "Added PartialEq to ContextBudget as transitive requirement for OverflowEvent (contains ContextBudget field)"
  - "Equality tests use clone-and-compare pattern instead of constructing from outside crate, because all diagnostic structs are #[non_exhaustive]"
patterns_established:
  - "Clone-and-compare test pattern for #[non_exhaustive] types: produce real instances via pipeline, then assert clone == original and structural differences via != on differing inputs"
observability_surfaces:
  - none
duration: 12min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T01: Add PartialEq derives to Rust diagnostic types

**Derived PartialEq on 6 diagnostic structs plus ContextBudget, with 15 integration tests exercising equality across all types**

## What Happened

Added `PartialEq` to the derive macros on 6 diagnostic structs: `CountRequirementShortfall`, `TraceEvent`, `OverflowEvent`, `IncludedItem`, `ExcludedItem`, and `SelectionReport`. Also added `PartialEq` to `ContextBudget` (in `model/context_budget.rs`) because `OverflowEvent` contains a `ContextBudget` field and derives require transitive trait implementations.

Did NOT add `Eq` to any of the 6 structs or `ContextBudget` — they contain `f64` fields (`score`, `duration_ms`, `tokens_over_budget`, `estimation_safety_margin_percent`) which do not implement `Eq`.

Created `crates/cupel/tests/equality.rs` with 15 test functions covering: SelectionReport clone equality and structural inequality, IncludedItem clone and content-based inequality, ExcludedItem clone and content-based inequality, TraceEvent clone and item-count divergence, CountRequirementShortfall clone equality, OverflowEvent via Proceed-strategy report clone, ExclusionReason variant equality/inequality, and InclusionReason variant equality.

## Verification

- `cargo test --all-targets` — 143 tests passed (including 15 new equality tests)
- `cargo test --all-targets --features serde` — 192 tests passed
- `cargo clippy --all-targets -- -D warnings` — clean, no warnings
- `grep -c "PartialEq" crates/cupel/src/diagnostics/mod.rs` — 9 (3 pre-existing + 6 new)

### Slice-level checks
- ✅ `cargo test --all-targets` — all pass
- ✅ `cargo test --all-targets --features serde` — all pass
- ✅ `cargo clippy --all-targets -- -D warnings` — clean
- ⏳ `dotnet test --configuration Release` — not yet (T02 scope)
- ⏳ `dotnet build --configuration Release` — not yet (T02 scope)

## Diagnostics

None — equality is a pure function with no runtime observability surface.

## Deviations

- Added `PartialEq` to `ContextBudget` (not in the original plan) because `OverflowEvent` contains a `ContextBudget` field and the derive requires the trait on all fields.
- Tests use clone-and-compare pattern instead of constructing reports with different fields directly, because all diagnostic structs are `#[non_exhaustive]` and cannot be constructed outside the crate. This still exercises all 6 types effectively.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — Added `PartialEq` to 6 struct derives
- `crates/cupel/src/model/context_budget.rs` — Added `PartialEq` to `ContextBudget` derive
- `crates/cupel/tests/equality.rs` — New integration test file with 15 equality tests
