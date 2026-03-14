# Cupel v1.0 Requirements

## Pipeline Engine

- [x] **PIPE-01**: ContextItem model with Content (non-nullable string), Kind, Tokens, Timestamp, Source, Tags, Priority, Pinned, OriginalTokens, FutureRelevanceHint, and extensible Metadata
- [x] **PIPE-02**: ContextBudget model with MaxTokens, TargetTokens, ReservedSlots, OutputReserve, EstimationSafetyMarginPercent
- [x] **PIPE-03**: Fixed pipeline executing Classify → Score → Deduplicate → Slice → Place stages in order
- [x] **PIPE-04**: Ordinal-only scoring invariant enforced: scorers rank, slicers drop, placers position — no component crosses this boundary
- [x] **PIPE-05**: Pinned items bypass scoring and enter pipeline at Placer stage

## Scorers

- [x] **SCORE-01**: IScorer interface with output conventionally 0.0–1.0 (documented, not enforced by type)
- [x] **SCORE-02**: RecencyScorer — scores items by temporal proximity
- [x] **SCORE-03**: PriorityScorer — scores items by explicit priority value
- [x] **SCORE-04**: KindScorer — scores items by content kind (message, document, tool output, etc.)
- [x] **SCORE-05**: TagScorer — scores items by tag-based categorical boosting
- [x] **SCORE-06**: FrequencyScorer — scores items by reference frequency as relevance signal
- [x] **SCORE-07**: ReflexiveScorer — scores items using caller-supplied FutureRelevanceHint
- [x] **SCORE-08**: CompositeScorer with configurable aggregation (WeightedAverage, nested composites)
- [x] **SCORE-09**: ScaledScorer wrapper that normalizes arbitrary scorer output to 0–1 range

## Slicers

- [x] **SLICE-01**: ISlicer interface for budget-constrained item selection
- [x] **SLICE-02**: GreedySlice — O(N log N) greedy fill by score/token ratio
- [x] **SLICE-03**: KnapsackSlice — 0-1 DP knapsack with token discretization for optimal budget utilization
- [x] **SLICE-04**: QuotaSlice — percentage-based semantic quotas with Require(Kind, minPercent) / Cap(Kind, maxPercent)
- [x] **SLICE-05**: StreamSlice — online/streaming selection for IAsyncEnumerable sources
- [x] **SLICE-06**: Pinned item + quota interaction is specified behavior with clear errors on conflict

## Placement

- [x] **PLACE-01**: IPlacer interface (pluggable, not hardcoded)
- [x] **PLACE-02**: UShapedPlacer as default implementation (primacy + recency attention curve)
- [x] **PLACE-03**: ChronologicalPlacer as alternative implementation (timestamp ordering)

## Explainability

- [x] **TRACE-01**: ContextResult return type from day 1 containing Items and optional ContextTrace
- [x] **TRACE-02**: ITraceCollector with NullTraceCollector (no-op default) and DiagnosticTraceCollector
- [x] **TRACE-03**: Trace event construction gated (IsEnabled check before allocation)
- [x] **TRACE-04**: Explicit trace propagation (no AsyncLocal)
- [x] **TRACE-05**: SelectionReport / DryRun() returning included items with scores, excluded items with ExclusionReason enum
- [x] **TRACE-06**: OverflowStrategy enum (Throw | Truncate | Proceed) with optional observer callback

## API Surface

- [x] **API-01**: CupelPolicy as declarative, serializable config tying pipeline together
- [x] **API-02**: Fluent builder via CupelPipeline.CreateBuilder() over fixed pipeline (no call-next middleware)
- [x] **API-03**: Both explicit policy and intent-based lookup via CupelOptions.AddPolicy("intent", policy)
- [x] **API-04**: IContextSource interface (IAsyncEnumerable<ContextItem>) in core
- [x] **API-05**: Token counting is caller's responsibility — ContextItem.Tokens is required non-nullable int

## Named Policies

- [x] **POLICY-01**: 7+ built-in policies: chat, code-review, rag, document-qa, tool-use, long-running, debugging
- [x] **POLICY-02**: [Experimental] attribute on preset methods
- [x] **POLICY-03**: Policy presets serve as living documentation and test fixtures

## Serialization

- [x] **JSON-01**: [JsonPropertyName] on all public types from day 1
- [x] **JSON-02**: Incremental serialization: ContextBudget + SlicerConfig first, scorer config after CompositeScorer stabilizes
- [x] **JSON-03**: RegisterScorer(string name, Func<IScorer> factory) hook for future serialization extensibility
- [x] **JSON-04**: JSON only (no YAML — minimal dependencies)

## Packaging

- [x] **PKG-01**: Wollax.Cupel — core library with zero external dependencies beyond BCL
- [ ] **PKG-02**: Wollax.Cupel.Extensions.DependencyInjection — Microsoft.Extensions.DI integration (separate package)
- [ ] **PKG-03**: Wollax.Cupel.Tiktoken — optional token counting companion using Microsoft.ML.Tokenizers
- [x] **PKG-04**: Wollax.Cupel.Json — JSON policy serialization companion with source-generated JsonSerializerContext
- [ ] **PKG-05**: Published to nuget.org as public packages

---

## Future Requirements

(Deferred beyond v1.0)

- Embedding-based semantic similarity scorer (requires external dependency)
- LLM-based reranking scorer (non-deterministic, latency-destroying)
- Hot reload / PolicyWatcher (complex threading, Phase 3+)
- Cross-language SDK / CLI (document algorithm as spec first)
- AdaptiveScorer / ML-based scoring (gradient-boosted on small N is worse than tuned heuristics)

## Out of Scope

- **Storage / persistence** — Cupel is stateless. No conversation history, vector stores, or caches.
- **Retrieval / RAG** — Cupel scores what you give it. No external document fetching.
- **Tokenizer in core** — Caller pre-counts tokens. Optional companion package for convenience.
- **LLM API integration** — Cupel prepares context, does not call models.
- **Compression / summarization** — Cupel scores pre-compressed items. Compression is caller's responsibility.
- **IContextSink** — Cupel selects; consumers convert to their format.
- **YAML serialization** — Contradicts minimal-dependencies constraint.
- **Scorer DAG engine** — CompositeScorer with nesting achieves the same result.
- **LLM-specific adapters** — No Cupel.Adapters.Anthropic/OpenAI packages.

## Traceability

| Requirement | Phase | Status |
|---|---|---|
| PIPE-01 | Phase 1: Project Scaffold & Core Models | ● complete |
| PIPE-02 | Phase 1: Project Scaffold & Core Models | ● complete |
| PIPE-03 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| PIPE-04 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| PIPE-05 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| SCORE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| SCORE-02 | Phase 3: Individual Scorers | ● complete |
| SCORE-03 | Phase 3: Individual Scorers | ● complete |
| SCORE-04 | Phase 3: Individual Scorers | ● complete |
| SCORE-05 | Phase 3: Individual Scorers | ● complete |
| SCORE-06 | Phase 3: Individual Scorers | ● complete |
| SCORE-07 | Phase 3: Individual Scorers | ● complete |
| SCORE-08 | Phase 4: Composite Scoring | ● complete |
| SCORE-09 | Phase 4: Composite Scoring | ● complete |
| SLICE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| SLICE-02 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| SLICE-03 | Phase 6: Advanced Slicers & Quota System | ● complete |
| SLICE-04 | Phase 6: Advanced Slicers & Quota System | ● complete |
| SLICE-05 | Phase 6: Advanced Slicers & Quota System | ● complete |
| SLICE-06 | Phase 6: Advanced Slicers & Quota System | ● complete |
| PLACE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| PLACE-02 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| PLACE-03 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| TRACE-01 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-02 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-03 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-04 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| TRACE-05 | Phase 7: Explainability & Overflow Handling | ● complete |
| TRACE-06 | Phase 7: Explainability & Overflow Handling | ● complete |
| API-01 | Phase 8: Policy System & Named Presets | ● complete |
| API-02 | Phase 5: Pipeline Assembly & Basic Execution | ● complete |
| API-03 | Phase 8: Policy System & Named Presets | ● complete |
| API-04 | Phase 2: Interfaces & Diagnostics Infrastructure | ● complete |
| API-05 | Phase 1: Project Scaffold & Core Models | ● complete |
| POLICY-01 | Phase 8: Policy System & Named Presets | ● complete |
| POLICY-02 | Phase 8: Policy System & Named Presets | ● complete |
| POLICY-03 | Phase 8: Policy System & Named Presets | ● complete |
| JSON-01 | Phase 1: Project Scaffold & Core Models | ● complete |
| JSON-02 | Phase 9: Serialization & JSON Package | ● complete |
| JSON-03 | Phase 9: Serialization & JSON Package | ● complete |
| JSON-04 | Phase 9: Serialization & JSON Package | ● complete |
| PKG-01 | Phase 1: Project Scaffold & Core Models | ● complete |
| PKG-02 | Phase 10: Companion Packages & Release | ○ planned |
| PKG-03 | Phase 10: Companion Packages & Release | ○ planned |
| PKG-04 | Phase 9: Serialization & JSON Package | ● complete |
| PKG-05 | Phase 10: Companion Packages & Release | ○ planned |
