---
id: T01
parent: S03
milestone: M001
provides:
  - Pipeline::run_traced<C: TraceCollector> — traces all 5 pipeline stages with timing, item counts, and per-item inclusion/exclusion reasons
  - Pipeline::dry_run — convenience wrapper returning a full SelectionReport via DiagnosticTraceCollector::Item
  - run() updated to destructure new three-tuple returns from classify, deduplicate, and place_items
  - Classify stage: negative-token items emitted as ExclusionReason::NegativeTokens
  - Deduplicate stage: removed duplicates emitted as ExclusionReason::Deduplicated
  - Slice stage: unselected items emitted as BudgetExceeded or PinnedOverride (D023 rule applied)
  - Place stage: truncated items emitted as BudgetExceeded; each result item recorded with InclusionReason
key_files:
  - crates/cupel/src/pipeline/mod.rs
key_decisions:
  - "Followed task plan structure: is_enabled() guards around record_* calls (aligns with protecting entire diagnostic blocks, not just TraceEvent construction)"
  - "score_lookup HashMap<&str, f64> built from sorted items before slice stage to enable O(1) score lookup during inclusion recording in Place stage"
  - "run() type annotation added: `|i: &ContextItem|` to resolve type inference ambiguity after deduplicate now returns tuple"
patterns_established:
  - "is_enabled() guard pattern: wrap entire diagnostic block (record_* calls + record_stage_event) in if collector.is_enabled() — avoids allocations in NullTraceCollector path"
  - "sliced_count HashMap pattern: count-based matching to correctly attribute exclusions when sorted contains duplicates"
observability_surfaces:
  - "pipeline.dry_run(&items, &budget) returns a fully populated SelectionReport with events (5 stages), included, excluded, total_candidates, total_tokens_considered"
  - "cargo doc --no-deps --open → Pipeline type shows run_traced and dry_run with doctests"
duration: 25min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T01: Extend stage functions and implement run_traced + dry_run

**Pipeline::run_traced<C: TraceCollector> and Pipeline::dry_run implemented with full 5-stage diagnostics; run() updated to destructure new tuple returns from classify/deduplicate/place_items.**

## What Happened

The three internal stage functions (`classify`, `deduplicate`, `place_items`) had already been updated to return extended tuples in a prior session — their signatures matched the task plan exactly. The only changes needed were in `pipeline/mod.rs`:

1. **Imports added**: `std::time::Instant`, all diagnostics types (`ExclusionReason`, `InclusionReason`, `PipelineStage`, `SelectionReport`, `TraceEvent`, `DiagnosticTraceCollector`, `TraceCollector`, `TraceDetailLevel`).

2. **`run()` updated**: destructured new return types — `(pinned, scoreable, _)` from classify, `(deduped, _)` from deduplicate, `(result, _)` from place_items. Added explicit type annotation `|i: &ContextItem|` to resolve type inference ambiguity caused by the new tuple return from deduplicate.

3. **`run_traced<C: TraceCollector>` implemented**: full 5-stage trace — Classify records neg-token exclusions, Score records stage timing, Deduplicate records excluded duplicates, Slice applies PinnedOverride vs BudgetExceeded rule (D023), Place records truncated items and per-result inclusion reasons. `score_lookup` HashMap built after Sort for O(1) score access in the Place recording block.

4. **`dry_run` implemented**: one-liner wrapper — creates `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`, calls `run_traced`, returns `collector.into_report()`.

5. **Doc link fix**: changed `[`run`]` to `[`Pipeline::run`]` in both new methods to resolve rustdoc unresolved link warnings.

## Verification

- `cargo build 2>&1 | grep "^error"` → zero errors (exit 0, grep exit 1 = no matches)
- `cargo doc --no-deps 2>&1 | grep -E "warning|error"` → zero warnings
- `cargo test --lib 2>&1 | grep -E "FAILED|^error"` → 29/29 passed, zero failures
- `cargo test --test conformance -- pipeline` → 5/5 passed (diagnostics conformance tests are T02)
- `grep -E "pub fn run_traced|pub fn dry_run" src/pipeline/mod.rs` → both methods present

## Diagnostics

- `pipeline.dry_run(&items, &budget)` returns a `SelectionReport` with `events` (5 `TraceEvent`s), `included` (per-item with score and reason), `excluded` (sorted by score desc with reason), `total_candidates`, `total_tokens_considered`
- `cargo doc --no-deps --open` → `Pipeline` type shows both methods with doctests

## Deviations

The stage functions were already updated (classify, deduplicate, place_items had correct return types). Only `pipeline/mod.rs` needed changes.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — run() updated for new tuple returns; run_traced and dry_run added with doc comments and doctests; diagnostics imports added
