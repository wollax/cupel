---
estimated_steps: 5
estimated_files: 2
---

# T01: Define diagnostic types in `src/diagnostics/mod.rs`

**Slice:** S01 — Diagnostics Data Types
**Milestone:** M001

## Description

Create `crates/cupel/src/diagnostics/mod.rs` containing all 8 public diagnostic types required by the spec. Follow the existing `OverflowStrategy` enum pattern for enums and the `ContextItem`/`ContextBudget` struct patterns for structs. Wire the new module into `lib.rs`. All types must compile clean with no doc warnings and no clippy warnings.

## Steps

1. Create `crates/cupel/src/diagnostics/mod.rs`. Define in order:
   - `PipelineStage` enum: 5 fieldless variants (`Classify`, `Score`, `Deduplicate`, `Slice`, `Place`), `#[non_exhaustive]`, derive `Debug, Clone, Copy, PartialEq, Eq, Hash`, serde cfg_attr stub
   - `TraceEvent` struct: `pub stage: PipelineStage`, `pub duration_ms: f64`, `pub item_count: usize`, `pub message: Option<String>`, `#[non_exhaustive]`, derive `Debug, Clone`, serde cfg_attr stub
   - `OverflowEvent` struct: `pub tokens_over_budget: i64`, `pub overflowing_items: Vec<ContextItem>`, `pub budget: ContextBudget`, `#[non_exhaustive]`, derive `Debug, Clone`, serde cfg_attr stub
   - `ExclusionReason` enum: 8 variants with exact field names from spec (see below), `#[non_exhaustive]`, derive `Debug, Clone, PartialEq` (no Copy — String fields), serde cfg_attr stub with `// custom serde impl in S04 — adjacent-tagged wire format`
   - `InclusionReason` enum: 3 fieldless variants (`Scored`, `Pinned`, `ZeroToken`), `#[non_exhaustive]`, derive `Debug, Clone, Copy, PartialEq, Eq`, serde cfg_attr stub
   - `IncludedItem` struct: `pub item: ContextItem`, `pub score: f64`, `pub reason: InclusionReason`, `#[non_exhaustive]`, derive `Debug, Clone`, serde cfg_attr stub
   - `ExcludedItem` struct: `pub item: ContextItem`, `pub score: f64` (not Option — pre-score exclusions get 0.0), `pub reason: ExclusionReason`, `#[non_exhaustive]`, derive `Debug, Clone`, serde cfg_attr stub
   - `SelectionReport` struct: `pub events: Vec<TraceEvent>`, `pub included: Vec<IncludedItem>`, `pub excluded: Vec<ExcludedItem>`, `pub total_candidates: usize`, `pub total_tokens_considered: i64`, `#[non_exhaustive]`, derive `Debug, Clone`. Doc comment must note: "`excluded` is sorted by score descending, stable by insertion order on ties."

2. `ExclusionReason` variant shapes (exact field names):
   - `BudgetExceeded { item_tokens: i64, available_tokens: i64 }` — active
   - `ScoredTooLow { score: f64, threshold: f64 }` — reserved
   - `Deduplicated { deduplicated_against: String }` — active
   - `QuotaCapExceeded { kind: String, cap: i64, actual: i64 }` — reserved
   - `QuotaRequireDisplaced { displaced_by_kind: String }` — reserved
   - `NegativeTokens { tokens: i64 }` — active
   - `PinnedOverride { displaced_by: String }` — active
   - `Filtered { filter_name: String }` — reserved

3. Add full doc comments to every public type and variant. At minimum: one-line summary per type/variant, a note on reserved variants explaining they are defined for forward-compatibility but not currently emitted.

4. Edit `crates/cupel/src/lib.rs`:
   - Add `pub mod diagnostics;` after the existing `pub mod` declarations
   - Add a `pub use diagnostics::{...}` block re-exporting all 8 public types: `ExclusionReason`, `InclusionReason`, `IncludedItem`, `ExcludedItem`, `OverflowEvent`, `PipelineStage`, `SelectionReport`, `TraceEvent`

5. Run verification commands and fix any issues.

## Must-Haves

- [ ] All 8 types exist in `src/diagnostics/mod.rs`
- [ ] `ExclusionReason` has exactly 8 variants with the correct field names matching the spec
- [ ] `ExcludedItem.score` is `f64`, not `Option<f64>`
- [ ] `#[non_exhaustive]` on all 8 public types
- [ ] `#[cfg_attr(feature = "serde", derive(serde::Serialize, serde::Deserialize))]` stubs on all types
- [ ] `ExclusionReason` serde stub includes `// custom serde impl in S04` comment
- [ ] All types re-exported from `lib.rs`
- [ ] Full doc comments on all types and variants
- [ ] `cargo test` passes
- [ ] `cargo doc --no-deps` emits no warnings
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0

## Verification

```bash
cargo test
cargo doc --no-deps 2>&1 | grep -E "warning|error" && echo "DOC ISSUES" || echo "DOC OK"
cargo clippy --all-targets -- -D warnings
```

All three commands must exit 0 with no error output.

## Observability Impact

- Signals added/changed: None — this task produces type definitions only; no runtime behavior
- How a future agent inspects this: `cargo doc --no-deps --open` shows the full public API surface; `grep -r "ExclusionReason\|SelectionReport" crates/cupel/src/` confirms all types are wired
- Failure state exposed: Compile errors in `src/diagnostics/mod.rs` are the primary failure signal; `cargo doc` warnings indicate missing doc coverage

## Inputs

- `crates/cupel/src/model/overflow_strategy.rs` — enum definition pattern to follow exactly
- `crates/cupel/src/model/context_item.rs` — struct style (public fields with `#[non_exhaustive]`) and serde cfg_attr pattern
- `crates/cupel/src/lib.rs` — existing re-export surface to extend
- `spec/src/diagnostics/` — canonical spec for all field names, variant names, and types
- `.kata/DECISIONS.md` D004, D005, D015 — enum and non_exhaustive conventions locked

## Expected Output

- `crates/cupel/src/diagnostics/mod.rs` — new file with 8 fully-documented public types
- `crates/cupel/src/lib.rs` — `pub mod diagnostics;` added, all 8 types re-exported
