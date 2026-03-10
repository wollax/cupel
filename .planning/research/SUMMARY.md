# Research Summary: Cupel Context Management Library

**Date**: 2026-03-10
**Synthesized from**: STACK.md, FEATURES.md, ARCHITECTURE.md, PITFALLS.md
**Consumer**: kata-roadmapper agent

---

## Executive Summary

Cupel is positioned to be the first dedicated context management library in the .NET ecosystem — a genuine blue ocean. Every major AI framework (LangChain, Semantic Kernel, LlamaIndex) treats context management as incidental to their primary purpose: they trim, reduce, or rerank, but none optimize. Cupel's core value proposition is a budget-constrained scoring pipeline with full explainability — a combination that does not exist anywhere today. The `SelectionReport`/`DryRun()` with `ExclusionReason` enum is the killer feature: 65% of enterprise AI failures in 2025 are attributed to context drift or memory loss, yet no solution tells you what was dropped or why.

The technical stack is well-researched and high-confidence. .NET 10/C# 14 with zero external dependencies in core is the right call. The fixed synchronous pipeline (Classify → Score → Deduplicate → Slice → Place) with async only at the `IContextSource` boundary is architecturally sound: it enables `Span<T>`, `stackalloc`, zero async state machine overhead, and stays under the <1ms target for <500 items. The companion package structure (DI, Tiktoken, Json) correctly externalizes optional dependencies. There are no significant unknowns in the stack — all technology decisions are HIGH confidence.

The primary execution risk is not architectural but implementation discipline: hidden allocations in hot paths (LINQ in scoring loops, closure captures, boxing, ungated trace construction) can destroy the sub-millisecond performance target without BenchmarkDotNet catching it early. The second risk is API surface brittleness — nullable annotation consistency, read-only vs mutable collection types, and `[JsonPropertyName]` from day one are non-negotiable Phase 1 constraints. The roadmap must treat benchmark infrastructure and `Microsoft.CodeAnalysis.PublicApiAnalyzers` as Phase 1 deliverables, not afterthoughts.

---

## Key Findings by Research Area

### Stack (STACK.md)

- **Runtime**: .NET 10 LTS, single TFM `net10.0`, C# 14. No multi-targeting. HIGH confidence, correct call.
- **C# 14 features with direct payoff**: extension members (fluent API, `IServiceCollection`), `field` keyword (property validation without backing fields), implicit `Span<T>` conversions (zero-alloc hot paths).
- **Testing**: xUnit v3 (3.2.2) — de facto standard, `ValueTask` lifecycle, parallel by default. Shouldly for assertions (MIT, FluentAssertions v8+ now requires commercial license). Verify.XunitV3 for snapshot testing `ContextResult`/`SelectionReport`/`ContextTrace`.
- **Benchmarking**: BenchmarkDotNet 0.15.8 with `[MemoryDiagnoser]` and `[ThreadingDiagnoser]`. Sub-millisecond targets require careful configuration (`WithIterationTime(250ms)`, `WithMaxRelativeError(0.02)`).
- **Tokenizer companion**: `Microsoft.ML.Tokenizers` v2.0.0 (Microsoft-owned, SharpToken README explicitly recommends migrating to it). Targets net8.0 but forward-compatible with .NET 10.
- **Packaging**: Central Package Management (`Directory.Packages.props`), MinVer for git tag-based versioning, NuGet Trusted Publishing via OIDC (no stored API keys), SourceLink enabled.
- **Verification gaps** (must check before implementation): Shouldly and MinVer versions may be stale; `Microsoft.ML.Tokenizers` net10.0 forward compat should be tested early; `[JsonPropertyName]` attribute availability in-box without NuGet reference.

### Features (FEATURES.md)

- **Ecosystem gap is real**: No existing solution combines multi-signal composite scoring + budget-constrained optimization + research-backed placement + full explainability. LangChain trims, LlamaIndex reranks, Semantic Kernel reduces. Nobody optimizes.
- **Table stakes** (must ship in v1): `ContextItem`, `ContextBudget`, fixed pipeline, `IScorer` + `RecencyScorer` + `PriorityScorer`, `ISlicer` + `GreedySlice`, `IPlacer` + `UShapedPlacer`, pinned items, `ContextResult`, zero-dependency core.
- **Differentiators** (competitive advantage, should ship in v1): `CompositeScorer` with weighted aggregation, `KnapsackSlice` with discretization, `QuotaSlice`, `SelectionReport`/`DryRun()`, `ContextTrace` with gated construction, `ExclusionReason` enum, `CupelPolicy` as serializable config, named policy presets.
- **Anti-features** (correct exclusions): No embeddings, no LLM reranking, no compression/summarization, no storage, no retrieval. These constraints are a feature of the architecture, not a limitation.
- **UShapedPlacer** is validated by the "Lost in the Middle" Stanford paper (2024-2025 follow-ups). The effect is model-dependent, which validates `IPlacer` as pluggable.
- **KnapsackSlice** requires token discretization (100-token buckets) to make W manageable (W'=1000 instead of W=100K). `GreedySlice` should be the default; `KnapsackSlice` is opt-in for provably optimal selection at accepted cost.
- **Named policies** (`ChatSession()`, `CodeReview()`, `AgentLoop()`) are the primary adoption lever — they lower the floor and serve as living documentation.

### Architecture (ARCHITECTURE.md)

- **Pipeline pattern**: Fixed stage pipeline with `PipelineExecutor` as internal orchestrator. Each stage has a narrow single-method interface. No middleware, no chain-of-responsibility.
- **Sync/async boundary**: Pipeline is synchronous. `IContextSource` is async (`IAsyncEnumerable<ContextItem>`). `ApplyAsync` materializes the source then calls the synchronous `Apply`. This enables `Span<T>` and avoids ~360-600 bytes of async state machine allocation per pipeline run.
- **Zero-allocation strategy**: `ArrayPool<T>.Shared` for temporary scored item arrays, `stackalloc` for small bounded buffers (CompositeScorer weights, typically <10), `ReadOnlySpan<char>` for string comparisons in classifiers, `ThreadStatic` scratch list pooling.
- **`ScoredItem` as readonly struct**: Stored inline in arrays, no per-item heap allocation between Score and Place stages.
- **`ContextItem` as sealed class**: Reference type fields (string Content, Tags, Metadata) make struct copying costly; sealed enables devirtualization.
- **ITraceCollector**: Custom interface, not `Activity`/`ActivitySource` (wrong abstraction — that's for distributed cross-service tracing). `NullTraceCollector` as singleton, all trace construction gated behind `IsEnabled`. DiagnosticSource bridge optional, belongs in DI package.
- **Builder pattern**: Hand-written `CupelPipelineBuilder`, not source-generated. Validation at `Build()` time, not runtime. Factory entry point: `CupelPipeline.CreateBuilder()`.
- **DI integration**: `TryAddSingleton` pattern, `IOptions<CupelOptions>`, keyed services for named pipelines (.NET 8+, available in .NET 10). Extension method namespace should be `Wollax.Cupel.Extensions.DependencyInjection`, not `Microsoft.Extensions.DependencyInjection`.
- **Build order**: Models → Interfaces → Diagnostics → Individual Scorers → CompositeScorer → Slicers → Placement + Classification → Pipeline Assembly → Policy + Presets → Companion Packages. This ordering is the natural phase structure.

### Pitfalls (PITFALLS.md)

- **Phase 1 scaffold non-negotiables**: `<Nullable>enable</Nullable>` project-wide (not file-level), `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`, `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`, single `<Version>` source in `Directory.Build.props`, CI zero-dependency assertion for core package, SourceLink from day one.
- **Core model non-negotiables**: All result types use `IReadOnlyList<T>` (not `List<T>`), `ContextItem` is immutable (`{ get; init; }`), `[JsonPropertyName]` on every serializable public property, `ArgumentNullException.ThrowIfNull()` on all public entry points.
- **Hot path discipline**: No LINQ in `IScorer.Score()`, `ISlicer.Slice()`, `IPlacer.Place()`. No closure captures in scoring lambdas. No boxing (especially `ContextItem.Metadata` must be `Dictionary<string, string>`, never `Dictionary<string, object>`). ALL trace construction gated behind `ITraceCollector.IsEnabled`.
- **Scoring correctness**: `CompositeScorer` must normalize weights internally (relative, not absolute). `RecencyScorer` must use relative timestamp ranking within the input set, not absolute time distance from now (purity invariant). Never compare scores with `==`. Use stable sort; secondary tiebreaking key is timestamp or insertion index.
- **DI pitfalls**: Pipeline/trace collector should be transient/scoped, not singleton. Scorers/slicers/placers are singleton (stateless). Enable `validateScopes: true` in development. Core must be fully usable without DI via `CupelPipeline.CreateBuilder()`.
- **Testing pitfalls**: Test through public API, not internals. Assert ordinal relationships ("A scores higher than B"), not exact doubles. Consumption tests against packed `.nupkg` (not `<ProjectReference>`) must be in CI before first publish. Benchmarks alongside first pipeline implementation, not "later."
- **JSON pitfalls**: Polymorphic serialization (`[JsonDerivedType]` with `$type` discriminator) belongs in the `Wollax.Cupel.Json` package, not core. AOT/trimming support with source generation is limited with polymorphism — document as unsupported in v1 and test in a trimmed app. `[JsonConstructor]` required on types with `init` properties.

---

## Roadmap Implications

### Suggested Phase Structure

**Phase 1: Foundation and Infrastructure** (highest risk, must be right)

Everything in this phase is load-bearing. Getting it wrong creates breaking changes.

- Project scaffold: solution structure, `Directory.Build.props`, `Directory.Packages.props`, `global.json`, `TreatWarningsAsErrors`, `Nullable`, SourceLink, `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
- CI pipeline: build, test, pack, zero-dependency assertion (`dotnet list Wollax.Cupel package` = 0), consumption test project against local NuGet feed.
- Core models: `ContextItem` (immutable, sealed, `[JsonPropertyName]`), `ContextBudget`, `ScoredItem` (readonly struct), `OverflowStrategy` enum, `ExclusionReason` enum.
- All public interfaces: `IScorer`, `ISlicer`, `IPlacer`, `IClassifier`, `IContextSource`, `ITraceCollector`.
- Diagnostics infrastructure: `NullTraceCollector` (singleton), `DiagnosticTraceCollector`, `ContextTrace`, `ContextResult`.
- BenchmarkDotNet project with baseline benchmarks (empty pipeline, 500 items). Establish the sub-millisecond baseline before writing any real scorer.

**Phase 2: Scoring and Basic Pipeline**

- Individual scorers: `RecencyScorer`, `PriorityScorer`, `KindScorer`, `TagScorer`, `FrequencyScorer`, `ReflexiveScorer`. Each scorer tested in isolation with ordinal relationship assertions. Pure functions, no internal mutable state.
- `CompositeScorer` with weight normalization and clamping. `ScaledScorer` wrapper.
- `DefaultClassifier`.
- `GreedySlicer` (default). `UShapedPlacer` (default).
- `PipelineExecutor` (internal), `CupelPipelineBuilder`, `CupelPipeline` facade.
- `ContextResult.Apply()` (sync) and `ApplyAsync()` (materializes `IContextSource`, then calls sync).
- Full pipeline integration tests: realistic item sets, pinned item behavior, empty input edge cases, zero budget, all-pinned scenarios.
- Benchmark validation: full pipeline with 100/250/500 items, with/without tracing. Assert zero Gen0 for tracing-disabled path.

**Phase 3: Advanced Optimization and Explainability**

- `KnapsackSlicer` with token discretization (100-token buckets). Document the greedy-vs-optimal trade-off explicitly.
- `QuotaSlicer` with `QuotaSpec` (Require/Cap by Kind percentage).
- `StreamSlicer` for unbounded `IAsyncEnumerable` sources.
- `SelectionReport` and `DryRun()` capability. `ExclusionReason` enum fully populated.
- `CupelPolicy` as declarative, serializable config. `CupelPolicies` named presets: `ChatSession()`, `CodeReview()`, `AgentLoop()`, `RagRetrieval()`. Mark with `[Experimental]`.
- Overflow behavior: `OverflowStrategy.Throw`, `Truncate`, `Proceed` with observer callback.

**Phase 4: Companion Packages**

- `Wollax.Cupel.Extensions.DependencyInjection`: `AddCupel()`, `CupelOptions`, `IOptions<T>`, keyed services for named pipelines, `DiagnosticSource` bridge. Lifetime correctness: transient pipeline/trace, singleton scorers/slicers/placers.
- `Wollax.Cupel.Tiktoken`: `TiktokenCountProvider` bridging `Microsoft.ML.Tokenizers`. Test `Microsoft.ML.Tokenizers` on .NET 10 forward compat early.
- `Wollax.Cupel.Json`: `PolicySerializer`/`PolicyLoader`, `CupelJsonContext` source-generated `JsonSerializerContext`, polymorphic `$type` discriminators for scorer/slicer/placer configs. Document AOT support as v1-unsupported, provide smoke test in trimmed app.

**Phase 5: Documentation and Release**

- README with quick-start, concept explanations, policy examples.
- XML doc comments on all public interfaces and types.
- Samples projects: basic usage, DI integration.
- `PublicAPI.Shipped.txt` finalized for v1.0.0.
- NuGet Trusted Publishing configured (OIDC, no stored API keys).
- Tag `v1.0.0-alpha.1` to trigger first publish workflow.

---

## Confidence Assessment

| Area | Confidence | Notes |
|------|-----------|-------|
| .NET 10 / C# 14 stack | HIGH | All key features verified against official docs |
| xUnit v3 / Shouldly / Verify | HIGH (xUnit), MEDIUM (Shouldly version) | Verify Shouldly latest version before pinning |
| BenchmarkDotNet configuration | HIGH | Verified, sub-ms config is correct |
| Microsoft.ML.Tokenizers | HIGH | Correct choice; verify .NET 10 forward compat in Phase 4 |
| Fixed sync pipeline architecture | HIGH | Sound reasoning, no counterarguments |
| ArrayPool / Span<T> zero-alloc patterns | HIGH | Verified from .NET runtime docs |
| ITraceCollector custom design | HIGH | Activity/ActivitySource explicitly wrong abstraction |
| Scoring algorithm correctness | HIGH | Greedy as default, knapsack with discretization for optimal |
| UShapedPlacer justification | HIGH | Stanford "Lost in the Middle" paper, multiple 2024-2025 follow-ups |
| Ecosystem gap analysis | HIGH | NuGet search confirms no .NET prior art |
| KnapsackSlice discretization bucket size | MEDIUM | 100-token blocks is reasonable but should be benchmarked |
| Named policy presets design | MEDIUM | Chat/RAG/AgentLoop are intuitive but needs user testing for actual defaults |
| ScoredItem struct vs class performance | MEDIUM | Heuristically correct, but benchmark to confirm for real workloads |
| MinVer versioning | MEDIUM | Community standard, verify latest version and .NET 10 compat |
| STJ polymorphic source gen + AOT | LOW-MEDIUM | Known limitation, documented as v1-unsupported is the right call |

---

## Critical Decisions for Roadmapper

The roadmapper must treat these as **hard constraints**, not options:

1. **Phase 1 must include BenchmarkDotNet and PublicApiAnalyzers.** Deferring either creates technical debt that is expensive to pay later (regressions already shipped, breaking changes already made).

2. **`ContextItem` must be immutable from day one.** `{ get; init; }` on all properties. Changing this after v1 is a source-breaking change.

3. **`GreedySlicer` is the default.** `KnapsackSlicer` is explicitly opt-in. Never reverse this default — greedy is faster, knapsack trades time for optimality, and users should understand the trade-off.

4. **`SelectionReport`/`DryRun()` belongs in Phase 3, not later.** It depends on the full pipeline being assembled, but it is a v1 feature. Moving it to v2 would be a significant regression from the stated differentiators.

5. **Named policies are `[Experimental]` at launch.** This is the correct posture — they represent opinionated defaults that will evolve. The `[Experimental]` attribute gives freedom to iterate without breaking changes.

6. **Companion packages ship together with core, versioned identically.** Never publish core alone. The DI and Tiktoken packages are how most users will actually consume the library.
