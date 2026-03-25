---
id: T02
parent: S01
milestone: M008
provides:
  - "Snapshot collection wired into all 5 stage blocks in `run_with_components`: Classify, Score, Deduplicate, Slice, Place"
  - "`collector.on_pipeline_completed(&synthetic_report, budget, &stage_snapshots)` called at end of `run_with_components` when `!stage_snapshots.is_empty()`"
  - "Synthetic `SelectionReport` built from snapshot data: `included` from `result` + `score_lookup`, `excluded` union of all stage excluded items"
  - "`total_tokens_considered` extracted before `set_candidates` call so it is available for the synthetic report"
  - "`NullTraceCollector` path unaffected: `is_enabled()` false → snapshots vec never populated → `on_pipeline_completed` never called"
key_files:
  - crates/cupel/src/pipeline/mod.rs
key_decisions:
  - "Dedup stage excluded items for snapshot built as `Vec<ExcludedItem>` first, then reused for both `record_excluded` loop and snapshot — avoids double-clone by cloning item/score/reason once into `ExcludedItem`, then cloning from there for the collector call (D165 accepted cost)"
  - "Slice stage excluded items similarly built first as `ExcludedItem`, fed to both `record_excluded` and snapshot vec"
  - "Place stage excluded items built from `truncated` with per-item `available_tokens` computed as in the original code; same pattern used for both collector call and snapshot"
  - "`scored_len` captured before `deduplicate(scored, ...)` consumes `scored` so it is available as `item_count_in` for the Dedup snapshot"
  - "Duration captured to local (`classify_ms`, `score_ms`, `ded_ms`, `slice_ms`, `place_ms`) per stage so the same value feeds both `TraceEvent` and `StageTraceSnapshot` without double-calling `t.elapsed()`"
patterns_established:
  - "All stage snapshot pushes are inside their respective `if collector.is_enabled()` blocks — consistent with existing TraceEvent emission pattern"
  - "`on_pipeline_completed` call gated on `!stage_snapshots.is_empty()` which is equivalent to `collector.is_enabled()` having been true"
observability_surfaces:
  - "`cargo test on_pipeline_completed` — both integration tests pass; any regression here immediately names the broken assertion"
  - "`grep 'stage_snapshots.push' pipeline/mod.rs` — must show 5 matches (one per stage)"
duration: 25m
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Wire snapshot collection into run_with_components and call on_pipeline_completed

**Snapshot collection wired across all 5 pipeline stages; `on_pipeline_completed` called once at end with synthetic `SelectionReport` and 5 `StageTraceSnapshot`s.**

## What Happened

Modified `crates/cupel/src/pipeline/mod.rs` — the only file changed. No changes to any other file.

**Imports updated:** Added `ExcludedItem`, `IncludedItem`, `StageTraceSnapshot` to the existing `use crate::diagnostics::{...}` block.

**Initialization:** Before the Classify stage, extracted `total_tokens_considered` from `items` (guarded on `collector.is_enabled()`) and initialized `stage_snapshots: Vec<StageTraceSnapshot>` (capacity 5 when enabled, empty when not).

**Per-stage snapshot pushes (5 total):**

1. **Classify** — `item_count_in: items.len()`, `item_count_out: pinned.len() + scoreable.len()`, excluded from `neg_items`. Duration captured to `classify_ms` local to avoid calling `elapsed()` twice.

2. **Score** — `item_count_in: scoreable.len()`, `item_count_out: scored.len()`, `excluded: vec![]`. Duration captured to `score_ms`.

3. **Deduplicate** — `scored_len` captured before `deduplicate()` consumes `scored`. Excluded items built as `Vec<ExcludedItem>` first, then used for both `record_excluded` loop and snapshot `excluded` field. Duration captured to `ded_ms`.

4. **Slice** — `item_count_in: sorted.len()`, `item_count_out: sliced.len()`. Excluded items collected during the existing sorted-item loop into `slice_snapshot_excluded`, then fed to both `record_excluded` and snapshot. Duration captured to `slice_ms`.

5. **Place** — `item_count_in: pinned.len() + sliced.len()`, `item_count_out: result.len()`. Excluded items built from `truncated` into `place_snapshot_excluded`. Duration captured to `place_ms`.

**`on_pipeline_completed` call:** After the `record_included` loop, inside the `if collector.is_enabled()` block, gated on `!stage_snapshots.is_empty()`. Built synthetic `SelectionReport` with `included` from `result` + `score_lookup`, `excluded` as union of all stage snapshot excluded items (via `flat_map`), and `total_tokens_considered` from the pre-loop local. Called `collector.on_pipeline_completed(&synthetic_report, budget, &stage_snapshots)`.

## Verification

- `cargo test --all-targets` → 169 passed (81 unit + 48 unit + integration tests), 0 failed ✓
- `cargo test on_pipeline_completed` → both new integration tests pass ✓  
  - `on_pipeline_completed_called_once_with_five_snapshots` — passes (was failing in T01) ✓
  - `on_pipeline_completed_not_called_for_null_collector` — passes ✓
- `cargo clippy --all-targets -- -D warnings` → exit 0, no warnings ✓
- `grep -c 'stage_snapshots.push' pipeline/mod.rs` → 5 ✓

## Deviations

None. Task plan followed exactly. The only implementation nuance was that `ExcludedItem` construction was unified for the dedup and place stages to avoid double-clone issues (build once, use for both collector and snapshot).

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — snapshot collection wired into all 5 stage blocks + `on_pipeline_completed` call added at end of `run_with_components`
