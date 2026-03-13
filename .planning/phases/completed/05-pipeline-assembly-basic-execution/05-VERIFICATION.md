# Phase 05 Verification: Pipeline Assembly & Basic Execution

**Status: passed**
**Score: 28/29 must-haves verified**
**Test suite: 287/287 passed**
**Build: zero warnings**

---

## Test & Build Results

```
Tests:  287 passed, 0 failed, 0 skipped (268ms)
Build:  Succeeded — zero warnings
```

---

## Must-Have Checklist

### Plan 05-01: Slicer & Placer Implementations

| # | Must-Have | Result | Evidence |
|---|-----------|--------|----------|
| 1 | GreedySlice fills budget by score/token ratio (value density) in O(N log N) | PASS | `GreedySlice.cs` L29-44: builds `(Density, Index)` array, calls `Array.Sort`. Doc comment states "O(N log N) due to sorting by density". |
| 2 | GreedySlice uses stable sort via `(double Density, int Index)` tuple pattern | PASS | `GreedySlice.cs` L29, L40-44: sort comparator falls back to `a.Index.CompareTo(b.Index)` for ties. |
| 3 | Zero-token items have infinite density and are always included | PASS | `GreedySlice.cs` L33-35: `tokens == 0` assigns `double.MaxValue` density. L56-58: zero-token items added unconditionally, not consuming budget. |
| 4 | GreedySlice fills to `TargetTokens` (soft goal), not `MaxTokens` | PASS | `GreedySlice.cs` L47: `remainingTokens = budget.TargetTokens`. `MaxTokens` is never read. |
| 5 | UShapedPlacer places highest-scored items at edges (start and end) | PASS | `UShapedPlacer.cs` L32-65: sorts descending by score, even indices go left (`result[left]`), odd go right (`result[right]`). |
| 6 | ChronologicalPlacer orders by timestamp ascending, null timestamps sort to end | PASS | `ChronologicalPlacer.cs` L38-61: null-aware comparator: has-ts before no-ts, ascending within has-ts, stable by index. |
| 7 | All three implementations follow zero-allocation discipline in hot paths | PASS | All three allocate only fixed-size value-type arrays (`(T, int)[]`) for sort keys, then a result array — no closures or LINQ in hot loops. |

### Plan 05-02: Pipeline Builder & Execution

| # | Must-Have | Result | Evidence |
|---|-----------|--------|----------|
| 8 | `CupelPipeline.CreateBuilder()` returns a `PipelineBuilder` with fluent API | PASS | `CupelPipeline.cs` L39: `public static PipelineBuilder CreateBuilder() => new();`. `PipelineBuilder.cs` L23-92: all `WithX`/`AddScorer` return `this`. Test `CreateBuilder_ReturnsPipelineBuilder` asserts type. |
| 9 | Builder validates at `Build()`: budget required | PASS | `PipelineBuilder.cs` L102-103: throws `InvalidOperationException("Budget is required.")`. `Build_MissingBudget_Throws` passes. |
| 10 | Builder validates at `Build()`: scorer required | PASS | `PipelineBuilder.cs` L108-109: throws `InvalidOperationException("A scorer is required.")`. `Build_MissingScorer_Throws` passes. |
| 11 | Builder validates at `Build()`: `WithScorer`/`AddScorer` not mixed | PASS | `PipelineBuilder.cs` L111-112: throws when both paths used. `Build_MixedScoringPaths_Throws` passes. |
| 12 | Pipeline executes fixed order: Classify → Score → Deduplicate → Sort → Slice → Place | PASS | `CupelPipeline.cs` L54-249: sequential labeled comment blocks in that exact order. `StageOrder_ClassifyScoreDedupSlicePlace` test confirms ordering. |
| 13 | Pinned items bypass Score/Dedup/Slice and enter at Placer with effective score 1.0 | PASS | `CupelPipeline.cs`: pinned split at L66-73 (Classify), not in `scored[]` (Score L100-104), not in `bestByContent` (Dedup L121-143), merged post-Slice at L219-227 with `Score = 1.0`. Tests `PinnedItems_BypassScoring` and `PinnedItems_EffectiveScore1_0` pass. |
| 14 | Dedup uses `StringComparer.Ordinal` on Content, keeps highest-scored duplicate | PASS | `CupelPipeline.cs` L121: `new Dictionary<string, int>(StringComparer.Ordinal)`. L127: replaces entry only when `scored[i].Score > scored[existingIndex].Score`. `DuplicateContent_KeepsHighestScored` passes. |
| 15 | Pinned tokens consume budget; slicer gets reduced `TargetTokens` | PASS | `CupelPipeline.cs` L181-186: `effectiveTarget = Math.Max(0, _budget.TargetTokens - pinnedTokens)`. `PinnedItems_ConsumeBudget` passes. |
| 16 | Pinned overflow throws `InvalidOperationException` | PASS | `CupelPipeline.cs` L92-97: throws with descriptive message. `PinnedOverflow_ThrowsInvalidOperationException` passes. |
| 17 | Items with `Tokens < 0` are skipped; `Tokens == 0` items are valid | PASS | `CupelPipeline.cs` L61-63: `continue` on `item.Tokens < 0`. Zero-token items proceed normally. Tests `NegativeTokens_ItemSkipped` and `ZeroTokens_ItemIncluded` pass. |
| 18 | Tracing is per-execution via optional `ITraceCollector` parameter | PASS | `CupelPipeline.Execute` L49: `ITraceCollector? traceCollector = null`. `NullTraceCollector.Instance` is the default. Report is non-null only when `DiagnosticTraceCollector` is passed. |
| 19 | `AddScorer` path internally creates `CompositeScorer` at `Build()` time | PASS | `PipelineBuilder.cs` L114-116: `new CompositeScorer(_scorerEntries!)` created in `Build()`. `AddScorer_CreatesComposite` passes. |
| 20 | Default slicer is `GreedySlice` | PASS | `PipelineBuilder.cs` L120: `_slicer ?? new GreedySlice()`. `Build_DefaultSlicer_IsGreedySlice` passes. |
| 21 | Default placer is `ChronologicalPlacer` | PASS | `PipelineBuilder.cs` L121: `_placer ?? new ChronologicalPlacer()`. `Build_DefaultPlacer_IsChronologicalPlacer` passes. |
| 22 | `ExecuteAsync(IContextSource)` materializes items then delegates to sync `Execute` | PASS | `CupelPipeline.cs` L263-267: `await source.GetItemsAsync()` then `Execute(items, traceCollector)`. `ExecuteAsync_MaterializesSource` passes. |

### Plan 05-03: Benchmarks

| # | Must-Have | Result | Evidence |
|---|-----------|--------|----------|
| 23 | Full pipeline benchmark with 100/250/500 items | PASS | `PipelineBenchmark.cs` L12: `[Params(100, 250, 500)]`. `FullPipeline()` benchmark exists. |
| 24 | Benchmark uses `[Params(100, 250, 500)]` | PASS | `PipelineBenchmark.cs` L12: exact annotation present. |
| 25 | Items have realistic variety (different kinds, priorities, timestamps, pinned) | PASS | `PipelineBenchmark.cs` L30-42: items vary by Kind (ToolOutput/Message), Priority (every 7th), Timestamp (ascending), one pinned item. |
| 26 | Tracing-disabled path: `[MemoryDiagnoser]` present on benchmark | PASS | `PipelineBenchmark.cs` L6: `[MemoryDiagnoser]` on class. `TraceGatingBenchmark.cs` L9: same. |
| 27 | `TraceGatingBenchmark` verifies zero-allocation path via `NullTraceCollector.IsEnabled` gate | PASS | `TraceGatingBenchmark.cs` L29-61: baseline uses `NullTraceCollector.Instance`, gates every `RecordItemEvent` call behind `if (trace.IsEnabled)`. NullTraceCollector.IsEnabled returns false, so no allocations occur in that path. |

### ROADMAP.md Success Criteria

| # | Criterion | Result | Evidence |
|---|-----------|--------|----------|
| 28 | `CupelPipeline.CreateBuilder()` produces builder that validates at `Build()` and returns working pipeline | PASS | See #8-11 above. |
| 29 | Pipeline stage order fixed, no reordering possible | PARTIAL PASS* | Stages are hardcoded in source. `PipelineStage` enum in PublicAPI lists 5 values but the enum does not include "Sort" as a distinct stage value — Sort happens internally between Dedup and Slice without a trace event. The plan says "Classify → Score → Deduplicate → Sort → Slice → Place" but the `PipelineStage` enum only has Classify=0, Score=1, Deduplicate=2, Slice=3, Place=4 (Sort is internal). This is architecturally intentional and the code is correct — noted as minor discrepancy with plan wording only. |

*The Sort step is implemented internally in `CupelPipeline.Execute` (L162-178) and is correct, but it does not emit a trace event and is not a public `PipelineStage` enum value. The plan document's stage list includes "Sort" but the implementation treats it as an internal detail between Dedup and Slice. This is not a gap — it is a deliberate design choice consistent with the tracing system.

---

## Public API Coverage

All Phase 5 new public types are present in `PublicAPI.Unshipped.txt`:

- `GreedySlice` — lines 148-150
- `UShapedPlacer` — lines 151-153
- `ChronologicalPlacer` — lines 154-156
- `CupelPipeline` — lines 157-160 (including `CreateBuilder`, `Execute`, `ExecuteAsync`)
- `PipelineBuilder` — lines 161-169 (all fluent methods)

---

## Summary

Phase 5 is complete. All 287 tests pass, the build is clean, all must-haves from the three plans are verified against the actual source, and PublicAPI.Unshipped.txt covers every new public type.

The only note is cosmetic: the plan documents list "Sort" as a named pipeline stage, but the implementation correctly treats it as an internal step between Dedup and Slice without a public `PipelineStage` enum value or trace event. This is consistent with the tracing design and does not represent a gap.

**Verdict: PASSED**
