---
slice: S01
milestone: M001
assessed_at: 2026-03-17
verdict: roadmap_updated
---

# S01 Post-Slice Roadmap Assessment

## Verdict

Roadmap updated — minor boundary map corrections only. Slice order, risk levels, dependencies, and requirement coverage are unchanged.

## Risk Retirement

S01 retired no named key risks (it was a data-types slice). It did de-risk S02/S03/S04 by confirming that all 8 diagnostic type shapes are stable and that `ExclusionReason`'s serde complexity is correctly scoped to S04.

## Success Criterion Coverage

- `pipeline.run_traced(&mut collector)` returns SelectionReport → S02, S03 ✓
- `pipeline.dry_run(items)` works in Rust → S03 ✓
- All diagnostic types serialize/deserialize under `serde` → S04 ✓
- Diagnostics conformance vectors pass in CI → S03 ✓
- `cargo clippy --all-targets -- -D warnings` passes → S05, S07 ✓
- `KnapsackSlice` returns error when capacity×items > 50M in both languages → S06, S07 ✓
- High-signal backlog issues resolved → S06, S07 ✓

All criteria have at least one remaining owning slice. Coverage check passes.

## Boundary Map Corrections

Two inaccuracies in the S01 → S02 boundary map were corrected:

1. **`TraceEvent` is a struct, not an enum.** The boundary map listed `TraceEvent enum`. The actual type is `pub struct TraceEvent` with a `stage: PipelineStage` field. `PipelineStage` is the enum.

2. **`ExcludedItem.score` is `f64`, not `Option<f64>`.** The boundary map listed `score: Option<f64>`. S01 implemented `score: f64` (consistent with `IncludedItem.score: f64`) per the task plan. Both fields carry the item's scorer output at time of recording, and a sentinel value (0.0) is used when score is unavailable rather than `Option`.

Both corrections applied to the `### S01 → S02` section of `M001-ROADMAP.md`.

## New Assumptions Established

- **`PinnedOverride` is a Slice-stage event.** The greedy slicer's `compute_effective_budget` subtracts pinned tokens from `effective_target` before slicing, making Place/Truncate overflow unreachable when pinned items fit. S03 must detect `BudgetExceeded` caused by pinned budget consumption at the Slice stage and map it to `PinnedOverride`. The `diag-pinned-override.toml` vector carries a detailed S03 implementation note.

- **`SelectionReport` lacks serde derive until S04.** S02 and S03 must not attempt to serialize a `SelectionReport` under `--features serde` — it will not compile until S04 adds the custom `ExclusionReason` adjacent-tagged impl and the `SelectionReport` derive.

## Requirement Coverage

Sound. No changes to `REQUIREMENTS.md` needed.

- R001 (Rust diagnostics parity): S01 delivered all type definitions; S02, S03 remain the primary owners
- R006 (Diagnostics serde): S01 added stubs to 7 of 8 types; S04 remains the primary owner

## What the Next Slice Starts With

S02 (`TraceCollector` trait and implementations) can proceed immediately. The consumed types are stable:
- `TraceEvent` (struct with `stage: PipelineStage`, `duration_ms: f64`, `item_count: usize`, `message: Option<String>`)
- `ExclusionReason` (8-variant data-carrying enum, no serde derive until S04)
- `InclusionReason` (`Scored`, `Pinned`, `ZeroToken`)
- `SelectionReport`, `IncludedItem`, `ExcludedItem` (all defined, no serde on SelectionReport)
- `PipelineStage` (5-variant enum with serde stub)
