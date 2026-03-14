# Cupel — Project State

## Current Position

Phase: 13 of 15 — Budget Contract Implementation
Milestone: v1.0 Core Library
Plan: 1 of 2
Status: In progress
Last activity: 2026-03-14 — Completed 13-01-PLAN.md (Budget Contract Wiring)

Progress: █████████████████████████████████████████░░░░ 41/42 plans (97%)

## Phase Overview

NEXT_PHASE=13

| Phase | Status |
|-------|--------|
| 1. Project Scaffold & Core Models | ● complete (5/5 plans) |
| 2. Interfaces & Diagnostics Infrastructure | ● complete (2/2 plans) |
| 3. Individual Scorers | ● complete (3/3 plans) |
| 4. Composite Scoring | ● complete (3/3 plans) |
| 5. Pipeline Assembly & Basic Execution | ● complete (3/3 plans) |
| 6. Advanced Slicers & Quota System | ● complete (5/5 plans) |
| 7. Explainability & Overflow Handling | ● complete (3/3 plans) |
| 8. Policy System & Named Presets | ● complete (3/3 plans) |
| 9. Serialization & JSON Package | ● complete (3/3 plans) |
| 10. Companion Packages & Release | ● complete (3/3 plans) |
| 11. Language-Agnostic Specification | ● complete (3/3 plans) |
| 12. Rust Crate (Assay) | ● complete (3/3 plans) |
| 13. Budget Contract Implementation | ◐ in progress (1/2 plans) |
| 14. Policy Type Completeness | ○ planned |
| 15. Conformance Hardening | ○ planned |

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
- ExclusionReason evolved from 4 to 8 values: BudgetExceeded, ScoredTooLow, Deduplicated, QuotaCapExceeded, QuotaRequireDisplaced, NegativeTokens, PinnedOverride, Filtered
- SelectionReport.TotalCandidates and TotalTokensConsidered are `required` (not optional) to avoid ambiguous default-0 semantics
- InternalsVisibleTo added to Wollax.Cupel.csproj for test access to internal ReportBuilder
- ReportBuilder uses (ExcludedItem, int Index) tuple array sort for stable descending score ordering
- ScorerEntry uses IReadOnlyDictionary for KindWeights/TagWeights with defensive copies via new Dictionary(source)
- CupelPolicy uses [..source] collection expression spread for defensive copies of Scorers and Quotas lists
- ContextKindJsonConverter and ContextKindDictionaryConverter made public for cross-assembly source gen access
- ContextKindJsonConverter needs ReadAsPropertyName/WriteAsPropertyName for Dictionary<ContextKind,T> key serialization
- STJ source gen UseStringEnumConverter does not apply naming policy — use [JsonStringEnumMemberName] on each enum member
- CupelJsonSerializer uses separate overloads (not optional params) to satisfy RS0026 backcompat analyzer
- RegisterScorer stores all factories as Func<JsonElement?, IScorer> internally; parameterless overload wraps via _ => factory()
- Unknown scorer type detection uses JsonDocument.Parse on raw JSON to extract type names and compare against built-in set
- Built-in scorer type names hardcoded as string array matching [JsonStringEnumMemberName] values on ScorerType
- .NET 10 STJ does NOT wrap JsonConstructor ArgumentException in JsonException — exceptions propagate unwrapped
- CupelJsonSerializer facade catches ArgumentException from constructors and wraps in JsonException with `$:` path prefix
- DI extension methods namespace: Microsoft.Extensions.DependencyInjection (standard .NET convention)
- AddCupelTracing uses TryAddTransient to avoid overriding user-provided ITraceCollector
- ContextBudget is a registration-time parameter on AddCupelPipeline, not part of CupelOptions
- TiktokenTokenCounter wraps Tokenizer (base class) not TiktokenTokenizer for field type flexibility
- Microsoft.ML.Tokenizers throws NotSupportedException for unrecognized model/encoding names
- Tiktoken bridge has no hard dependency on data packages — consumers add O200kBase/Cl100kBase as needed
- Consumption test project uses `*-*` version wildcard to match prerelease .nupkg from MinVer
- Consumption test project needs `PackageReference Update` for SourceLink/MinVer inherited from Directory.Build.props when ManagePackageVersionsCentrally is disabled
- Spec uses mdBook for publication, TOML for conformance test vectors, behavioral-equivalence conformance model
- Spec mandates IEEE 754 64-bit doubles, stable sort with (Score, Index) tiebreaking, byte-exact ordinal deduplication
- Conformance suite: 28 required + 9 optional TOML test vectors covering all scorers, slicers, placers, and pipeline
- Rust Scorer trait requires Any supertrait + as_any() method for CompositeScorer DFS cycle detection downcasting
- Rust ContextItem metadata uses HashMap<String, String> instead of serde_json::Value to avoid public serde_json dependency
- Rust cycle detection uses usize (data pointer cast) in HashSet to avoid lifetime issues with raw fat pointers
- Rust Pipeline uses Box<dyn Scorer>, Box<dyn Slicer>, Box<dyn Placer> for trait object composition
- Rust QuotaSlice sub-budget uses ContextBudget::new with maxTokens=cap, targetTokens=kindBudget (cap may be < kindBudget when proportional exceeds cap)
- Rust UShapedPlacer uses Vec<Option<ContextItem>> for result array to avoid unsafe uninitialized memory
- Rust place stage looks up original scores by content match when re-associating sliced items with scores
- Rust conformance test runner uses toml::Value for dynamic TOML parsing with factory functions for scorers/slicers/placers
- All 28 required conformance tests pass — first non-C# implementation validated against the spec
- Budget contract wiring: ReservedSlots subtracted first, then EstimationSafetyMarginPercent applied as multiplicative reduction
- Safety margin uses int cast (truncation) for effective budget values, consistent with existing int budget semantics
- Streaming path uses foreach over ReservedSlots (no pinnedTokens in streaming mode)

### Roadmap Evolution
- Phase 11 added: Language-Agnostic Specification — formal spec for Cupel's algorithm, enabling multi-language implementations
- Phase 12 added: Rust Crate (Assay) — first non-C# implementation, validates spec's language-independence

### Blockers
(None)

### Technical Debt
(None)
