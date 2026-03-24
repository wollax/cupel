---
id: S01
parent: M004
milestone: M004
provides:
  - PartialEq on SelectionReport, IncludedItem, ExcludedItem, TraceEvent, CountRequirementShortfall, OverflowEvent (Rust)
  - PartialEq on ContextBudget (Rust — transitive dependency for OverflowEvent)
  - IEquatable<ContextItem> with collection-aware equality (Tags SequenceEqual, Metadata element-wise) (.NET)
  - IEquatable<IncludedItem>, IEquatable<ExcludedItem>, IEquatable<SelectionReport> with SequenceEqual for all list properties (.NET)
  - PublicAPI.Unshipped.txt updated with all equality surface
requires:
  - slice: none
    provides: first slice — builds on existing diagnostic types
affects:
  - S02
  - S03
  - S04
key_files:
  - crates/cupel/src/diagnostics/mod.rs
  - crates/cupel/src/model/context_budget.rs
  - crates/cupel/tests/equality.rs
  - src/Wollax.Cupel/ContextItem.cs
  - src/Wollax.Cupel/Diagnostics/IncludedItem.cs
  - src/Wollax.Cupel/Diagnostics/ExcludedItem.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReport.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Models/ContextItemEqualityTests.cs
  - tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs
key_decisions:
  - "D103: Exact f64 comparison — no epsilon (deterministic pipelines produce identical scores)"
  - "D109: Rust PartialEq but NOT Eq (f64 fields prevent it)"
  - "D110: PartialEq added to ContextBudget as transitive requirement for OverflowEvent"
  - ".NET record == and != auto-generated from custom Equals override — no explicit operator declarations needed"
patterns_established:
  - "Clone-and-compare test pattern for Rust #[non_exhaustive] types: produce via pipeline, clone, assert clone == original"
  - "Collection-aware record equality pattern (.NET): IEquatable<T> + custom Equals with SequenceEqual for lists + element-wise for dictionaries + pragmatic O(1) GetHashCode"
observability_surfaces:
  - none — equality is a pure function with no runtime observability surface
drill_down_paths:
  - .kata/milestones/M004/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M004/slices/S01/tasks/T02-SUMMARY.md
  - .kata/milestones/M004/slices/S01/tasks/T03-SUMMARY.md
duration: 37min
verification_result: passed
completed_at: 2026-03-23T13:00:00Z
---

# S01: SelectionReport structural equality

**Structural equality on SelectionReport, IncludedItem, and ExcludedItem in both Rust (PartialEq derives) and .NET (IEquatable with collection-aware deep comparison), enabling programmatic report comparison for fork diagnostics and snapshot testing**

## What Happened

Three tasks delivered full structural equality across both languages:

**T01 (Rust):** Added `PartialEq` to 6 diagnostic structs (`SelectionReport`, `IncludedItem`, `ExcludedItem`, `TraceEvent`, `CountRequirementShortfall`, `OverflowEvent`) plus `ContextBudget` (transitive dependency). Did NOT add `Eq` — f64 fields prevent it. Created 15 integration tests using a clone-and-compare pattern (necessary because all diagnostic structs are `#[non_exhaustive]`).

**T02 (.NET ContextItem):** Added `IEquatable<ContextItem>` with custom `Equals` using `SequenceEqual` for Tags and element-wise `TryGetValue` + `object.Equals` for Metadata. Pragmatic O(1) `GetHashCode` using collection counts. 14 test cases covering tag order, metadata nulls, hash consistency.

**T03 (.NET Diagnostics):** Extended the collection-aware pattern to `IncludedItem`, `ExcludedItem` (with null-safe `DeduplicatedAgainst`), and `SelectionReport` (SequenceEqual for all four list properties). Confirmed `TraceEvent` and `CountRequirementShortfall` have correct auto-generated equality. 14 test cases. Updated `PublicAPI.Unshipped.txt` with all new surface.

## Verification

- `cargo test --all-targets` — 143 passed, 0 failed (includes 15 new equality tests)
- `cargo test --all-targets --features serde` — 192 passed, 0 failed
- `cargo clippy --all-targets -- -D warnings` — clean
- `dotnet build --configuration Release` — 0 errors, 0 warnings (14 projects)
- `dotnet test --configuration Release` — 764 passed, 0 failed (includes 28 new equality tests)

## Requirements Advanced

- R050 — SelectionReport structural equality fully implemented in both languages with exact f64 comparison

## Requirements Validated

- R050 — All must-haves met: PartialEq on 6 Rust types, IEquatable on 4 .NET types, collection-aware deep equality, exact f64 comparison, PublicAPI updated, all tests pass in both languages

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- T01: Added `PartialEq` to `ContextBudget` (not in original plan) — required transitively because `OverflowEvent` contains a `ContextBudget` field
- T02: No explicit `operator ==`/`!=` declarations needed — .NET record compiler generates them from the custom `Equals` override

## Known Limitations

- Rust types have `PartialEq` but not `Eq` due to f64 fields — cannot be used as HashMap keys or in HashSet
- .NET `GetHashCode` uses pragmatic O(1) collection contributions (count-based) — hash distribution is adequate for general use but not optimized for hash-heavy workloads

## Follow-ups

- none

## Files Created/Modified

- `crates/cupel/src/diagnostics/mod.rs` — Added PartialEq to 6 struct derives
- `crates/cupel/src/model/context_budget.rs` — Added PartialEq to ContextBudget derive
- `crates/cupel/tests/equality.rs` — 15 Rust equality integration tests
- `src/Wollax.Cupel/ContextItem.cs` — IEquatable<ContextItem> with collection-aware Equals/GetHashCode
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — IEquatable<IncludedItem>
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — IEquatable<ExcludedItem> with null-safe DeduplicatedAgainst
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — IEquatable<SelectionReport> with SequenceEqual for all lists
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 8 new public API entries for equality members
- `tests/Wollax.Cupel.Tests/Models/ContextItemEqualityTests.cs` — 14 .NET equality tests
- `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs` — 14 .NET equality tests

## Forward Intelligence

### What the next slice should know
- `report1 == report2` works in both languages — S02 can use this directly for PolicySensitivityDiff computation
- .NET equality flows through records: `IncludedItem` and `ExcludedItem` equality delegates to `ContextItem` equality automatically
- Rust equality is `PartialEq` only (not `Eq`) — use `==` operator directly, do not try to put reports in `HashSet`

### What's fragile
- .NET `GetHashCode` is O(1) approximation — if downstream slices need hash-map keyed by `SelectionReport`, hash collisions may be high; upgrade `GetHashCode` to include first-element hashes if this becomes a problem

### Authoritative diagnostics
- `crates/cupel/tests/equality.rs` — exercises all 6 Rust diagnostic types plus edge cases
- `tests/Wollax.Cupel.Tests/Diagnostics/SelectionReportEqualityTests.cs` — end-to-end .NET equality including nested ContextItem comparison

### What assumptions changed
- Original plan assumed explicit `operator ==`/`!=` needed in .NET — records generate them from custom `Equals` automatically
- ContextBudget needed PartialEq (not in original scope) — transitive requirement from OverflowEvent
