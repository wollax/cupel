---
estimated_steps: 7
estimated_files: 1
---

# T02: Wire snapshot collection into run_with_components and call on_pipeline_completed

**Slice:** S01 — Add on_pipeline_completed hook to core cupel TraceCollector
**Milestone:** M008

## Description

Implement the snapshot-collection wiring in `Pipeline::run_with_components`. This is the behavioral heart of S01:

1. Inside each existing `if collector.is_enabled()` block (one per stage), construct a `StageTraceSnapshot` and push it onto a running `Vec<StageTraceSnapshot>`.
2. After the Place stage, build a synthetic `SelectionReport` from the snapshot data + `result` + `score_lookup`.
3. Call `collector.on_pipeline_completed(&report, budget, &snapshots)`.

When this task is complete, the failing T01 integration test passes, all 167 prior tests still pass, and clippy is clean. No changes to any file except `pipeline/mod.rs`.

## Steps

1. At the top of `run_with_components`, after the `collector.set_candidates(...)` call, add:
   ```rust
   let total_tokens_considered: i64 = if collector.is_enabled() {
       items.iter().map(|i| i.tokens()).sum()
   } else {
       0
   };
   let mut stage_snapshots: Vec<crate::diagnostics::StageTraceSnapshot> =
       if collector.is_enabled() { Vec::with_capacity(5) } else { Vec::new() };
   ```
   Note: `total_tokens_considered` is already computed inline in the `set_candidates` call. Extract it to a local here so it's available at the end of the function for the synthetic report.

2. In the **Classify** stage `if collector.is_enabled()` block, after pushing the `TraceEvent`, push a snapshot:
   ```rust
   stage_snapshots.push(StageTraceSnapshot {
       stage: PipelineStage::Classify,
       item_count_in: items.len(),
       item_count_out: pinned.len() + scoreable.len(),
       duration_ms: /* the classify duration already computed */,
       excluded: neg_items.iter().map(|item| ExcludedItem {
           item: item.clone(),
           score: 0.0,
           reason: ExclusionReason::NegativeTokens { tokens: item.tokens() },
       }).collect(),
   });
   ```
   The classify duration is already in `t.elapsed().as_secs_f64() * 1000.0` — capture it to a local `classify_ms` before building the `TraceEvent` to avoid computing it twice, or compute it once and store it.

3. In the **Score** stage `if collector.is_enabled()` block:
   ```rust
   stage_snapshots.push(StageTraceSnapshot {
       stage: PipelineStage::Score,
       item_count_in: scoreable.len(),
       item_count_out: scored.len(),
       duration_ms: t.elapsed().as_secs_f64() * 1000.0, // captured from Score t
       excluded: vec![],
   });
   ```
   Again capture the duration to a local or compute it once for both `TraceEvent` and `StageTraceSnapshot`.

4. In the **Deduplicate** stage `if collector.is_enabled()` block, build the snapshot's `excluded` from `ded_excluded` items:
   ```rust
   let ded_snapshot_excluded: Vec<ExcludedItem> = ded_excluded.iter().map(|si| ExcludedItem {
       item: si.item.clone(),
       score: si.score,
       reason: ExclusionReason::Deduplicated {
           deduplicated_against: si.item.content().to_owned(),
       },
   }).collect();
   stage_snapshots.push(StageTraceSnapshot {
       stage: PipelineStage::Deduplicate,
       item_count_in: scored.len(),   // before dedup — scored is still in scope (consumed into deduped but len matches)
       item_count_out: deduped.len(),
       duration_ms: /* dedup duration */,
       excluded: ded_snapshot_excluded,
   });
   ```
   Note: `scored` is consumed by `deduplicate(scored, ...)`. Capture `let scored_len = scored.len()` **before** the `deduplicate` call so it's available for `item_count_in` in the snapshot.

5. In the **Slice** stage `if collector.is_enabled()` block, collect snapshot excluded items during the existing per-item loop. Add a parallel local `Vec<ExcludedItem>` that captures each item excluded by the slice stage:
   ```rust
   let mut slice_snapshot_excluded: Vec<ExcludedItem> = Vec::new();
   // existing loop over &sorted...
   for si in &sorted {
       // ... existing content + sliced_count logic ...
       // after determining `reason`:
       let exc = ExcludedItem { item: si.item.clone(), score: si.score, reason: reason.clone() };
       collector.record_excluded(si.item.clone(), si.score, reason);
       slice_snapshot_excluded.push(exc);
   }
   stage_snapshots.push(StageTraceSnapshot {
       stage: PipelineStage::Slice,
       item_count_in: sorted.len(),
       item_count_out: sliced.len(),
       duration_ms: /* slice duration */,
       excluded: slice_snapshot_excluded,
   });
   ```
   To avoid cloning twice (once for `collector.record_excluded`, once for snapshot), construct the `ExcludedItem` first, then clone into `record_excluded` or vice versa. Either is correct — the research doc notes this double-clone is the accepted cost (D165).

6. In the **Place** stage `if collector.is_enabled()` block, collect place-stage excluded items from `truncated` and push the snapshot:
   ```rust
   let mut place_snapshot_excluded: Vec<ExcludedItem> = Vec::new();
   for (item, score) in &truncated {
       let available_tokens = budget.target_tokens() - result.iter().map(|i| i.tokens()).sum::<i64>();
       let exc = ExcludedItem {
           item: item.clone(),
           score: *score,
           reason: ExclusionReason::BudgetExceeded {
               item_tokens: item.tokens(),
               available_tokens,
           },
       };
       collector.record_excluded(item.clone(), *score, exc.reason.clone());
       place_snapshot_excluded.push(exc);
   }
   stage_snapshots.push(StageTraceSnapshot {
       stage: PipelineStage::Place,
       item_count_in: pinned.len() + sliced.len(),
       item_count_out: result.len(),
       duration_ms: /* place duration */,
       excluded: place_snapshot_excluded,
   });
   // ... existing record_included loop ...
   ```

7. After the Place block (after all existing `record_included` calls), add the `on_pipeline_completed` call when snapshots were collected:
   ```rust
   if !stage_snapshots.is_empty() {
       // Build synthetic SelectionReport for on_pipeline_completed.
       // included: items in result with scores from score_lookup
       let synthetic_included: Vec<crate::diagnostics::IncludedItem> = result.iter().map(|item| {
           let (score, reason) = if item.pinned() {
               (1.0, crate::diagnostics::InclusionReason::Pinned)
           } else if item.tokens() == 0 {
               (score_lookup.get(item.content()).copied().unwrap_or(0.0), crate::diagnostics::InclusionReason::ZeroToken)
           } else {
               (score_lookup.get(item.content()).copied().unwrap_or(0.0), crate::diagnostics::InclusionReason::Scored)
           };
           crate::diagnostics::IncludedItem { item: item.clone(), score, reason }
       }).collect();
       // excluded: union of all stage snapshot excluded items
       let synthetic_excluded: Vec<crate::diagnostics::ExcludedItem> =
           stage_snapshots.iter().flat_map(|s| s.excluded.iter().cloned()).collect();
       let total_candidates = synthetic_included.len() + synthetic_excluded.len();
       let synthetic_report = SelectionReport {
           events: vec![],
           included: synthetic_included,
           excluded: synthetic_excluded,
           total_candidates,
           total_tokens_considered,
           count_requirement_shortfalls: vec![],
       };
       collector.on_pipeline_completed(&synthetic_report, budget, &stage_snapshots);
   }
   ```

   **Note:** `ExclusionReason` already derives `Clone` (confirmed: line 117 in `diagnostics/mod.rs`). `ExcludedItem` also already derives `Clone`. No additional derives needed.

## Must-Haves

- [ ] `ExclusionReason` derives `Clone` — already confirmed present; no change needed
- [ ] `StageTraceSnapshot` is pushed in all 5 stage blocks: Classify, Score, Deduplicate, Slice, Place — in order
- [ ] Snapshot `item_count_in` and `item_count_out` match the values described in the research doc's "Stage Data Available" table
- [ ] Snapshot `excluded` for each stage contains only the items excluded by that stage
- [ ] `collector.on_pipeline_completed(&synthetic_report, budget, &stage_snapshots)` is called once at the end, gated on `!stage_snapshots.is_empty()` (equivalently `collector.is_enabled()`)
- [ ] `NullTraceCollector` path is unaffected: `is_enabled()` returns false, snapshots vec is never populated, `on_pipeline_completed` is never called
- [ ] `cargo test --all-targets` passes 169+ tests (167 prior + 2 new integration tests)
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0

## Verification

- `cd crates/cupel && cargo test --all-targets` — must show 169+ passed, 0 failed
- `cd crates/cupel && cargo test on_pipeline_completed` — both new tests pass
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` — exits 0
- `cd crates/cupel && cargo test` (unit tests only) — 167+ pass, 0 failed (regression check)
- Manual check: grep for `stage_snapshots.push` in `pipeline/mod.rs` — must appear exactly 5 times (one per stage)

## Observability Impact

- Signals added/changed: `on_pipeline_completed` is now called at the end of every `run_traced` invocation with an enabled collector; future agents can inspect this signal via the integration test or by adding their own `SpyCollector`
- How a future agent inspects this: `cargo test on_pipeline_completed` directly; or grep `pipeline/mod.rs` for `on_pipeline_completed` to confirm the call is present
- Failure state exposed: if the call is missing, the integration test fails with `assertion failed: spy.called == 1`; if snapshot count is wrong, assertion message names expected 5 vs actual N

## Inputs

- `crates/cupel/src/pipeline/mod.rs` — full `run_with_components` function; all stage blocks already have `if collector.is_enabled()` guards
- `crates/cupel/src/diagnostics/mod.rs` — `StageTraceSnapshot`, `ExcludedItem`, `IncludedItem`, `SelectionReport`, `ExclusionReason`, `InclusionReason`, `PipelineStage` (defined in T01)
- `crates/cupel/tests/on_pipeline_completed.rs` — failing integration test from T01 that this task makes pass
- S01-RESEARCH.md — "Stage Data Available" table for correct `item_count_in`/`item_count_out` mapping; "run_with_components Wiring Strategy" section

## Expected Output

- `crates/cupel/src/pipeline/mod.rs` — modified with 5 snapshot push calls + `on_pipeline_completed` call at end of `run_with_components`
- All 167+ prior tests pass; 2 new integration tests pass; clippy clean
