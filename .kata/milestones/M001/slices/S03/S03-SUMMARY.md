---
id: S03
parent: M001
milestone: M001
provides:
  - Pipeline::run_traced<C: TraceCollector> — full 5-stage diagnostics (Classify, Score, Deduplicate, Slice, Place) with per-item inclusion/exclusion reasons and stage timing
  - Pipeline::dry_run — convenience wrapper returning SelectionReport via DiagnosticTraceCollector at Item detail level; discards Vec<ContextItem>
  - Classify stage diagnostics: NegativeTokens exclusion with token value
  - Deduplicate stage diagnostics: Deduplicated exclusion with deduplicated_against content
  - Slice stage diagnostics: PinnedOverride (D023 rule) or BudgetExceeded with available_tokens (D025)
  - Place stage diagnostics: BudgetExceeded for truncated items; InclusionReason (Scored/Pinned/ZeroToken) for each result item
  - run_pipeline_diagnostics_test conformance harness — parses [expected.diagnostics.*] TOML sections and asserts SelectionReport fields
  - Five new conformance tests covering all five diagnostics vectors (NegativeTokens, Deduplicated, PinnedOverride, Scored, BudgetExceeded)
  - ClassifyResult and PlaceResult type aliases fixing pre-existing clippy::type_complexity violations
requires:
  - slice: S01
    provides: TraceEvent, ExclusionReason (all variants), InclusionReason, SelectionReport, IncludedItem, ExcludedItem, PipelineStage — all diagnostic data types
  - slice: S02
    provides: TraceCollector trait (is_enabled, record_stage_event, record_item_event, record_included, record_excluded, set_candidates), NullTraceCollector, DiagnosticTraceCollector, TraceDetailLevel, into_report()
affects:
  - S04
key_files:
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/src/pipeline/classify.rs
  - crates/cupel/src/pipeline/place.rs
  - crates/cupel/tests/conformance/pipeline.rs
key_decisions:
  - "D022 — stage function return types extended to expose excluded items directly (no external diffing needed)"
  - "D023 — PinnedOverride detection rule: pinned_tokens > 0 && item.tokens() > effective_target && item.tokens() <= budget.target_tokens() - budget.output_reserve()"
  - "D024 — dry_run discards Vec<ContextItem>; callers needing both call run_traced directly"
  - "D025 — BudgetExceeded available_tokens = effective_target - sum(sliced_item.tokens())"
  - "D026 — ClassifyResult and PlaceResult type aliases satisfy clippy::type_complexity after T01 extended return types"
  - "is_enabled() guard pattern: wrap entire diagnostic block in if collector.is_enabled() to avoid allocations in NullTraceCollector path"
  - "score_lookup HashMap<&str, f64> built from sorted items after Sort stage for O(1) score access during Place inclusion recording"
patterns_established:
  - "run_pipeline_diagnostics_test pattern: load vector → build pipeline/budget via shared helpers → run_traced → into_report → assert summary, included[], excluded[] in order with epsilon score check + variant-specific field checks"
  - "exclusion_reason_tag / inclusion_reason_tag: convert #[non_exhaustive] enums to &'static str for conformance vector comparison without serde dependency"
  - "is_enabled() guard: wraps entire diagnostic block (record_* calls + record_stage_event) — avoids all allocations in NullTraceCollector monomorphization path"
observability_surfaces:
  - "cargo test --test conformance -- pipeline::diag --nocapture — runs only the 5 diagnostics tests with full assertion messages (expected vs actual with field names)"
  - "pipeline.dry_run(&items, &budget) — returns SelectionReport with events (5 TraceEvents), included, excluded, total_candidates, total_tokens_considered"
  - "cargo doc --no-deps → Pipeline type shows run_traced and dry_run with doctests"
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

**T01** extended three internal stage functions to expose excluded items in their return values (`classify` → three-tuple with neg-token items, `deduplicate` → survivors + excluded, `place_items` → placed + truncated-with-scores), then implemented `run_traced` and `dry_run` in `pipeline/mod.rs`. The stage function extensions had already been applied in a prior session, so T01 only required changes to `mod.rs`: adding diagnostics imports, updating `run()` to destructure the new tuple returns, implementing `run_traced` with per-stage `is_enabled()` guards, and implementing `dry_run` as a one-liner wrapper.

**T02** extended the conformance harness with `run_pipeline_diagnostics_test` and five test functions. The helper reuses the existing budget/pipeline/items construction pattern from `run_pipeline_test`, then calls `pipeline.run_traced` with a `DiagnosticTraceCollector` at `TraceDetailLevel::Item` and asserts against all `[expected.diagnostics.*]` TOML sections — summary counts, included items (content, score, reason), and excluded items (content, score, reason, variant-specific fields). T02 also fixed two pre-existing `clippy::type_complexity` violations in `classify.rs` and `place.rs` introduced by T01's extended return types, adding `ClassifyResult` and `PlaceResult` type aliases.

## Verification

- `cargo test --test conformance -- pipeline` → **10/10 passed** (5 original + 5 new diagnostics vectors)
- `cargo test --lib` → **29/29 unit tests passed**
- `cargo clippy --all-targets -- -D warnings` → **zero warnings/errors**
- `cargo doc --no-deps` → **zero warnings/errors**
- `grep -E "pub fn run_traced|pub fn dry_run" src/pipeline/mod.rs` → **both methods present**

## Requirements Advanced

- R001 — `run_traced` and `dry_run` now exist on `Pipeline`; all five diagnostics conformance vectors pass end-to-end against the real implementation

## Requirements Validated

- R001 — Rust diagnostics parity fully validated: `TraceCollector` trait, `NullTraceCollector`, `DiagnosticTraceCollector`, `SelectionReport`, `run_traced()`, and `dry_run()` all exist and are conformance-verified. The only remaining R001 work is serde (S04), which is additive.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- Stage functions (`classify`, `deduplicate`, `place_items`) had already been extended with the correct return types before T01 started — only `pipeline/mod.rs` needed changes.
- Pre-existing `clippy::type_complexity` violations in `classify.rs` and `place.rs` were fixed in T02 (not T01) since they only became blocking at the clippy gate check.
- Diagnostics imports were not added to `conformance.rs` (as the task plan suggested) — they are only needed in `pipeline.rs` where they are already imported directly; adding them to the parent module created unused-import warnings.

## Known Limitations

- Serde on `SelectionReport` and related types is deferred to S04 — callers cannot yet JSON-serialize diagnostic reports.
- `TraceDetailLevel::Stage` variant exists but `run_traced` always records item-level events; stage-only recording is enforced by callers through `DiagnosticTraceCollector::new(TraceDetailLevel::Stage)` which simply disables item-level record calls inside the collector.

## Follow-ups

- S04: add `#[derive(Serialize, Deserialize)]` behind the `serde` feature to all diagnostic types; implement custom serde for `ExclusionReason` (adjacent-tagged format; stub `cfg_attr` annotations in place from S01).

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — run() updated for new tuple returns; run_traced and dry_run added with doc comments and doctests; diagnostics imports added
- `crates/cupel/src/pipeline/classify.rs` — ClassifyResult type alias; classify() return type updated to use it
- `crates/cupel/src/pipeline/place.rs` — PlaceResult type alias; place_items() return type updated to use it
- `crates/cupel/tests/conformance/pipeline.rs` — exclusion_reason_tag, inclusion_reason_tag, run_pipeline_diagnostics_test, and 5 new #[test] functions added

## Forward Intelligence

### What the next slice should know
- S04 (serde) starts with stub `cfg_attr` annotations on `ExclusionReason` in `src/diagnostics/mod.rs` from S01 — look for `// custom serde impl in S04` comments to find the exact locations.
- `DiagnosticTraceCollector` stores excluded items as `Vec<(ExcludedItem, usize)>` (with insertion index for stable score-desc sort tiebreak); `into_report()` strips the index. This internal representation is relevant when S04 adds serde to the collector output.
- The `score_lookup` in `run_traced` is `HashMap<&str, f64>` keyed on content — this works because content strings are unique within `sorted`; if that invariant ever breaks, the lookup needs to change.

### What's fragile
- `score_lookup` keyed on content: relies on content uniqueness in the `sorted` vec post-deduplication — Deduplicate stage guarantees this, but if the pipeline order changes, this assumption breaks.
- PinnedOverride detection rule (D023): the condition `item.tokens() <= budget.target_tokens() - budget.output_reserve()` is a point-in-time spec interpretation; verified against `diag-pinned-override.toml` but may need revisiting if the spec evolves.

### Authoritative diagnostics
- `cargo test --test conformance -- pipeline::diag --nocapture` — runs only the 5 diagnostics tests with full assertion output including expected vs actual field values; fastest way to diagnose any regression in the SelectionReport shape.
- `cargo doc --no-deps` — zero-warning gate on doc correctness; run this first if doc links appear broken.

### What assumptions changed
- Stage functions already had extended return types when T01 started — only `pipeline/mod.rs` needed changes; the task plan assumed all four files would need editing.
