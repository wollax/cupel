# S06: .NET Quality Hardening — Research

**Date:** 2026-03-21
**Domain:** .NET / C# codebase quality — Wollax.Cupel + tests
**Confidence:** HIGH (all code read directly; triage complete)

## Summary

S06 batch-resolves ~15-20 high-signal .NET issues across `Wollax.Cupel`, its companion packages, and the test suite. The primary new feature is the KnapsackSlice DP table size guard (R002). The remainder are grouped into four themes: API surface correctness, enum forward-compatibility, documentation, and test coverage.

**Baseline:** 649 passing tests across all four test projects (`Wollax.Cupel.Tests`, `Wollax.Cupel.Json.Tests`, `Wollax.Cupel.Extensions.DependencyInjection.Tests`, `Wollax.Cupel.Tiktoken.Tests`). All tests pass with no failures before S06.

**Triage result: 20 high-signal items selected** (naming, enum anchoring, one floating-point correctness bug, guard feature, doc gaps, and real test coverage gaps). Low-signal cosmetic and infrastructure items (NuGet caching, TRX upload, benchmark renaming, ITokenCounter interface design, etc.) are deferred.

## Recommendation

Execute in four tasks:

- **T01** — KnapsackSlice DP guard (R002) — standalone new feature with its own tests
- **T02** — API surface hardening — naming, error messages, enum forward-compat, one correctness fix, XML docs
- **T03** — Interface contract documentation — ITraceCollector, ISlicer, ContextResult, IScorer
- **T04** — Test coverage additions — 10 real test gaps + 2 cleanup items

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Token budget arithmetic | Existing `budget.TargetTokens / _bucketSize` pattern in `KnapsackSlice.Slice()` | Guard check uses same discretized `capacity` variable already computed |
| Enum integer anchoring | `TraceDetailLevel = 0 / 1` is the model | Follow same pattern for `PipelineStage` (0..4) and missing `ScorerType.Scaled = 6`, `SlicerType.Stream = 2` |
| Test structure | `ThrowsExactly<T>` used in `KnapsackSliceTests` and `StreamSliceTests` | Standardize all new exception tests on `ThrowsExactly<T>` |

## Existing Code and Patterns

- `src/Wollax.Cupel/KnapsackSlice.cs` — DP guard goes between "Discretize capacity" and "Rent DP array from pool"; use `(long)candidateCount * (capacity + 1) > 50_000_000L` before `ArrayPool.Rent`. Throw `InvalidOperationException` (no custom exception type exists in .NET — see CupelPipeline.cs pinned budget check pattern).
- `src/Wollax.Cupel/CupelPipeline.cs:30` — `internal OverflowStrategy OverflowStrategyValue` property should be renamed to `OverflowStrategy`. Property name shadowing its type is legal in C# and matches `CupelPolicy.OverflowStrategy`.
- `src/Wollax.Cupel/Slicing/QuotaBuilder.cs:88` — `if (totalRequired > 100)` should be `if (totalRequired > 100.0 + 1e-9)` to handle three 33.333...% kinds without spurious rejection.
- `src/Wollax.Cupel/CupelPolicy.cs:134` — Quotas+Stream error message exposes `QuotaSlice`, `ISlicer`, `IAsyncSlicer`. Replace with caller-facing: `"Quotas cannot be combined with SlicerType.Stream. Stream slicing is asynchronous and does not support synchronous quota wrapping."`.
- `src/Wollax.Cupel/ScorerEntry.cs:86-90` — `"InnerScorer must be null when Type is not Scaled."` → `"InnerScorer is only valid for ScorerType.Scaled. Remove it or change the type to Scaled."`.
- `src/Wollax.Cupel/ScorerType.cs` — `Scaled` has no explicit value; PublicAPI declares `Scaled = 6`. Add `= 6`.
- `src/Wollax.Cupel/SlicerType.cs` — `Stream` has no explicit value; PublicAPI declares `Stream = 2`. Add `= 2`.
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — no explicit values. Add `Classify = 0, Score = 1, Deduplicate = 2, Slice = 3, Place = 4`.
- `src/Wollax.Cupel/ContextItem.cs` — properties have no XML doc comments. `ContextBudget` is the model for what comprehensive XML docs look like.
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — `IsEnabled` doc does not state constancy requirement. Add: callers may cache the value at pipeline entry; implementations should not toggle it mid-run.
- `src/Wollax.Cupel/ISlicer.cs` — sort precondition ("candidates sorted score desc") is buried in param doc. Surface in interface `<summary>`.
- `src/Wollax.Cupel/ContextResult.cs` — `Report` nullability doc references `NullTraceCollector` (concrete). Change to describe the condition in behavioral terms: "null when tracing is disabled."
- `tests/Wollax.Cupel.Tests/Slicing/KnapsackSliceTests.cs` — uses `ThrowsExactly<T>`; add negative-token test and DP guard boundary test here.
- `tests/Wollax.Cupel.Tests/Policy/CupelPolicyTests.cs` — has `Validation_StreamBatchSizeWithGreedySlicer_Throws`; add symmetric Knapsack case.
- `tests/Wollax.Cupel.Tests/Slicing/QuotaSliceTests.cs` — missing constructor null-arg coverage.
- `tests/Wollax.Cupel.Tests/Scoring/PriorityScorerTests.cs` — missing `ScoresAreInZeroToOneRange` (RecencyScorer has it).
- `tests/Wollax.Cupel.Tests/Scoring/TagScorerTests.cs` — missing: (a) case-insensitive match test (`"IMPORTANT"` matches `"important"` key), (b) zero-total-weight branch test.
- `tests/Wollax.Cupel.Extensions.DependencyInjection.Tests/CupelServiceCollectionExtensionsTests.cs:287` — `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` is a duplicate of the test at line 155; remove it.
- `tests/Wollax.Cupel.Tests/Pipeline/PipelineBuilderTests.cs:688` — `result.Items.Count > 0` should be `IsEqualTo(3)` (3 items in the test all fit within 500-token budget).

## Constraints

- `.NET 10` target; no external dependencies in `Wollax.Cupel` core
- No new public API surface beyond the KnapsackSlice guard (which surfaces as `InvalidOperationException` — no new exception type needed)
- `OverflowStrategyValue` → `OverflowStrategy` property rename is `internal`; no public API change, no PublicAPI.txt update needed
- All 649 existing tests must continue to pass after each task
- Must build and test before committing each task

## Common Pitfalls

- **KnapsackSlice guard: use `long` for the multiplication** — `candidateCount * (capacity + 1)` overflows `int` at large values. Guard must be `(long)candidateCount * (capacity + 1) > 50_000_000L`.
- **`capacity` in KnapsackSlice is already discretized** — The relevant allocation is `candidateCount × (capacity + 1)` bools (the `keepArray`), where `capacity = budget.TargetTokens / _bucketSize`. The guard check must use the **post-discretization** `capacity`, not raw `TargetTokens`. Check this *after* computing capacity and before the `ArrayPool.Rent` calls.
- **DiagnosticTraceCollector `_detailLevel < TraceDetailLevel.Item` is acceptable** — `TraceDetailLevel` already has explicit integer values (`Stage=0, Item=1`); the comparison is anchored and not fragile. Do not change it.
- **QuotaBuilder epsilon applies to sum check only** — The per-kind `Require > Cap` validation does not have a floating-point issue (both are user-supplied non-derived values). Only the accumulated `totalRequired > 100` sum needs the `1e-9` tolerance.
- **OverflowStrategy property rename in CupelPipeline** — `OverflowStrategyValue` is used in `CupelServiceCollectionExtensions.cs` (DI extension). Check all `internal` consumers before renaming; `rg OverflowStrategyValue` in the codebase.
- **ScorerType.Scaled = 6** — The value must match `PublicAPI.Shipped.txt`. Verify by checking `src/Wollax.Cupel/PublicAPI.Shipped.txt` before hardcoding.

## Open Risks

- **OverflowStrategyValue rename scope** — need to `rg OverflowStrategyValue` to find all internal consumers before renaming (likely only `CupelServiceCollectionExtensions.cs`).
- **KnapsackSlice guard: threshold semantics** — D006 says "50 million cells (capacity × items)". The actual keepArray allocation is `candidateCount × (capacity + 1)`. The `+1` is minor but the guard should match the allocation shape, not just the abstract "cells" concept. Use `candidateCount * (capacity + 1)` to match actual bytes allocated.
- **Test isolation** — TaskT04 test additions in `Scoring/` and `Slicing/` must not share state with existing tests; use the `CreateItem` helper pattern already established in each test class (do not assume a shared fixture is present — each test class defines its own).

## Issue Triage — Selected for S06

### T01: KnapsackSlice DP guard (R002)

| # | Issue file | Signal | Action |
|---|-----------|--------|--------|
| 1 | (R002 requirement) | **Required** | Add `(long)candidateCount * (capacity + 1) > 50_000_000L` guard, throw `InvalidOperationException`. Add 3 tests: just-below-limit passes, at-limit-fails, above-limit-fails. |

### T02: API surface hardening

| # | Issue file | Signal | Action |
|---|-----------|--------|--------|
| 2 | `2026-03-14-overflow-strategy-value-naming.md` | naming | Rename `OverflowStrategyValue` → `OverflowStrategy` in `CupelPipeline.cs` (internal property) |
| 3 | `2026-03-13-phase6-pr-review-suggestions.md` (#4) | correctness | `QuotaBuilder`: change `> 100` to `> 100.0 + 1e-9` |
| 4 | `2026-03-14-quotas-stream-error-leaks-internals.md` | UX | Replace error message in `CupelPolicy.cs:134` with caller-facing language |
| 5 | `2026-03-14-scorer-entry-rejection-hint.md` | UX | Improve ScorerEntry.cs error message for `InnerScorer` to include corrective guidance |
| 6 | `2026-03-14-explicit-enum-integer-assignments.md` | forward-compat | `ScorerType.Scaled = 6`, `SlicerType.Stream = 2` |
| 7 | `phase02-review-03.md` | forward-compat | `PipelineStage` explicit integer assignments (0–4) |
| 8 | `007-contextitem-xml-docs.md` | docs | Add XML `<summary>` comments to all `ContextItem` properties |

### T03: Interface contract documentation

| # | Issue file | Signal | Action |
|---|-----------|--------|--------|
| 9 | `phase02-review-05.md` / `phase05-review-suggestions-docs.md` | contract | `ITraceCollector.IsEnabled`: document constancy — "callers may cache the result for a pipeline run; implementations must not toggle this mid-run" |
| 10 | `phase02-review-01.md` / `phase05-review-suggestions-docs.md` | contract | `ISlicer`: move sort-precondition note to interface `<summary>` (currently only in param doc) |
| 11 | `phase02-review-12.md` | doc clarity | `ContextResult.Report`: decouple nullability from `NullTraceCollector` concrete reference |
| 12 | `phase02-review-02.md` | doc clarity | `SelectionReport`: replace `DiagnosticTraceCollector` reference with `ITraceCollector` in XML doc |

### T04: Test coverage additions

| # | Issue file | Signal | Action |
|---|-----------|--------|--------|
| 13 | `2026-03-13-phase6-pr-review-suggestions.md` (#3) | test gap | `KnapsackSliceTests`: add test that negative-token items are silently skipped |
| 14 | `2026-03-14-missing-knapsack-stream-batchsize-test.md` | test gap | `CupelPolicyTests`: add `Validation_StreamBatchSizeWithKnapsackSlicer_Throws` |
| 15 | `2026-03-13-phase6-pr-review-suggestions.md` (#2) | test gap | `QuotaSliceTests`: add `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull` |
| 16 | `scorer-test-gaps.md` (#1) | test gap | `PriorityScorerTests`: add `ScoresAreInZeroToOneRange` (matches RecencyScorer pattern) |
| 17 | `scorer-test-gaps.md` (#2) | test gap | `TagScorerTests`: add case-insensitive tag matching test |
| 18 | `scorer-test-gaps.md` (#3) | test gap | `TagScorerTests`: add zero-total-weight test (untested branch) |
| 19 | `2026-03-14-duplicate-transient-tracecollector-test.md` | hygiene | Remove duplicate `AddCupelTracing_IsTransient_DifferentInstancesPerResolve` (line 287) |
| 20 | `2026-03-14-stream-slicer-weak-assertion.md` | test quality | `PipelineBuilderTests:688` — change `Count > 0` to `IsEqualTo(3)` |

## Deferred (not high-signal enough for S06)

- NuGet cache / TRX upload in CI (phase10 CI infra) — pure CI plumbing, no correctness impact
- `ITokenCounter` interface in core (phase10) — design change, not quality debt
- `CupelPresets` static singleton caching (phase08) — micro-perf, not correctness
- Benchmark naming inconsistency (phase04) — cosmetic
- Duplicate `CreateItem`/`PassAllSlicer` helpers in tests (phase07) — refactor, no coverage gap
- `IsSealed` reflection tests (phase07) — remove if desired but low impact
- `CupelOptions` `RegisteredIntents` surface (phase08) — new public API, not S06 scope
- `AddCupel` additive behavior doc, `WithTokenCount` overwrite doc, `TiktokenTokenCounter` thread-safety (phase10) — good docs issues but not high-signal for a quality hardening batch

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| C# / .NET | none needed | n/a — standard .NET patterns, no specialized tooling required |

## Sources

- Direct code reads: `KnapsackSlice.cs`, `CupelPipeline.cs`, `CupelPolicy.cs`, `ScorerEntry.cs`, `QuotaBuilder.cs`, `DiagnosticTraceCollector.cs`, `ITraceCollector.cs`, `IScorer.cs`, `ScorerType.cs`, `SlicerType.cs`, `PipelineStage.cs`, `TraceDetailLevel.cs`, `OverflowStrategy.cs`
- All 90 open issues in `.planning/issues/open/` triaged; 20 selected for S06
- Baseline test run: 649 passing, 0 failing (confirmed with `dotnet test --no-build`)
- Decisions register (DECISIONS.md): D006 (50M cell threshold), D008 (QH scope definition)
