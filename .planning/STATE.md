# Cupel — Project State

## Current Position

Phase: 6 of 12 — Advanced Slicers & Quota System
Milestone: v1.0 Core Library
Plan: 5 of 5
Status: Complete
Last activity: 2026-03-13 — Completed 06-05-PLAN.md (Slicer benchmarks)

## Phase Overview

NEXT_PHASE=7

| Phase | Status |
|-------|--------|
| 1. Project Scaffold & Core Models | ● complete (5/5 plans) |
| 2. Interfaces & Diagnostics Infrastructure | ● complete (2/2 plans) |
| 3. Individual Scorers | ● complete (3/3 plans) |
| 4. Composite Scoring | ● complete (3/3 plans) |
| 5. Pipeline Assembly & Basic Execution | ● complete (3/3 plans) |
| 6. Advanced Slicers & Quota System | ● complete (5/5 plans) |
| 7. Explainability & Overflow Handling | ○ planned |
| 8. Policy System & Named Presets | ○ planned |
| 9. Serialization & JSON Package | ○ planned |
| 10. Companion Packages & Release | ○ planned |
| 11. Language-Agnostic Specification | ○ planned |
| 12. Rust Crate (Assay) | ○ planned |

## Accumulated Context

### Decisions
- Content is non-nullable string on ContextItem (brainstorm explored nullable, PROJECT.md specifies non-nullable)
- Ordinal-only scoring invariant confirmed across all brainstorm tracks
- CompositeScorer over scorer DAG (nested composites, ~30 lines vs hundreds)
- Fixed pipeline over middleware (no call-next, no silent-drop)
- IPlacer interface (pluggable, U-shaped as default not mandate)
- GreedySlice is the default slicer; KnapsackSlice is opt-in for provably optimal selection
- Named policies are [Experimental] at launch — opinionated defaults that will evolve
- Phase 1 includes BenchmarkDotNet and PublicApiAnalyzers as non-negotiable infrastructure
- .NET 10 SDK generates .slnx (XML solution format) by default — used Cupel.slnx instead of Cupel.sln
- .NET 10 SDK requires Microsoft.Testing.Platform runner mode via global.json for dotnet test with TUnit
- STJ returns null (not JsonException) for JSON literal `null` on reference types — converter not invoked
- Record value equality uses reference equality for collection properties (IReadOnlyList, IReadOnlyDictionary) — not structural
- TUnit exception assertions: use Throws<T>() / ThrowsExactly<T>(), not ThrowsException().OfType<T>()
- ContextBudget is a sealed class (not record) to prevent with-expressions bypassing constructor validation
- Custom ContextKindDictionaryConverter handles Dictionary<ContextKind, int> serialization for ReservedSlots
- ScoredItem lives in root namespace (Wollax.Cupel) — appears in pipeline interface signatures
- TraceEvent uses required init properties (not positional constructor) for clarity
- TUnit HasCount() obsolete in current version — use Count().IsEqualTo(n)
- TUnit Assert.That(constant) triggers analyzer error — use non-constant expressions
- ContextResult.TotalTokens uses manual for-loop (no LINQ) to avoid delegate allocations
- Sealed records with required properties in .NET 10 do not generate public copy constructors
- No AsyncLocal in codebase — explicit ITraceCollector parameter propagation confirmed
- Scorer zero-allocation discipline: for loops with indexer access only, no LINQ/foreach/closures in Score() methods
- Rank-based scoring pattern: count items with lesser value, interpolate rank/(countWithValues-1), null → 0.0, single → 1.0
- TUnit treenode-filter does not support `--filter` flag — use `--treenode-filter` with path syntax instead
- ScaledScorer degenerate case: use `max == min` exact equality (not epsilon), return 0.5 (midpoint)
- ScaledScorer exposes inner scorer via `internal IScorer Inner` property for CompositeScorer cycle detection traversal
- CompositeScorer uses parallel IScorer[] and double[] arrays with pre-normalized weights for zero-allocation Score()
- Relative weight equivalence tests need Within(1e-14) tolerance due to floating-point normalization differences
- Stable sort pattern: (double Score, int Index) tuple array + Array.Sort with static comparison delegate — adopted in Phase 5 GreedySlice, UShapedPlacer, ChronologicalPlacer
- TUnit treenode-filter: `**` wildcard only valid in final path segment — use full path like `/Wollax.Cupel.Tests/Wollax.Cupel.Tests.Slicing/GreedySliceTests/**`
- KnapsackSlice uses 2D boolean keep table for 1D DP reconstruction — standard reverse-scan comparison (dp[w] != dp[w-dw]) fails for single-item and same-value cases
- KnapsackSlice tests use bucketSize=1 for precision-sensitive cases, realistic token values (500+) with default bucketSize=100
- StreamSlice uses CancelAsync() + linked CTS for budget-full signalling; swallows OperationCanceledException when self-initiated
- IAsyncSlicer is the async counterpart of ISlicer for streaming IAsyncEnumerable sources
- QuotaSet uses internal constructor — only QuotaBuilder.Build() can create instances
- QuotaSlice proportional distribution uses integer arithmetic with candidate token mass weighting
- Unconfigured kinds receive proportional share of unassigned budget based on candidate token mass
- TraceEvent.Message: optional string property for diagnostic warnings (pinned+quota conflict, etc.)
- WithQuotas wraps whatever slicer is currently set at call time — ordering matters
- ExecuteStreamAsync checks cancellationToken.ThrowIfCancellationRequested() at entry before async work
- ScoreStreamAsync uses micro-batch scoring aligned to StreamSlice.BatchSize for meaningful relative scorer context

### Roadmap Evolution
- Phase 11 added: Language-Agnostic Specification — formal spec for Cupel's algorithm, enabling multi-language implementations
- Phase 12 added: Rust Crate (Assay) — first non-C# implementation, validates spec's language-independence

### Blockers
(None)

### Technical Debt
(None)
