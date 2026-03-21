---
id: S06
parent: M001
milestone: M001
provides:
  - KnapsackSlice DP table size guard — throws InvalidOperationException when (long)candidateCount * (capacity+1) > 50_000_000L
  - QuotaBuilder epsilon fix — three 33.333...% quota kinds accepted without spurious rejection
  - CupelPipeline.OverflowStrategyValue renamed to OverflowStrategy (internal)
  - Caller-facing CupelPolicy Quotas+Stream error message (no internal type names)
  - ScorerEntry InnerScorer error message with corrective guidance
  - ScorerType.Scaled = 6, SlicerType.Stream = 2, PipelineStage 0–4 explicit integer anchors
  - Comprehensive XML <summary> docs on all 11 ContextItem properties
  - ITraceCollector.IsEnabled constancy contract in XML docs
  - ISlicer.Slice method summary states sort precondition (candidates sorted score desc)
  - ContextResult.Report uses behavioral nullability language ("null when tracing is disabled")
  - SelectionReport class summary references ITraceCollector not DiagnosticTraceCollector
  - 6 new tests (KnapsackSlice guard boundary x3, negative-token x1, null constructor args x2, scorer range x1, tag scorer x2, policy stream+knapsack x1)
  - 1 duplicate test removed (CupelServiceCollectionExtensionsTests)
  - 658 tests total, 0 failures
requires: []
affects:
  - S07
key_files:
  - src/Wollax.Cupel/KnapsackSlice.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs
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
  - tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs
  - tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs
  - tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs
  - tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs
  - tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs
  - tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs
key_decisions:
  - D031 — Error messages name only public API surface (no internal types in user-visible exceptions)
  - D032 — Epsilon applied only to total-sum check in QuotaBuilder (not per-kind checks)
  - D033 — Interface contract docs use interface types (ITraceCollector), not concrete implementations
patterns_established:
  - OOM-bound guard pattern: compute cell count with long arithmetic, throw InvalidOperationException with diagnostic interpolation before allocating
  - Error messages name only the public API surface (param name, enum value)
  - Interface contract XML docs describe behavioral conditions using interface types, not concrete implementations
observability_surfaces:
  - KnapsackSlice InvalidOperationException message includes candidates=N, capacity=C, cells=K for diagnosability
  - CupelPolicy Quotas+Stream exception message names public constraint — visible in test output and logs
  - ScorerEntry InnerScorer exception message includes corrective action ("Remove it or change the type to Scaled")
drill_down_paths:
  - .kata/milestones/M001/slices/S06/tasks/T01-SUMMARY.md
  - .kata/milestones/M001/slices/S06/tasks/T02-SUMMARY.md
  - .kata/milestones/M001/slices/S06/tasks/T03-SUMMARY.md
  - .kata/milestones/M001/slices/S06/tasks/T04-SUMMARY.md
duration: ~2h total (T01: 10m, T02: 12m, T03: short, T04: ~60m)
verification_result: passed
completed_at: 2026-03-21
---

# S06: .NET Quality Hardening

**658 tests pass with zero regressions; 20 triage items resolved across KnapsackSlice OOM guard, API surface hardening, interface contract docs, and test coverage gaps.**

## What Happened

S06 executed as four sequential tasks resolving all 20 planned triage items across the `Wollax.Cupel` library and its test suite.

**T01 — KnapsackSlice DP guard:** R002's OOM guard inserted after `discretizedWeights` is built and before any `ArrayPool.Rent` calls. The guard computes `cellCount = (long)candidateCount * (capacity + 1)` and throws `InvalidOperationException` when `cellCount > 50_000_000L`. The `long` cast on the first operand promotes the entire multiplication, preventing int overflow. Four tests cover: at-limit passes (5000 × 10000 = 50M → no throw), one-above-limit throws (5000 × 10001 = 50,005,000), clearly-over throws (10000 × 5001), and the existing negative-token silent-exclusion filter.

**T02 — API surface hardening:** Seven fixes applied cleanly. `CupelPipeline.OverflowStrategyValue` renamed to `OverflowStrategy` (internal only; one consumer updated in `CupelServiceCollectionExtensions.cs`). QuotaBuilder epsilon fix allows three equal-share 33.333...% quotas through the total-sum check without floating-point drift rejection. `CupelPolicy` Quotas+Stream error message rewritten to name only public types. `ScorerEntry` InnerScorer error adds corrective guidance ("Remove it or change the type to Scaled"). `ScorerType.Scaled = 6`, `SlicerType.Stream = 2`, `PipelineStage` 0–4 all anchored after verification against `PublicAPI.Shipped.txt`. All 11 `ContextItem` properties received comprehensive XML `<summary>` docs with units, invariants, and behavioral notes.

**T03 — Interface contract docs:** Four targeted doc-only changes: `ITraceCollector.IsEnabled` documents the constancy requirement (callers may cache; implementations must not toggle mid-run). `ISlicer.Slice` method summary now states the sort precondition. `ContextResult.Report` uses behavioral nullability language without referencing concrete types. `SelectionReport` class summary references `ITraceCollector` not `DiagnosticTraceCollector`.

**T04 — Test coverage and hygiene:** Six test files updated. New tests: `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` (CupelPolicy), `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull` backed by `ArgumentNullException.ThrowIfNull` guards added to `QuotaSlice` constructor, `ScoresAreInZeroToOneRange` (PriorityScorer), `TagScorer_CaseInsensitiveMatch` and `TagScorer_ZeroTotalWeight_ReturnsZeroScore`. Duplicate DI test `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` removed. `PipelineBuilderTests.cs:688` assertion was already `IsEqualTo(3)` — no change needed. Net result: +5 tests (6 added, 1 removed), total 658.

## Verification

- `dotnet test` — 658 tests, 0 failures, 0 skipped ✅
- `dotnet build` — zero errors, zero warnings ✅
- `dotnet test --treenode-filter "/*/*/KnapsackSliceTests/*"` — 17 tests pass including all 4 new guard tests ✅
- `dotnet test --treenode-filter "/*/*/CupelPolicyTests/*"` — 21 tests pass including Knapsack stream batch size test ✅
- `dotnet test --treenode-filter "/*/*/QuotaSliceTests/*"` — 13 tests pass including null constructor arg tests ✅
- All new exception tests use `ThrowsExactly<T>` ✅

## Requirements Advanced

- R002 — .NET half of KnapsackSlice DP guard now implemented; `InvalidOperationException` thrown at >50M cells with diagnostic message; four tests verify boundary behavior
- R004 — All 20 triage items resolved: naming, error messages, enum anchoring, epsilon fix, XML docs, test gaps, test hygiene

## Requirements Validated

- R004 — .NET codebase quality hardening: all 20 high-signal issues resolved; 658 tests pass with zero regressions; `dotnet build` clean; R004 is validated

## New Requirements Surfaced

- None — the triage scope was pre-defined; no new issues identified during execution that warrant tracking

## Requirements Invalidated or Re-scoped

- None

## Deviations

- **T04 test count:** T04 summary reports "658 to 663" but actual post-T04 count is 658. Pre-T04 count was 653 (confirmed by T01–T03 summaries all reporting 653). With +6 new tests −1 duplicate = net +5, the final count is 658, not 663. The T04 summary's starting baseline was incorrect. The "649+" slice goal is satisfied.
- `PipelineBuilderTests.cs:688` — Plan said "Change `Count > 0` to `IsEqualTo(3)`"; the assertion was already `IsEqualTo(3)` in the codebase. No change required.

## Known Limitations

- R002 is half-validated: the .NET guard is in place and tested; the Rust guard (`CupelError::TableTooLarge`) is S07's responsibility. R002 will be fully validated after S07.
- 20 issues resolved but ~70 open issues remain in `.planning/issues/open/` — these are cosmetic or low-signal and intentionally deferred

## Follow-ups

- S07 must add the Rust-side `CupelError::TableTooLarge` variant and KnapsackSlice guard to complete R002

## Files Created/Modified

- `src/Wollax.Cupel/KnapsackSlice.cs` — DP table size guard inserted after discretizedWeights, before ArrayPool.Rent
- `src/Wollax.Cupel/CupelPipeline.cs` — OverflowStrategyValue → OverflowStrategy rename
- `src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs` — updated to OverflowStrategy
- `src/Wollax.Cupel/Slicing/QuotaBuilder.cs` — epsilon fix on total-sum check; ArgumentNullException.ThrowIfNull added to QuotaSlice constructor
- `src/Wollax.Cupel/CupelPolicy.cs` — caller-facing Quotas+Stream error message
- `src/Wollax.Cupel/ScorerEntry.cs` — improved InnerScorer error message with corrective guidance
- `src/Wollax.Cupel/ScorerType.cs` — Scaled = 6 explicit anchor
- `src/Wollax.Cupel/SlicerType.cs` — Stream = 2 explicit anchor
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — Classify = 0 through Place = 4 explicit anchors
- `src/Wollax.Cupel/ContextItem.cs` — comprehensive XML <summary> docs on all 11 properties
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — IsEnabled constancy contract
- `src/Wollax.Cupel/ISlicer.cs` — sort precondition in Slice method summary
- `src/Wollax.Cupel/ContextResult.cs` — behavioral nullability language on Report property
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — class summary references ITraceCollector
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs` — 4 new tests (guard boundary x3, negative-token x1)
- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs` — Validation_StreamBatchSizeWithKnapsackSlicer_Throws
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs` — QuotaSlice_NullSlicer_ThrowsArgumentNull, QuotaSlice_NullQuotas_ThrowsArgumentNull
- `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs` — ScoresAreInZeroToOneRange
- `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs` — TagScorer_CaseInsensitiveMatch, TagScorer_ZeroTotalWeight_ReturnsZeroScore
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs` — duplicate test removed

## Forward Intelligence

### What the next slice should know
- S07 inherits a clean .NET suite (658 tests, zero build warnings) — no regressions to worry about from S06 changes
- The Rust KnapsackSlice guard (S07/T01 or similar) should mirror the .NET pattern: `(long)candidateCount * (capacity + 1) > 50_000_000L`, throw with diagnostic fields, test at-limit / one-above / clearly-over
- The `CupelError::TableTooLarge` variant is specified but not yet created; `#[non_exhaustive]` on `CupelError` is already in place (D015)

### What's fragile
- `QuotaBuilder` epsilon — only applied to the total-sum check; per-kind `Require > Cap` checks remain integer-precision. Any future floating-point quota changes should check whether additional epsilon guards are needed there.
- `TagScorer` case-insensitive behavior depends on `FrozenDictionary` preserving the `StringComparer.OrdinalIgnoreCase` comparer from the source `Dictionary`. If the source Dictionary construction changes, case-insensitivity could silently break.

### Authoritative diagnostics
- `dotnet test` output is the ground truth for test counts and pass/fail — summary files may have incorrect counts
- `rg "OverflowStrategyValue" src/ tests/` → zero results confirms rename complete
- `dotnet build` warnings=0 is the authoritative build-clean signal

### What assumptions changed
- T04 test count: plan assumed pre-T04 suite was 658 (written after observing T01 result), but T01-T03 summaries consistently report 653. The net +5 from T04 lands at 658, not 663.
- `PipelineBuilderTests.cs:688` `Count > 0` assertion was already `IsEqualTo(3)` — triage issue was pre-fixed in a prior session.
