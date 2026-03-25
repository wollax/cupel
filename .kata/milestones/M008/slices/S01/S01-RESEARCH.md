# S01: Add on_pipeline_completed hook to core cupel TraceCollector — Research

**Researched:** 2026-03-24
**Domain:** Rust diagnostics / trait extension
**Confidence:** HIGH — all relevant files read, .NET reference implementation inspected, 167 existing tests confirmed passing

## Summary

S01 is a pure additive change to the `cupel` core crate: add a defaulted no-op `on_pipeline_completed` method to the `TraceCollector` trait, define a `StageTraceSnapshot` struct, and wire the call into `run_with_components`. No existing functionality changes. All 167 passing tests are unchanged because the new method defaults to a no-op.

The main Rust-specific challenge is that `on_pipeline_completed` (per D164) takes `&SelectionReport`, but `run_with_components` holds `&mut C: TraceCollector` — it cannot call `DiagnosticTraceCollector::into_report()` (consuming) from inside the pipeline. The resolution: **build the `SelectionReport` directly from data already in scope at the end of `run_with_components`** — `result`, `score_lookup`, stage-scoped excluded items from the snapshots, and the initial `items` slice. No modification to `DiagnosticTraceCollector` required; `into_report()` remains unchanged for callers who want a report.

The .NET `StageTraceSnapshot` has no `excluded` field — the OTel collector rescans `report.Excluded` via `GetExclusionsForStage()`. The Rust spec (D165) takes a different approach: each snapshot carries **only the excluded items attributable to its stage**. This is better for S02 — `CupelOtelTraceCollector::on_pipeline_completed` can iterate snapshots without a second pass through the report.

## Recommendation

Add `on_pipeline_completed` as a defaulted no-op to `TraceCollector`. Define `StageTraceSnapshot` in `crates/cupel/src/diagnostics/mod.rs` (same file as `ExcludedItem`, `PipelineStage`, etc.). Wire into `run_with_components` by collecting snapshots in parallel with the existing stage event recording. Build a synthetic `SelectionReport` at the end from snapshot data + `result` to satisfy the signature. Expose `StageTraceSnapshot` from `crates/cupel/src/lib.rs`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Stage timing | `let t = Instant::now()` already in every stage block | Already measured; just capture alongside existing event recording |
| Stage item counts | `item_count` in existing `TraceEvent` is the output count; input count is already in scope as a local | No new measurements needed |
| Excluded-item construction | `ExcludedItem { item, score, reason }` already constructed per stage in `run_with_components` | Clone while constructing, or build snapshots in parallel |
| `SelectionReport` construction | Struct fields are all `pub`; can be constructed directly | No builder needed for the synthetic report in `on_pipeline_completed` |

## Existing Code and Patterns

- **`crates/cupel/src/diagnostics/trace_collector.rs`** — `TraceCollector` trait lives here. Has 3 existing defaulted no-op methods (`record_included`, `record_excluded`, `set_candidates`). The new `on_pipeline_completed` follows the exact same pattern. `DiagnosticTraceCollector` has private fields `included: Vec<IncludedItem>`, `excluded: Vec<(ExcludedItem, usize)>`, `total_candidates`, `total_tokens_considered`.

- **`crates/cupel/src/diagnostics/mod.rs`** — `ExcludedItem`, `IncludedItem`, `PipelineStage`, `SelectionReport`, `TraceEvent`, `ContextBudget` (re-export). `StageTraceSnapshot` should be defined here alongside `ExcludedItem` and `SelectionReport`.

- **`crates/cupel/src/pipeline/mod.rs`** — `run_with_components` is the call site. The function signature is:
  ```rust
  fn run_with_components<C: TraceCollector>(
      &self, items, budget, scorer, slicer, placer, deduplication, overflow_strategy, collector: &mut C
  ) -> Result<Vec<ContextItem>, CupelError>
  ```
  Stage timing uses `let t = Instant::now()` before each stage, `t.elapsed().as_secs_f64() * 1000.0` after. All stage event recording is already behind `if collector.is_enabled()` guards. Stage snapshot collection follows the same guard pattern.

- **`src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs`** — .NET reference: `Stage`, `ItemCountIn`, `ItemCountOut`, `Duration: TimeSpan`. No `excluded` field — .NET rescans via `GetExclusionsForStage()`. Rust adds `excluded: Vec<ExcludedItem>` per D165.

- **`src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`** — .NET reference: `OnPipelineCompleted` is a defaulted no-op interface method. Signature confirmed: `void OnPipelineCompleted(SelectionReport report, ContextBudget budget, IReadOnlyList<StageTraceSnapshot> stageSnapshots)`.

- **`src/Wollax.Cupel/CupelPipeline.cs`** — .NET wiring: `stageSnapshots` is built alongside stage event recording (`if (sw is not null)`). `trace.OnPipelineCompleted(report, _budget, stageSnapshots!)` is called AFTER `reportBuilder.Build(events)` but BEFORE returning. Rust can follow the same pattern by building the SelectionReport at the end of `run_with_components` when snapshots are being tracked.

- **`crates/cupel-testing/Cargo.toml`** — Pattern to follow for `cupel-otel` crate structure in S02: `cupel = { version = "1.1", path = "../cupel" }`, includes only `src/**/*.rs`, `tests/**/*.rs`, `Cargo.toml`, `LICENSE`, `README.md`.

## Stage Data Available in `run_with_components`

All data needed for `StageTraceSnapshot` is naturally available:

| Stage | item_count_in | item_count_out | excluded items |
|-------|---------------|----------------|----------------|
| Classify | `items.len()` | `pinned.len() + scoreable.len()` | `neg_items` → `NegativeTokens` reason |
| Score | `scoreable.len()` | `scored.len()` | none (Score stage has no exclusions in current impl) |
| Deduplicate | `scored.len()` (before dedup fn) | `deduped.len()` | `ded_excluded` → `Deduplicated` reason |
| Slice | `sorted.len()` | `sliced.len()` | items in `sorted` not in `sliced` → `BudgetExceeded` / `CountCapExceeded` / `PinnedOverride` (existing logic in the `if collector.is_enabled()` block) |
| Place | `pinned.len() + sliced.len()` | `result.len()` | `truncated` → `BudgetExceeded` |

Stage duration uses `t.elapsed().as_secs_f64() * 1000.0` (same pattern as `TraceEvent`).

## The SelectionReport Challenge — Resolution

`on_pipeline_completed` needs `&SelectionReport`. In `run_with_components`, the collector is `&mut C` — cannot call `into_report()` (consuming). The resolution:

**Build a synthetic `SelectionReport` at the end of `run_with_components` from data already in scope when snapshots are tracked:**

```
included:                from `result` + `score_lookup` + pinned status
excluded:                union of all snapshot.excluded Vecs (or built directly)
total_candidates:        items.len() (the original input)
total_tokens_considered: items.iter().map(|i| i.tokens()).sum() (computed at top for set_candidates)
count_requirement_shortfalls: empty Vec (not populated here; only DiagnosticTraceCollector does this)
events:                  Vec::new() (not needed for OTel; OTel builds spans from snapshots)
```

This synthetic report is built ONLY when `collector.is_enabled()` and stage snapshots are being tracked. `DiagnosticTraceCollector` does NOT need to be modified — `into_report()` works as before. `NullTraceCollector::is_enabled()` returns false, so the snapshot-building path is dead code for Null.

**`total_tokens_considered` availability**: the value is already computed for `set_candidates` at the top of `run_with_components`:
```rust
collector.set_candidates(items.len(), items.iter().map(|i| i.tokens()).sum());
```
We need to cache this in a local when enabled, parallel to stage snapshot construction.

## `StageTraceSnapshot` Struct Definition

Defined in `crates/cupel/src/diagnostics/mod.rs`:

```rust
#[non_exhaustive]
#[derive(Debug, Clone)]
pub struct StageTraceSnapshot {
    pub stage: PipelineStage,
    pub item_count_in: usize,
    pub item_count_out: usize,
    pub duration_ms: f64,
    /// Excluded items attributable to this stage only.
    /// Empty for Score (no exclusions currently), and non-empty for
    /// Classify (NegativeTokens), Deduplicate (Deduplicated),
    /// Slice (BudgetExceeded/CountCapExceeded/PinnedOverride), Place (BudgetExceeded).
    pub excluded: Vec<ExcludedItem>,
}
```

`#[non_exhaustive]` required (per crate convention for all library-owned structs). `Clone` is needed because `ExcludedItem` derives `Clone`. No `PartialEq` for now (has no f64 comparability issue but adds complexity; S02 doesn't need it). No serde in S01 (OTel bridge doesn't require it; add only if needed).

## `on_pipeline_completed` Trait Method Signature

```rust
/// Called once at the end of a pipeline run with structured completion data.
///
/// **No-op default.** OTel-bridge implementations override this to emit
/// `cupel.pipeline` and `cupel.stage.*` spans from the structured snapshot data.
/// [`NullTraceCollector`] and [`DiagnosticTraceCollector`] rely on this no-op.
fn on_pipeline_completed(
    &mut self,
    _report: &SelectionReport,
    _budget: &ContextBudget,
    _stage_snapshots: &[StageTraceSnapshot],
) {}
```

`&mut self` (not `&self`) to be consistent with all other `TraceCollector` methods and to allow OTel implementations to clear internal state if needed. `&[StageTraceSnapshot]` (slice, not `Vec`) for the same reason as other array params.

## `run_with_components` Wiring Strategy

Stage snapshot building runs **inside the existing `if collector.is_enabled()` blocks**, parallel to `record_stage_event`. No separate guard needed. Snapshots are accumulated in a `Option<Vec<StageTraceSnapshot>>` (or `Vec<StageTraceSnapshot>` that starts empty and is populated only when enabled).

At the very end of `run_with_components`, after the Place stage:
```
if let Some(snapshots) = &stage_snapshots {
    // Build synthetic SelectionReport for on_pipeline_completed
    let report = build_completion_report(&result, &score_lookup, &pinned, &snapshots, total_tokens_considered);
    collector.on_pipeline_completed(&report, budget, snapshots);
}
```

`build_completion_report` is a private helper (free function or closure) that avoids code in the hot path.

## `lib.rs` Exports

`StageTraceSnapshot` must be re-exported from `crates/cupel/src/lib.rs` so S02 (`cupel-otel`) can reference it in its `TraceCollector` implementation:
```rust
pub use diagnostics::{
    ...,
    StageTraceSnapshot,  // new
};
```

## Constraints

- `crates/cupel` must remain zero-dependency in production (`[dependencies]`: only `chrono`, `serde` optional, `thiserror`) — `StageTraceSnapshot` uses only internal types, no new deps
- `on_pipeline_completed` MUST be a defaulted no-op — `NullTraceCollector` and `DiagnosticTraceCollector` must not need to override it
- Core crate semver stays `1.1.x` — adding a defaulted method is non-breaking
- `#[non_exhaustive]` on `StageTraceSnapshot` — mandatory per crate convention
- The stage snapshot building path is gated on `collector.is_enabled()` so `NullTraceCollector` users pay zero overhead (monomorphization eliminates the block)

## Common Pitfalls

- **Forgetting `items.len()` for Classify `item_count_in`**: The classify stage block starts after `set_candidates(items.len(), ...)` — capture `items.len()` as a local if building snapshots in the block.
- **`scored.len()` vs `scoreable.len()` for Score input**: `score_items(&scoreable, scorer)` returns `scored`; `scored.len() == scoreable.len()` always (no exclusions at Score stage). Use `scoreable.len()` for `item_count_in`.
- **Slice stage excluded items already exist in the collector**: When building the slice snapshot's `excluded` field, extract them at the point they're already being constructed in the existing `for si in &sorted { ... collector.record_excluded(...) }` block — clone each `ExcludedItem` into the snapshot as well, or build the snapshot's `excluded` Vec in the same pass.
- **`total_tokens_considered` scope**: The value is computed inline in the `set_candidates` call. Extract it to a local variable when snapshots are being tracked so it's available at the bottom of `run_with_components`.
- **`count_requirement_shortfalls` in synthetic report**: Must be `Vec::new()` — shortfalls are populated by `CountQuotaSlice` via `DiagnosticTraceCollector`. The OTel collector doesn't need shortfalls for its attribute set.
- **`events` field in synthetic report**: Must be `Vec::new()` — OTel collector builds spans from `stage_snapshots`, not from `events`. Passing an empty events list is correct for `on_pipeline_completed` usage.

## Open Risks

- **Double clone of excluded items**: Each excluded item is cloned twice — once into `collector.record_excluded(item.clone(), ...)` and once into the stage snapshot `excluded` field. For the common case (NullTraceCollector), both paths are eliminated by monomorphization. For DiagnosticTraceCollector, the snapshot is never built (no-op `on_pipeline_completed`). For the OTel collector (S02), the double clone is the price for keeping the design clean. Acceptable.
- **`Place` stage `item_count_in` calculation**: The .NET equivalent uses `merged.Length` (= `pinned.Count + slicedScored.Count`). In Rust, this is `pinned.len() + sliced.len()`. Confirm this is computed before `place_items` is called.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | none needed | n/a — pure Rust trait extension; no external libraries |

## Sources

- `crates/cupel/src/diagnostics/trace_collector.rs` — complete TraceCollector trait and both implementations (NullTraceCollector, DiagnosticTraceCollector); defaulted no-op method pattern
- `crates/cupel/src/diagnostics/mod.rs` — ExcludedItem, SelectionReport, PipelineStage, TraceEvent definitions; location for new StageTraceSnapshot
- `crates/cupel/src/pipeline/mod.rs` — complete `run_with_components` implementation; all stage timing and excluded-item recording patterns
- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs` — .NET reference struct (Stage, ItemCountIn, ItemCountOut, Duration)
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — .NET `OnPipelineCompleted` defaulted no-op interface method; exact signature confirmed
- `src/Wollax.Cupel/CupelPipeline.cs` — .NET wiring: stageSnapshots built alongside TraceEvent recording; `OnPipelineCompleted` called after `reportBuilder.Build()`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — OTel reference impl: all spans built in `OnPipelineCompleted`; `GetExclusionsForStage` rescans report.Excluded (Rust avoids this via snapshot.excluded)
- `crates/cupel-testing/Cargo.toml` — companion crate Cargo.toml pattern (used in S02 for cupel-otel)
- `cargo test --all-targets` — 167 tests passing, confirmed pre-S01 baseline
