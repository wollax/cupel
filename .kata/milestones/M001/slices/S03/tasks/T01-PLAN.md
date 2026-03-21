---
estimated_steps: 7
estimated_files: 4
---

# T01: Extend stage functions and implement run_traced + dry_run

**Slice:** S03 — Pipeline run_traced & DryRun
**Milestone:** M001

## Description

Extend three internal `pub(crate)` stage functions to return their excluded/truncated items as part of their output tuples, then implement `Pipeline::run_traced<C: TraceCollector>` and `Pipeline::dry_run` on the existing `Pipeline` struct. The stage-function extensions eliminate the need for external diffing in `run_traced`, keeping the implementation clean and correct for duplicate-content scenarios. `run()` is updated minimally to destructure the new return types with no behavioral change.

## Steps

1. **Extend `classify::classify`** — change return type to `Result<(Vec<ContextItem>, Vec<ContextItem>, Vec<ContextItem>), CupelError>` (pinned, scoreable, neg_token_items). Replace the `continue` on negative-token items with a push to a `neg_items: Vec<ContextItem>` vec; return it as the third tuple element. In `pipeline/mod.rs` `run()`: change `let (pinned, scoreable) = classify::classify(...)` to `let (pinned, scoreable, _) = classify::classify(...)`.

2. **Extend `deduplicate::deduplicate`** — change return type to `(Vec<ScoredItem>, Vec<ScoredItem>)` (survivors, excluded). After building `best_by_content: HashMap<String, usize>`, split the input `scored` vec into survivors (where `best_by_content.get(content) == Some(i)`) and excluded (everything else). Return both. In `pipeline/mod.rs` `run()`: change to `let (deduped, _) = deduplicate::deduplicate(...)`.

3. **Extend `place::place_items` and `handle_overflow`** — change `handle_overflow` to return `Result<(Vec<ScoredItem>, Vec<ScoredItem>), CupelError>` (kept, dropped). For `Throw`: return `Err(...)` unchanged. For `Truncate`: collect items that don't make the greedy cut into a `dropped: Vec<ScoredItem>` vec and return `(kept, dropped)`. For `Proceed`: return `(merged, vec![])`. Update `place_items` to return `Result<(Vec<ContextItem>, Vec<(ContextItem, f64)>), CupelError>` where the second element is `dropped.into_iter().map(|si| (si.item, si.score)).collect()`. In `pipeline/mod.rs` `run()`: change to `let (result, _) = place::place_items(...)`.

4. **Add imports to `pipeline/mod.rs`** — add at the top of the file:
   ```rust
   use std::time::Instant;
   use crate::diagnostics::{
       ExclusionReason, InclusionReason, PipelineStage, SelectionReport, TraceEvent,
   };
   use crate::diagnostics::trace_collector::{
       DiagnosticTraceCollector, TraceCollector, TraceDetailLevel,
   };
   ```

5. **Implement `run_traced<C: TraceCollector>`** on `Pipeline`. The method takes `&self, items: &[ContextItem], budget: &ContextBudget, collector: &mut C` and returns `Result<Vec<ContextItem>, CupelError>`. Internal flow:
   - `collector.set_candidates(items.len(), items.iter().map(|i| i.tokens()).sum())`
   - **Classify**: `let t = Instant::now(); let (pinned, scoreable, neg_items) = classify::classify(items, budget)?;` — then if `collector.is_enabled()`: record each neg_item with `record_excluded(item.clone(), 0.0, ExclusionReason::NegativeTokens { tokens: item.tokens() })`, then `record_stage_event(TraceEvent { stage: PipelineStage::Classify, duration_ms: t.elapsed().as_secs_f64() * 1000.0, item_count: pinned.len() + scoreable.len(), message: None })`
   - **Score**: `let t = Instant::now(); let scored = score::score_items(...);` — if enabled: `record_stage_event(... Score, scored.len() ...)`
   - **Deduplicate**: `let t = Instant::now(); let (deduped, ded_excluded) = deduplicate::deduplicate(scored, self.deduplication);` — if enabled: for each excluded `ScoredItem`, call `record_excluded(si.item.clone(), si.score, ExclusionReason::Deduplicated { deduplicated_against: si.item.content().to_owned() })`; then `record_stage_event(... Deduplicate, deduped.len() ...)`
   - **Sort**: `let sorted = sort::sort_scored(deduped);` — no stage event (no `PipelineStage::Sort` variant)
   - **Build score lookup for later use**: `let score_lookup: std::collections::HashMap<&str, f64> = sorted.iter().map(|si| (si.item.content(), si.score)).collect();`
   - **Compute pinned_tokens and effective_target**: `let pinned_tokens: i64 = pinned.iter().map(|i| i.tokens()).sum(); let effective_budget = slice::compute_effective_budget(budget, pinned_tokens); let effective_target = effective_budget.target_tokens();`
   - **Slice**: `let t = Instant::now(); let sliced = slice::slice_items(&sorted, budget, pinned_tokens, self.slicer.as_ref());` — if enabled: compute `sliced_total: i64 = sliced.iter().map(|i| i.tokens()).sum(); let available_tokens = effective_target - sliced_total;` — build `sliced_count: HashMap<&str, usize>` from sliced; iterate `sorted` in order, consuming sliced_count; for each unmatched item, apply PinnedOverride rule (D023): if `pinned_tokens > 0 && si.item.tokens() > effective_target && si.item.tokens() <= budget.target_tokens() - budget.output_reserve()` → `ExclusionReason::PinnedOverride { displaced_by: pinned.first().map(|p| p.content().to_owned()).unwrap_or_default() }`; else → `ExclusionReason::BudgetExceeded { item_tokens: si.item.tokens(), available_tokens }`; call `record_excluded(si.item.clone(), si.score, reason)`; then `record_stage_event(... Slice, sliced.len() ...)`
   - **Place**: `let t = Instant::now(); let (result, truncated) = place::place_items(&pinned, &sliced, &sorted, budget, self.overflow_strategy, self.placer.as_ref())?;` — if enabled: for each `(item, score)` in truncated: `record_excluded(item.clone(), score, ExclusionReason::BudgetExceeded { item_tokens: item.tokens(), available_tokens: budget.target_tokens() - result.iter().map(|i| i.tokens()).sum::<i64>() })`; then `record_stage_event(... Place, result.len() ...)`. After stage event: for each item in `result`: determine reason and score — if `item.pinned()` → `(1.0, InclusionReason::Pinned)`; else if `item.tokens() == 0` → `(score_lookup.get(item.content()).copied().unwrap_or(0.0), InclusionReason::ZeroToken)`; else → `(score_lookup.get(item.content()).copied().unwrap_or(0.0), InclusionReason::Scored)`; call `record_included(item.clone(), score, reason)`
   - Return `Ok(result)`

6. **Implement `dry_run`** on `Pipeline`. Takes `&self, items: &[ContextItem], budget: &ContextBudget`. Returns `Result<SelectionReport, CupelError>`. Body: `let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item); self.run_traced(items, budget, &mut collector)?; Ok(collector.into_report())`. Doc comment: "Runs the pipeline and returns a full `SelectionReport` without side effects. Equivalent to calling `run_traced` with a `DiagnosticTraceCollector::Item` collector and discarding the `Vec<ContextItem>`."

7. **Write doc comments and a doctest** for both methods in `pipeline/mod.rs` following the existing `run()` example style. `run_traced` doctest should show creation of a `DiagnosticTraceCollector`, calling `run_traced`, and checking `report.included.len()`. `dry_run` doctest should show calling `dry_run` and checking `report.total_candidates`.

## Must-Haves

- [ ] `classify::classify` returns three-tuple including `neg_token_items`; `run()` updated to destructure with `_`
- [ ] `deduplicate::deduplicate` returns `(survivors, excluded)`; `run()` updated
- [ ] `place::place_items` returns `(placed, truncated_with_scores)`; `run()` updated
- [ ] `Pipeline::run_traced<C: TraceCollector>` exists with the correct signature (per-invocation ownership per D001)
- [ ] `Pipeline::dry_run` exists; internally wraps `run_traced` with `DiagnosticTraceCollector::Item` (D024)
- [ ] All five pipeline stages emit a `TraceEvent` (Classify, Score, Deduplicate, Slice, Place)
- [ ] Classify stage: negative-token items recorded with `ExclusionReason::NegativeTokens`
- [ ] Deduplicate stage: removed duplicates recorded with `ExclusionReason::Deduplicated { deduplicated_against }`
- [ ] Slice stage: unselected items recorded with `BudgetExceeded` or `PinnedOverride` (D023); `available_tokens = effective_target - sliced_total` (D025)
- [ ] Place stage: each result item recorded with correct `InclusionReason` and score; truncated items recorded with `BudgetExceeded`
- [ ] `cargo build 2>&1 | grep "^error"` → zero errors
- [ ] `cargo doc --no-deps 2>&1 | grep -E "warning|error"` → zero warnings
- [ ] `cargo test --lib 2>&1 | grep -E "FAILED|^error"` → zero failures (29 unchanged)

## Verification

- `cd crates/cupel && cargo build 2>&1 | grep "^error"` → zero errors
- `cd crates/cupel && cargo doc --no-deps 2>&1 | grep -E "warning|error"` → zero warnings
- `cd crates/cupel && cargo test --lib 2>&1 | grep -E "FAILED|^error"` → zero failures
- `cd crates/cupel && grep -E "pub fn run_traced|pub fn dry_run" src/pipeline/mod.rs` → both lines present

## Observability Impact

- Signals added/changed: `run_traced` is the primary observability signal — it populates a `SelectionReport` that exposes stage-level timing, item counts, inclusion/exclusion reasons, and per-item scores for any pipeline run
- How a future agent inspects this: `pipeline.dry_run(&items, &budget)` returns a fully populated `SelectionReport`; `cargo doc --no-deps --open` shows both new methods on the `Pipeline` type
- Failure state exposed: if `run_traced` returns `Err`, the collector state is dropped (into_report is not called); if a stage panics, the collector is lost; this is expected behavior (D018 convention)

## Inputs

- `crates/cupel/src/diagnostics/trace_collector.rs` — `TraceCollector` trait with 6 methods including `set_candidates`, `record_included`, `record_excluded`, `record_stage_event`; `DiagnosticTraceCollector`; `TraceDetailLevel`; all from S02
- `crates/cupel/src/diagnostics/mod.rs` — `ExclusionReason` (all 8 variants), `InclusionReason` (Scored/Pinned/ZeroToken), `PipelineStage` (5 variants), `TraceEvent`, `SelectionReport`; all from S01
- `crates/cupel/src/pipeline/classify.rs` — current `classify` function; modify to return neg_token_items
- `crates/cupel/src/pipeline/deduplicate.rs` — current `deduplicate` function with `best_by_content` map; modify to return excluded
- `crates/cupel/src/pipeline/place.rs` — current `place_items` and `handle_overflow`; modify to return truncated
- `crates/cupel/src/pipeline/slice.rs` — `compute_effective_budget` (needed for PinnedOverride detection in T01 step 5); `slice_items` (no change)
- S02 Forward Intelligence (from S02-SUMMARY.md): "record_included/excluded/set_candidates are defaulted trait methods; NullTraceCollector inherits them as no-ops. S03 should call these generically — no if is_enabled() guard needed before calling them." Also: "is_enabled() is the right guard before constructing expensive TraceEvent payloads."

## Expected Output

- `crates/cupel/src/pipeline/classify.rs` — `classify` returns `Result<(Vec<ContextItem>, Vec<ContextItem>, Vec<ContextItem>), CupelError>` with neg-token items as third element
- `crates/cupel/src/pipeline/deduplicate.rs` — `deduplicate` returns `(Vec<ScoredItem>, Vec<ScoredItem>)` with excluded items as second element
- `crates/cupel/src/pipeline/place.rs` — `place_items` returns `Result<(Vec<ContextItem>, Vec<(ContextItem, f64)>), CupelError>` with truncated items as second element; `handle_overflow` returns `Result<(Vec<ScoredItem>, Vec<ScoredItem>), CupelError>`
- `crates/cupel/src/pipeline/mod.rs` — `run()` updated to destructure new return types; `run_traced<C: TraceCollector>` and `dry_run` added with full doc comments; imports for diagnostics types added
