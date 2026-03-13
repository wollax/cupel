---
phase: "06"
plan: "06-04"
title: "Builder integration and pipeline dispatch for new slicers"
status: complete
started: "2026-03-13T21:30:17Z"
completed: "2026-03-13T21:36:19Z"
duration: "6m 2s"
tests_before: 361
tests_after: 371
---

# 06-04 Summary: Builder integration and pipeline dispatch for new slicers

Extended PipelineBuilder with convenience methods for new slicers and added IAsyncSlicer dispatch to CupelPipeline, completing the builder-level integration for Phase 6.

## Commits

| Hash | Message |
|------|---------|
| `6b48832` | test(06-04): add failing tests for PipelineBuilder convenience methods |
| `f9ccb44` | feat(06-04): add UseGreedySlice, UseKnapsackSlice, WithQuotas, WithAsyncSlicer to PipelineBuilder |
| `4f26dc8` | test(06-04): add failing tests for CupelPipeline streaming and pinned+quota detection |
| `aa33503` | feat(06-04): add ExecuteStreamAsync, pinned+quota detection, and ScoreStreamAsync |

## What was built

### PipelineBuilder extensions
- `UseGreedySlice()` — sets GreedySlice as the slicer
- `UseKnapsackSlice(int bucketSize = 100)` — sets KnapsackSlice as the slicer
- `WithQuotas(Action<QuotaBuilder>)` — wraps the current slicer (or default GreedySlice) in a QuotaSlice decorator; validates at config time via QuotaBuilder.Build()
- `WithAsyncSlicer(IAsyncSlicer)` — stores an async slicer for streaming execution

### CupelPipeline extensions
- `ExecuteStreamAsync(IAsyncEnumerable<ContextItem>, ...)` — dispatches to IAsyncSlicer for streaming sources
- `ScoreStreamAsync` (private) — buffers items into micro-batches before scoring, giving relative scorers meaningful allItems context
- Pinned+quota conflict detection: emits trace WARNING (not exception) when pinned items of a Kind exceed its Cap percentage

### TraceEvent extension
- Added optional `Message` property for diagnostic warnings (e.g., pinned items exceeding quota cap)

## Decisions

- **TraceEvent.Message added**: The existing TraceEvent had no field for warning text. Added optional `string? Message` property to carry diagnostic messages. This is a minor schema extension to an existing type, not an architectural change.
- **Cancellation check at entry point**: `ExecuteStreamAsync` checks `cancellationToken.ThrowIfCancellationRequested()` before async work begins, ensuring pre-cancelled tokens throw immediately.

## Deviations

- **TraceEvent schema extension**: Plan specified "emit trace WARNING" but TraceEvent had no message field. Added `Message` property — minimal deviation, no architectural impact.
- **KnapsackSlice test items**: Used `Tokens: 100` and `FutureRelevanceHint: 0.8` instead of default `CreateItem` helper values, because KnapsackSlice DP does not select zero-value items (score 0 from null FutureRelevanceHint yields DP value 0).

## Test coverage

- 10 new PipelineBuilder tests: UseGreedySlice, UseKnapsackSlice (default + custom), WithQuotas (3 composition patterns + 2 validation), WithAsyncSlicer (set + null)
- 7 new CupelPipeline tests: ExecuteStreamAsync (basic, budget-full, no-slicer, null-source, cancellation), pinned+quota warning, WithQuotas ordering
- All 371 tests pass, zero warnings
