---
id: S06
parent: M001
milestone: M001
provides:
  - KnapsackSlice DP table guard — InvalidOperationException when candidateCount × (capacity+1) > 50M cells
  - QuotaBuilder epsilon fix — three equal-share 33.333...% quotas now accepted
  - OverflowStrategyValue → OverflowStrategy internal rename in CupelPipeline + consumer update
  - Caller-facing error messages (CupelPolicy Quotas+Stream, ScorerEntry InnerScorer)
  - ScorerType.Scaled = 6, SlicerType.Stream = 2, PipelineStage 0–4 explicit integer anchors
  - Comprehensive XML <summary> docs on all 11 ContextItem properties
  - ITraceCollector.IsEnabled constancy contract in doc
  - ISlicer.Slice sort precondition in method summary
  - ContextResult.Report behavioral nullability language (no concrete type reference)
  - SelectionReport class summary references ITraceCollector not DiagnosticTraceCollector
  - 6 new tests (KnapsackSlice guard boundary, QuotaSlice null-arg, PriorityScorer range, TagScorer case-insensitive + zero-weight, CupelPolicy stream-knapsack)
  - 1 duplicate DI test removed
  - NegativeTokenItems_SilentlyExcluded coverage test
requires: []
affects:
  - S07
key_files:
  - src/Wollax.Cupel/KnapsackSlice.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/Slicing/QuotaBuilder.cs
  - src/Wollax.Cupel/CupelPolicy.cs
  - src/Wollax.Cupel/ScorerEntry.cs
  - src/Wollax.Cupel/ScorerType.cs
  - src/Wollax.Cupel/SlicerType.cs
  - src/Wollax.Cupel/Diagnostics/PipelineStage.cs
  - src/Wollax.Cupel/ContextItem.cs
  - src/Wollax.Cupel/Diagnostics/ITraceCollector.cs
  - src/Wollax.Cupel/ISlicer.cs
  - src/Wollax.Cupel/ContextResult.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReport.cs
  - src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs
  - tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs
  - tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs
  - tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs
  - tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs
  - tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs
  - tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs
  - tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs
key_decisions:
  - OOM-bound guard pattern: compute cell count with long arithmetic, throw InvalidOperationException before allocating
  - Epsilon applied only to total-sum check (> 100.0 + 1e-9), not per-kind Require > Cap checks
  - Error messages name only public API surface — no internal types (ISlicer, QuotaSlice) in user-visible exceptions
  - Interface contract docs use interface types (ITraceCollector), not concrete implementations
patterns_established:
  - OOM-bound guard: (long)candidateCount * (capacity + 1) > limit with diagnostic message including all three fields
  - Exception tests use ThrowsExactly<T> (not Throws<T>)
  - Each test class uses its own CreateItem helper — no shared fixtures
observability_surfaces:
  - InvalidOperationException message includes candidates=N, capacity=C, cells=K when guard fires
  - CupelPolicy Quotas+Stream exception names public API constraint precisely (SlicerType.Stream)
  - ScorerEntry InnerScorer exception includes corrective action ("Remove it or change the type to Scaled")
drill_down_paths:
  - .kata/milestones/M001/slices/S06/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S06/tasks/T02-SUMMARY.md
  - .kata/milestones/M001/slices/S06/tasks/T03-SUMMARY.md
  - .kata/milestones/M001/slices/S06/tasks/T04-SUMMARY.md
duration: ~2h (4 tasks)
verification_result: passed
completed_at: 2026-03-21
---

# S06: .NET Quality Hardening

**20 triage items resolved across the .NET codebase: KnapsackSlice DP guard, epsilon fix for equal-share quotas, naming/error message/enum hardening, four interface contract doc improvements, and 6 new tests (net +5 after duplicate removal) — 658 tests pass with zero regressions.**

## What Happened

Four tasks executed cleanly in sequence with no regressions between any of them.

**T01 — KnapsackSlice DP table size guard:** Inserted the R002 guard immediately after `discretizedWeights` is built and before `ArrayPool.Rent` calls. Uses `(long)candidateCount * (capacity + 1) > 50_000_000L` — long cast on the first operand prevents int overflow; the `>` (not `>=`) condition lets exactly 50M cells pass. Exception message includes all three diagnostic fields (`candidates=N, capacity=C, cells=K`). Four tests added: at-limit-passes, one-above-throws, clearly-over-throws, and negative-token-items-silently-skipped.

**T02 — API surface hardening (7 items):** Renamed `CupelPipeline.OverflowStrategyValue` → `OverflowStrategy` (one internal consumer in `CupelServiceCollectionExtensions.cs`). Fixed `QuotaBuilder` total-sum check with `> 100.0 + 1e-9` to accept three equal-share 33.333...% quotas without floating-point rejection. Replaced internal-type-leaking error messages in `CupelPolicy` and `ScorerEntry` with caller-facing language. Anchored `ScorerType.Scaled = 6`, `SlicerType.Stream = 2`, and `PipelineStage` 0–4 — all verified against `PublicAPI.Shipped.txt` before writing. Added comprehensive XML `<summary>` docs to all 11 `ContextItem` properties.

**T03 — Interface contract documentation (4 items):** `ITraceCollector.IsEnabled` doc states callers may cache the value; implementations must not toggle mid-run. `ISlicer.Slice` method summary includes the sort precondition. `ContextResult.Report` doc uses behavioral language ("null when tracing is disabled") with no concrete type reference. `SelectionReport` class summary references `ITraceCollector` instead of `DiagnosticTraceCollector`.

**T04 — Test coverage gaps and hygiene (8 items):** Added `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` (CupelPolicy), `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull` (backed by `ArgumentNullException.ThrowIfNull` guards added to the constructor), `ScoresAreInZeroToOneRange` (PriorityScorer, mirrors RecencyScorer pattern), `TagScorer_CaseInsensitiveMatch` and `TagScorer_ZeroTotalWeight_ReturnsZeroScore`. Removed the duplicate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` test at line ~288 in DI tests. The `PipelineBuilderTests.cs:688` assertion was already `IsEqualTo(3)` — no change needed.

## Verification

- `dotnet test` — 658 tests, 0 failures, 0 skipped (meets 649+ goal)
- `dotnet build` — zero errors, zero warnings
- All new exception tests use `ThrowsExactly<T>` throughout
- Guard boundary: 50M cells exactly passes; 50M+1 throws `InvalidOperationException`

## Requirements Advanced

- R002 (.NET KnapsackSlice DP guard) — guard implemented, boundary tests pass; partial coverage complete; Rust half remains in S07
- R004 (.NET codebase quality hardening) — all 20 triage items resolved: naming, epsilon, error messages, enum anchors, XML docs, interface contracts, test coverage

## Requirements Validated

- R004 — all 20 triage items verified by `dotnet build` + `dotnet test` passing with zero regressions; requirement is fully satisfied

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

- **T04 test count:** T04 summary reported suite growth from 658→663, but the actual before-T04 count was 653 (confirmed by T01–T03 summaries); after T04's net +5 delta the correct total is 658. The "663" figure in the T04 summary was a transcription error in the narrative.
- **`PipelineBuilderTests.cs:688` assertion:** Already `IsEqualTo(3)` — no change required. Not a regression; the pre-existing code was already correct.

## Known Limitations

- R002 Rust half deferred to S07 (`CupelError::TableTooLarge` + Rust guard not implemented yet)
- `QuotaSlice` had no `ArgumentNullException.ThrowIfNull` guards before T04; added as part of test work (minor production code change not originally scoped but correct)

## Follow-ups

- S07 must add `CupelError::TableTooLarge` variant and the Rust KnapsackSlice guard to complete R002

## Files Created/Modified

- `src/Wollax.Cupel/KnapsackSlice.cs` — DP table guard before ArrayPool.Rent
- `src/Wollax.Cupel/CupelPipeline.cs` — OverflowStrategyValue → OverflowStrategy rename
- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs` — updated rename consumer
- `src/Wollax.Cupel/Slicing/QuotaBuilder.cs` — epsilon fix on total-sum check
- `src/Wollax.Cupel/CupelPolicy.cs` — caller-facing Quotas+Stream error message
- `src/Wollax.Cupel/ScorerEntry.cs` — InnerScorer error message with corrective guidance
- `src/Wollax.Cupel/ScorerType.cs` — Scaled = 6 explicit anchor
- `src/Wollax.Cupel/SlicerType.cs` — Stream = 2 explicit anchor
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — Classify = 0 through Place = 4 explicit anchors
- `src/Wollax.Cupel/ContextItem.cs` — comprehensive XML <summary> on all 11 properties
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — IsEnabled constancy contract
- `src/Wollax.Cupel/ISlicer.cs` — sort precondition in Slice method summary
- `src/Wollax.Cupel/ContextResult.cs` — Report behavioral nullability doc
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — class summary references ITraceCollector
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs` — 4 new tests (guard boundary + negative-token)
- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs` — StreamBatchSize+Knapsack validation test
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs` — null-slicer and null-quotas tests
- `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs` — ScoresAreInZeroToOneRange test
- `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs` — case-insensitive and zero-weight tests
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs` — duplicate test removed
- `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs` — already correct (no change)

## Forward Intelligence

### What the next slice should know
- S07 must implement `CupelError::TableTooLarge` in Rust to complete R002; the .NET guard pattern (long arithmetic, `>` not `>=`, diagnostic message with candidates/capacity/cells) should be mirrored
- The `QuotaSlice` production code now has `ArgumentNullException.ThrowIfNull` guards — this was added as part of T04's test work; it's in the test-gap fixes task but affects production code

### What's fragile
- `TagScorer` case-insensitive matching depends on `FrozenDictionary` preserving the `StringComparer.OrdinalIgnoreCase` comparer from the source `Dictionary` — if the FrozenDictionary construction path changes this could silently break
- QuotaBuilder epsilon (`1e-9`) is a hard-coded constant — if floating-point accumulation changes with different input counts, the threshold may need revisiting

### Authoritative diagnostics
- `rg "OverflowStrategyValue" src/ tests/` — zero results confirms rename is complete
- `dotnet test` count (658) is the authoritative baseline for S07 start

### What assumptions changed
- T04 plan assumed `PipelineBuilderTests.cs:688` had `Count > 0` — the file already had `IsEqualTo(3)`; no change was needed
- T04 test count narrative in summary had a transcription error (reported 663); actual count is 658 (653 baseline + net +5 from T04)
