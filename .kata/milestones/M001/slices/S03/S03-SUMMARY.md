---
id: S03
parent: M001
milestone: M001
provides:
  - Pipeline::run_traced<C: TraceCollector> — traces all 5 stages with timing, item counts, and per-item inclusion/exclusion reasons
  - Pipeline::dry_run — convenience wrapper returning SelectionReport via DiagnosticTraceCollector::Item
  - run() updated to destructure new tuple returns from classify/deduplicate/place_items
  - Classify stage: negative-token items emitted as ExclusionReason::NegativeTokens{tokens}
  - Deduplicate stage: removed duplicates emitted as ExclusionReason::Deduplicated{deduplicated_against}
  - Slice stage: unselected items emitted as BudgetExceeded or PinnedOverride (D023 rule applied)
  - Place stage: truncated items emitted as BudgetExceeded; each result item recorded with InclusionReason
  - run_pipeline_diagnostics_test harness helper — parses [expected.diagnostics.*] TOML sections and asserts SelectionReport
  - 5 diagnostics conformance test functions: diag_negative_tokens, diag_deduplicated, diag_pinned_override, diag_scored_inclusion, diagnostics_budget_exceeded
  - ClassifyResult and PlaceResult type aliases (clippy::type_complexity fix in classify.rs and place.rs)
requires:
  - slice: S01
    provides: TraceEvent, ExclusionReason, InclusionReason, SelectionReport, IncludedItem, ExcludedItem, PipelineStage — all diagnostic data types
  - slice: S02
    provides: TraceCollector trait, NullTraceCollector, DiagnosticTraceCollector, TraceDetailLevel, into_report() — all collector infrastructure
affects:
  - S04
  - S05
  - S07
key_files:
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/tests/conformance/pipeline.rs
  - crates/cupel/src/pipeline/classify.rs
  - crates/cupel/src/pipeline/place.rs
key_decisions:
  - "D022: stage function return type extensions (classify/deduplicate/place_items return excluded items directly to run_traced)"
  - "D023: PinnedOverride detection rule at Slice stage (pinned_tokens > 0 && item fits in unreserved budget)"
  - "D024: dry_run discards Vec<ContextItem> — convenience API for explain-why use case"
  - "D025: BudgetExceeded available_tokens = effective_target - sum(sliced_item.tokens())"
  - "D026: ClassifyResult and PlaceResult type aliases to satisfy clippy::type_complexity"
  - "is_enabled() guard pattern: wrap entire diagnostic block in if collector.is_enabled() to protect NullTraceCollector zero-cost path"
  - "score_lookup HashMap<&str, f64> built from sorted items before Slice stage for O(1) score access during Place inclusion recording"
patterns_established:
  - "is_enabled() guard: wrap entire record_* block (not just TraceEvent construction) in if collector.is_enabled() — avoids all allocations in NullTraceCollector path"
  - "run_pipeline_diagnostics_test pattern: load vector → build pipeline/budget via shared helpers → run_traced → into_report → assert summary, included[], excluded[] in order with epsilon score + variant field checks"
  - "exclusion_reason_tag / inclusion_reason_tag: convert #[non_exhaustive] enums to &'static str for vector comparison without serde dependency"
observability_surfaces:
  - "pipeline.dry_run(&items, &budget) — returns SelectionReport with 5 TraceEvents, included[], excluded[], total_candidates, total_tokens_considered"
  - "cargo test --test conformance -- pipeline::diag --nocapture — runs only the 5 new diagnostics tests with per-field assertion messages"
  - "cargo doc --no-deps --open — Pipeline type shows run_traced and dry_run with doctests"
drill_down_paths:
  - .kata/milestones/M001/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S03/tasks/T02-SUMMARY.md
duration: 45min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
---

# S03: Pipeline run_traced & DryRun

**`Pipeline::run_traced<C: TraceCollector>` and `Pipeline::dry_run` implemented with full 5-stage diagnostics; all 10 pipeline conformance tests pass (5 existing + 5 new diagnostics vectors).**

## What Happened

**T01** extended three internal stage functions to return excluded items as part of their output, then added the two new public pipeline methods. The stage function extensions (`classify` → three-tuple, `deduplicate` → two-tuple, `place_items` → two-tuple with truncated items) were already in place from a prior session, so T01's only changes were in `pipeline/mod.rs`: adding diagnostics imports, updating `run()` to destructure new return types, implementing `run_traced<C: TraceCollector>` with full 5-stage trace instrumentation, and implementing `dry_run` as a one-liner wrapper. The `score_lookup` HashMap is built from `sorted` items after the Score stage for O(1) score access during Place recording. The is_enabled() guard wraps entire diagnostic blocks to preserve the NullTraceCollector zero-cost invariant.

**T02** added the conformance harness extension: `exclusion_reason_tag`, `inclusion_reason_tag`, and `run_pipeline_diagnostics_test` in `tests/conformance/pipeline.rs`, plus 5 new test functions covering all five diagnostics vectors. The helper reuses existing budget/pipeline/items construction, calls `run_traced` with `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`, and asserts every field defined in the vector's `[expected.diagnostics.*]` sections including variant-specific data fields (NegativeTokens.tokens, Deduplicated.deduplicated_against, BudgetExceeded.item_tokens/available_tokens, PinnedOverride.displaced_by). T02 also fixed two pre-existing `clippy::type_complexity` lints in classify.rs and place.rs with `ClassifyResult` and `PlaceResult` type aliases, which was required to satisfy the zero-warning clippy gate.

## Verification

- `cargo test --test conformance -- pipeline` → 10/10 passed (5 existing + 5 new diagnostics), zero failures
- `cargo test --lib` → 29/29 unit tests passed, zero failures
- `cargo clippy --all-targets -- -D warnings` → zero warnings/errors
- `cargo doc --no-deps` → zero warnings/errors
- `grep -E "pub fn run_traced|pub fn dry_run" src/pipeline/mod.rs` → both methods present

## Requirements Advanced

- R001 — `run_traced` and `dry_run` now exist in the Rust crate; all 5 diagnostics conformance vectors pass. R001 is now fully implemented; final validation pending S04 serde integration and S05 CI hardening, but the runtime behavior is proven.

## Requirements Validated

- None validated in this slice. R001 advances to implementation-complete but full validation requires S04 (serde round-trip) to confirm end-to-end pipeline.

## New Requirements Surfaced

- None.

## Requirements Invalidated or Re-scoped

- None.

## Deviations

- **Stage functions pre-extended**: The extended return types for `classify`, `deduplicate`, and `place_items` were already in place from a prior session. T01 only needed to update `pipeline/mod.rs`.
- **T02 conformance.rs import unchanged**: Task plan called for adding diagnostics imports to `conformance.rs` (parent module), but they are only needed in `pipeline.rs` and adding them to the parent created unused-import warnings. Imports remain in `pipeline.rs` only.
- **ClassifyResult/PlaceResult type aliases**: Not in the original task plan; required to satisfy the zero-warning clippy gate after stage function return types were extended.

## Known Limitations

- R001 not yet fully validated: `SelectionReport` and all diagnostic types lack serde support until S04. Callers cannot persist or transmit diagnostic reports until S04 ships.
- The `is_enabled()` zero-cost invariant is enforced by code convention (guards in run_traced), not by compiler-verified zero-allocation proof. A future micro-benchmark test could confirm no allocations occur in the NullTraceCollector path.

## Follow-ups

- S04: add `#[derive(Serialize, Deserialize)]` behind `serde` feature to all diagnostic types; serde round-trip test on `SelectionReport`
- S05: add `cargo clippy --all-targets -- -D warnings` and `cargo-deny` unmaintained warning to CI
- S07: resolve remaining Rust quality issues; KnapsackSlice DP guard (`CupelError::TableTooLarge`)

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — `run_traced` and `dry_run` added; `run()` updated for new tuple returns; diagnostics imports added
- `crates/cupel/tests/conformance/pipeline.rs` — `exclusion_reason_tag`, `inclusion_reason_tag`, `run_pipeline_diagnostics_test`, and 5 new `#[test]` functions added
- `crates/cupel/src/pipeline/classify.rs` — `ClassifyResult` type alias added; `classify()` return type updated
- `crates/cupel/src/pipeline/place.rs` — `PlaceResult` type alias added; `place_items()` return type updated

## Forward Intelligence

### What the next slice should know
- `SelectionReport`, `TraceEvent`, `ExclusionReason`, `InclusionReason` all lack `#[derive(Serialize, Deserialize)]` — this is the primary S04 deliverable. The cfg_attr stubs added in S01 are in place; S04 fills them in.
- `DiagnosticTraceCollector::into_report()` consumes the collector (`self`) — callers cannot call `run_traced` and then inspect the collector separately; they must call `into_report()` to extract the `SelectionReport`.
- The conformance harness in `tests/conformance/pipeline.rs` is the authoritative integration test surface. When S04 adds serde, a separate serde round-trip test file is the right home (not adding to pipeline.rs).

### What's fragile
- `score_lookup` HashMap uses `&str` keys from `item.content()` — if two items have identical content but different scores (which should not happen after deduplication), only one score survives. The deduplication stage guarantees this won't occur in practice, but the assumption is implicit.
- The PinnedOverride detection rule (D023) is tied to the exact form of `effective_target` as computed in the Slice stage. If `effective_target` computation changes, the rule boundary must be re-verified against the `diag-pinned-override.toml` vector.

### Authoritative diagnostics
- `cargo test --test conformance -- pipeline::diag --nocapture` — runs only the 5 diagnostics tests with full field-level assertion messages; this is the primary signal for any S03-related failures
- `cargo test --test conformance -- pipeline` — all 10 pipeline conformance tests; fast end-to-end check

### What assumptions changed
- Stage functions were assumed to need extension in T01 — they were already extended, reducing T01 scope to only `pipeline/mod.rs` changes.
