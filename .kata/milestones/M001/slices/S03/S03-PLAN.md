# S03: Pipeline run_traced & DryRun

**Goal:** `Pipeline::run_traced<C: TraceCollector>` and `Pipeline::dry_run` exist on the Rust `Pipeline` struct; each of the five diagnostics pipeline stages emits correct inclusion/exclusion reasons; all five diagnostics conformance vectors pass in the integration test harness.
**Demo:** `cargo test --test conformance -- pipeline` shows 5 new passing diagnostics tests alongside the existing 5 pipeline tests; `cargo doc --no-deps` and `cargo clippy --all-targets -- -D warnings` emit zero warnings.

## Must-Haves

- `Pipeline::run_traced<C: TraceCollector>(&self, items: &[ContextItem], budget: &ContextBudget, collector: &mut C) -> Result<Vec<ContextItem>, CupelError>` — per-invocation ownership (D001); coexists with `run()` without breaking it (D002)
- `Pipeline::dry_run(&self, items: &[ContextItem], budget: &ContextBudget) -> Result<SelectionReport, CupelError>` — internally creates `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`, calls `run_traced`, returns `collector.into_report()`; discards the `Vec<ContextItem>` (D024)
- `run_traced` calls `collector.set_candidates(items.len(), total_tokens)` before Stage 1; `total_tokens = items.iter().map(|i| i.tokens()).sum::<i64>()`
- **Classify stage**: records `ExclusionReason::NegativeTokens { tokens }` for each item with `tokens < 0`; records `PipelineStage::Classify` stage event with elapsed duration and surviving item count
- **Score stage**: records `PipelineStage::Score` stage event
- **Deduplicate stage**: records `ExclusionReason::Deduplicated { deduplicated_against: content.to_owned() }` for each removed duplicate with the removed item's score; records `PipelineStage::Deduplicate` stage event
- **Slice stage**: for each item in `sorted` not in `sliced`, emits `ExclusionReason::PinnedOverride { displaced_by }` when `pinned_tokens > 0 && item.tokens() > effective_target && item.tokens() <= budget.target_tokens() - budget.output_reserve()` (D023); otherwise emits `ExclusionReason::BudgetExceeded { item_tokens, available_tokens }` where `available_tokens = effective_target - sum(sliced_tokens)` (D025); records `PipelineStage::Slice` stage event; for PinnedOverride: `displaced_by = pinned.first().map(|p| p.content().to_owned()).unwrap_or_default()`
- **Place stage**: records `PipelineStage::Place` stage event; records `ExclusionReason::BudgetExceeded` for any items dropped by Truncate overflow; records each item in final result with correct `InclusionReason` (Pinned if `item.pinned()`, ZeroToken if `item.tokens() == 0`, Scored otherwise) and score (1.0 for pinned; score from `sorted` lookup for non-pinned)
- Internal stage functions `classify::classify`, `deduplicate::deduplicate`, and `place::place_items` are extended to return excluded/truncated items as part of their output so `run_traced` can record them without diffing (D022)
- `run()` is updated to destructure new return types — behavior unchanged
- Conformance harness in `tests/conformance/pipeline.rs` gains a `run_pipeline_diagnostics_test` function that parses `[expected.diagnostics.*]` TOML sections and asserts against the `SelectionReport` from `run_traced`; five new test functions cover all five diagnostics vectors
- `cargo test --test conformance -- pipeline` passes (10 total: 5 existing + 5 new)
- `cargo test --lib` passes (all 29 existing unit tests)
- `cargo clippy --all-targets -- -D warnings` zero warnings
- `cargo doc --no-deps` zero warnings

## Proof Level

- This slice proves: integration
- Real runtime required: no (test harness with constructed items; no external services)
- Human/UAT required: no

Integration justification: Unlike S01/S02 (contract-level), this slice runs real pipeline execution end-to-end with real input items and asserts that the output `SelectionReport` matches specification-authored conformance vectors. The conformance harness exercises the full Classify → Score → Deduplicate → Sort → Slice → Place path and validates inclusion/exclusion reasons, scores, and summary counts.

## Verification

- `cd crates/cupel && cargo test --test conformance -- pipeline 2>&1 | grep -E "FAILED|^error"` → zero failures (10 tests pass: 5 existing + 5 new diagnostics)
- `cd crates/cupel && cargo test --lib 2>&1 | grep -E "FAILED|^error"` → zero failures (29 unit tests unchanged)
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "^warning|^error"` → zero
- `cd crates/cupel && cargo doc --no-deps 2>&1 | grep -E "warning|error"` → zero
- `cd crates/cupel && grep -E 'run_traced|dry_run' src/pipeline/mod.rs | grep "pub fn"` → both methods present

## Observability / Diagnostics

- Runtime signals: None (library types; no runtime process)
- Inspection surfaces: `cargo test --test conformance -- pipeline::diag --nocapture` — runs only the 5 new diagnostics tests with full assertion messages on failure; assertion messages name the mismatched field (content, score, reason, count)
- Failure visibility: `cargo test` names the failing test and line; the diagnostics assert helpers should print the full expected vs actual `SelectionReport` on failure; `cargo clippy --all-targets` names the lint and file location
- Redaction constraints: None

## Integration Closure

- Upstream surfaces consumed:
  - `crates/cupel/src/diagnostics/trace_collector.rs` — `TraceCollector` trait (`is_enabled`, `record_stage_event`, `record_item_event`, `record_included`, `record_excluded`, `set_candidates`), `DiagnosticTraceCollector`, `TraceDetailLevel` (all from S02)
  - `crates/cupel/src/diagnostics/mod.rs` — `ExclusionReason`, `InclusionReason`, `PipelineStage`, `SelectionReport`, `TraceEvent` (all from S01)
  - `crates/cupel/conformance/required/pipeline/diag-*.toml` — 5 diagnostics conformance vectors (authored in S01)
- New wiring introduced in this slice:
  - `Pipeline::run_traced<C: TraceCollector>` and `Pipeline::dry_run` added to `pipeline/mod.rs`
  - `classify::classify`, `deduplicate::deduplicate`, `place::place_items` return signatures extended (internal only)
  - Conformance harness gains diagnostics parsing and 5 new test functions
- What remains before the milestone is truly usable end-to-end: S04 (serde on `SelectionReport` and `DiagnosticTraceCollector`); S05–S07 (quality hardening); nothing blocks external callers from using `run_traced` and `dry_run`

## Tasks

- [x] **T01: Extend stage functions and implement run_traced + dry_run** `est:50m`
  - Why: Creates the two new public pipeline methods (R001 primary deliverable); extends three internal stage functions to return excluded items, making `run_traced` straightforward to implement without re-implementing stage logic (D022)
  - Files: `crates/cupel/src/pipeline/classify.rs`, `crates/cupel/src/pipeline/deduplicate.rs`, `crates/cupel/src/pipeline/place.rs`, `crates/cupel/src/pipeline/mod.rs`
  - Do: (1) Extend `classify::classify` return type to `Result<(Vec<ContextItem>, Vec<ContextItem>, Vec<ContextItem>), CupelError>` (pinned, scoreable, neg_token_items); collect negative-token items into a separate `neg_items` vec instead of silently continuing; update `run()` to destructure: `let (pinned, scoreable, _) = classify::classify(...)?;`. (2) Extend `deduplicate::deduplicate` return type to `(Vec<ScoredItem>, Vec<ScoredItem>)` (survivors, excluded); track surviving indices via `best_by_content` map (already present); return items NOT in the survivors map as excluded; update `run()`: `let (deduped, _) = deduplicate::deduplicate(...);`. (3) Extend `place::place_items` and `handle_overflow` return types: `place_items` returns `Result<(Vec<ContextItem>, Vec<(ContextItem, f64)>), CupelError>` where second is `(truncated_item, score)` pairs; `handle_overflow` returns `Result<(Vec<ScoredItem>, Vec<ScoredItem>), CupelError>` (kept, dropped); for Throw/Proceed: dropped is empty; for Truncate: collect dropped items from the greedy pass; update `run()`: `let (result, _) = place::place_items(...)?;`. (4) Add `use std::time::Instant;` and `use crate::diagnostics::{ExclusionReason, InclusionReason, PipelineStage, SelectionReport, TraceEvent}; use crate::diagnostics::trace_collector::{DiagnosticTraceCollector, TraceCollector, TraceDetailLevel};` to `pipeline/mod.rs`. (5) Implement `run_traced<C: TraceCollector>` on `Pipeline`: call `set_candidates`; for each stage, call the stage function, record excluded items (from extended return values or inline for Slice), then record the stage TraceEvent using `Instant::now()` timing; after Place, build a score lookup from `sorted` and call `record_included` for each final item with the correct InclusionReason; for Slice exclusions, compute `available_tokens = effective_target - sliced.iter().map(|i| i.tokens()).sum::<i64>()` and apply PinnedOverride rule (D023) before defaulting to BudgetExceeded. (6) Implement `dry_run` on `Pipeline`: create `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`, call `self.run_traced(items, budget, &mut collector)?`, return `Ok(collector.into_report())`. (7) Write doc comments and a doctest for both `run_traced` and `dry_run` following the existing `run()` example style.
  - Verify: `cd crates/cupel && cargo build 2>&1 | grep "^error"` → zero errors; `cargo doc --no-deps 2>&1 | grep -E "warning|error"` → zero warnings; `cargo test --lib 2>&1 | grep -E "FAILED|^error"` → zero failures (29 still pass); `grep -E "pub fn run_traced|pub fn dry_run" src/pipeline/mod.rs` → both present
  - Done when: crate compiles with zero errors and zero doc warnings; all 29 existing unit tests still pass; both methods are present in the compiled binary

- [x] **T02: Conformance harness: diagnostics parsing and 5 test cases** `est:30m`
  - Why: Proves the run_traced + dry_run implementation is correct against the specification-authored conformance vectors (the primary R001 validation gate for this slice); closes the `expected.diagnostics.*` sections in all five diag vectors that have been inert since S01
  - Files: `crates/cupel/tests/conformance/pipeline.rs`, `crates/cupel/tests/conformance.rs`
  - Do: (1) In `conformance.rs`, add `DiagnosticTraceCollector, SelectionReport, TraceDetailLevel` to the `use cupel::{...}` import block. (2) In `pipeline.rs`, add a private helper `exclusion_reason_tag(reason: &cupel::ExclusionReason) -> &'static str` that maps each variant to its tag string ("BudgetExceeded", "NegativeTokens", "Deduplicated", "PinnedOverride", etc.). (3) Add `run_pipeline_diagnostics_test(vector_path: &str)` that: loads vector and builds pipeline/items/budget via existing helpers; creates `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`; calls `pipeline.run_traced(&items, &budget, &mut collector).expect("run_traced should succeed")`; calls `collector.into_report()` to get `SelectionReport`; reads `[expected.diagnostics.summary]` — assert `report.total_candidates == expected` and `report.total_tokens_considered == expected`; reads `[[expected.diagnostics.included]]` — assert count matches, then for each entry (in order) assert `content`, `score_approx` within epsilon (default 1e-9), and `inclusion_reason` string match against `match reason { InclusionReason::Scored => "Scored", InclusionReason::Pinned => "Pinned", InclusionReason::ZeroToken => "ZeroToken", _ => "Unknown" }`; reads `[[expected.diagnostics.excluded]]` — assert count matches, then for each entry assert `content`, `score_approx`, `exclusion_reason` via `exclusion_reason_tag`; for BudgetExceeded also assert `item_tokens` and `available_tokens` if present in vector; for NegativeTokens assert `tokens` if present; for Deduplicated assert `deduplicated_against` if present; for PinnedOverride assert `displaced_by` if present; use `score_epsilon` from vector's `[tolerance]` section if present, else 1e-9. (4) Add 5 test functions: `fn diag_negative_tokens()`, `fn diag_deduplicated()`, `fn diag_pinned_override()`, `fn diag_scored_inclusion()`, `fn diagnostics_budget_exceeded()` — each calls `run_pipeline_diagnostics_test("pipeline/<vector-name>.toml")`.
  - Verify: `cd crates/cupel && cargo test --test conformance -- pipeline 2>&1 | grep -E "FAILED|^error"` → zero failures (10 tests: 5 old + 5 new); `cargo clippy --all-targets -- -D warnings 2>&1 | grep -E "^warning|^error"` → zero
  - Done when: all 10 pipeline conformance tests pass (5 existing unchanged, 5 new diagnostics); clippy clean

## Files Likely Touched

- `crates/cupel/src/pipeline/mod.rs` — add `run_traced` and `dry_run` methods, add imports
- `crates/cupel/src/pipeline/classify.rs` — extend return type to include neg-token items
- `crates/cupel/src/pipeline/deduplicate.rs` — extend return type to include excluded items
- `crates/cupel/src/pipeline/place.rs` — extend return type to include truncated items
- `crates/cupel/tests/conformance/pipeline.rs` — add diagnostics test function + 5 test cases
- `crates/cupel/tests/conformance.rs` — add DiagnosticTraceCollector/TraceDetailLevel to imports
