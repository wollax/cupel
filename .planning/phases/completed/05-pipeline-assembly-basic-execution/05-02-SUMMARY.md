# 05-02 Summary: CupelPipeline, PipelineBuilder, and end-to-end pipeline execution

## Result: PASS

**Duration:** ~7 minutes
**Commits:** 4

| Hash | Message |
|------|---------|
| 3e1ced3 | test(05-02): add PipelineBuilder tests (RED) |
| 1cf0726 | feat(05-02): implement PipelineBuilder and CupelPipeline |
| a08f318 | test(05-02): add CupelPipeline execution tests (RED->GREEN) |
| 2d1e5c3 | feat(05-02): add stage-level tracing to CupelPipeline |

## What was built

### PipelineBuilder (sealed class)
- Fluent API: `WithBudget`, `WithScorer`, `AddScorer`, `WithSlicer`, `WithPlacer`, `WithDeduplication`
- `Build()` validates: budget required, scorer required, no mixing `WithScorer`/`AddScorer`
- `AddScorer` path creates `CompositeScorer` at build time
- Defaults: `GreedySlice` slicer, `ChronologicalPlacer` placer, dedup enabled

### CupelPipeline (sealed class)
- `CreateBuilder()` factory method
- `Execute(items, traceCollector?)` runs fixed pipeline: Classify -> Score -> Deduplicate -> Sort -> Slice -> Place
- `ExecuteAsync(source, traceCollector?, cancellationToken)` materializes `IContextSource` then delegates to `Execute`
- Pinned items bypass Score/Dedup/Slice, enter Placer with score 1.0
- Pinned tokens consume budget; slicer gets reduced TargetTokens
- Pinned overflow throws `InvalidOperationException`
- Dedup on Content with `StringComparer.Ordinal`, keeps highest-scored duplicate
- Items with Tokens < 0 skipped; Tokens == 0 included
- Stage-level tracing with `Stopwatch` only when `IsEnabled`

## Test coverage

- **PipelineBuilderTests**: 11 tests (builder validation, fluent API, scoring paths, overrides)
- **CupelPipelineTests**: 18 tests (stage ordering, pinned bypass, dedup, budget consumption, tracing, async)
- **Total suite**: 287 tests, 0 failures

## Files modified

| File | Change |
|------|--------|
| `src/Wollax.Cupel/PipelineBuilder.cs` | New — sealed builder with fluent API |
| `src/Wollax.Cupel/CupelPipeline.cs` | New — sealed pipeline with Execute/ExecuteAsync |
| `src/Wollax.Cupel/PublicAPI.Unshipped.txt` | Updated — 11 new entries |
| `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs` | New — 11 tests |
| `tests/Wollax.Cupel.Tests/Pipeline/CupelPipelineTests.cs` | New — 18 tests |

## Decisions made

- Tracing is per-execution (not on builder) — `ITraceCollector?` parameter on `Execute`/`ExecuteAsync`
- Budget adjustment for slicer: `effectiveMax = MaxTokens - OutputReserve - pinnedTokens`, `outputReserve=0` on adjusted budget
- `SelectionReport` populated only when `traceCollector is DiagnosticTraceCollector`
- Dedup dictionary iterated via `foreach` for simplicity (not perf-critical path)
