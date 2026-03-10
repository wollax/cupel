# Cupel v1.0 Requirements

## Pipeline Engine

- [ ] **PIPE-01**: ContextItem model with Content (non-nullable string), Kind, Tokens, Timestamp, Source, Tags, Priority, Pinned, OriginalTokens, FutureRelevanceHint, and extensible Metadata
- [ ] **PIPE-02**: ContextBudget model with MaxTokens, TargetTokens, ReservedSlots, OutputReserve, EstimationSafetyMarginPercent
- [ ] **PIPE-03**: Fixed pipeline executing Classify → Score → Deduplicate → Slice → Place stages in order
- [ ] **PIPE-04**: Ordinal-only scoring invariant enforced: scorers rank, slicers drop, placers position — no component crosses this boundary
- [ ] **PIPE-05**: Pinned items bypass scoring and enter pipeline at Placer stage

## Scorers

- [ ] **SCORE-01**: IScorer interface with output conventionally 0.0–1.0 (documented, not enforced by type)
- [ ] **SCORE-02**: RecencyScorer — scores items by temporal proximity
- [ ] **SCORE-03**: PriorityScorer — scores items by explicit priority value
- [ ] **SCORE-04**: KindScorer — scores items by content kind (message, document, tool output, etc.)
- [ ] **SCORE-05**: TagScorer — scores items by tag-based categorical boosting
- [ ] **SCORE-06**: FrequencyScorer — scores items by reference frequency as relevance signal
- [ ] **SCORE-07**: ReflexiveScorer — scores items using caller-supplied FutureRelevanceHint
- [ ] **SCORE-08**: CompositeScorer with configurable aggregation (WeightedAverage, nested composites)
- [ ] **SCORE-09**: ScaledScorer wrapper that normalizes arbitrary scorer output to 0–1 range

## Slicers

- [ ] **SLICE-01**: ISlicer interface for budget-constrained item selection
- [ ] **SLICE-02**: GreedySlice — O(N log N) greedy fill by score/token ratio
- [ ] **SLICE-03**: KnapsackSlice — 0-1 DP knapsack with token discretization for optimal budget utilization
- [ ] **SLICE-04**: QuotaSlice — percentage-based semantic quotas with Require(Kind, minPercent) / Cap(Kind, maxPercent)
- [ ] **SLICE-05**: StreamSlice — online/streaming selection for IAsyncEnumerable sources
- [ ] **SLICE-06**: Pinned item + quota interaction is specified behavior with clear errors on conflict

## Placement

- [ ] **PLACE-01**: IPlacer interface (pluggable, not hardcoded)
- [ ] **PLACE-02**: UShapedPlacer as default implementation (primacy + recency attention curve)
- [ ] **PLACE-03**: ChronologicalPlacer as alternative implementation (timestamp ordering)

## Explainability

- [ ] **TRACE-01**: ContextResult return type from day 1 containing Items and optional ContextTrace
- [ ] **TRACE-02**: ITraceCollector with NullTraceCollector (no-op default) and DiagnosticTraceCollector
- [ ] **TRACE-03**: Trace event construction gated (IsEnabled check before allocation)
- [ ] **TRACE-04**: Explicit trace propagation (no AsyncLocal)
- [ ] **TRACE-05**: SelectionReport / DryRun() returning included items with scores, excluded items with ExclusionReason enum
- [ ] **TRACE-06**: OverflowStrategy enum (Throw | Truncate | Proceed) with optional observer callback

## API Surface

- [ ] **API-01**: CupelPolicy as declarative, serializable config tying pipeline together
- [ ] **API-02**: Fluent builder via CupelPipeline.CreateBuilder() over fixed pipeline (no call-next middleware)
- [ ] **API-03**: Both explicit policy and intent-based lookup via CupelOptions.AddPolicy("intent", policy)
- [ ] **API-04**: IContextSource interface (IAsyncEnumerable<ContextItem>) in core
- [ ] **API-05**: Token counting is caller's responsibility — ContextItem.Tokens is required non-nullable int

## Named Policies

- [ ] **POLICY-01**: 7+ built-in policies: chat, code-review, rag, document-qa, tool-use, long-running, debugging
- [ ] **POLICY-02**: [Experimental] attribute on preset methods
- [ ] **POLICY-03**: Policy presets serve as living documentation and test fixtures

## Serialization

- [ ] **JSON-01**: [JsonPropertyName] on all public types from day 1
- [ ] **JSON-02**: Incremental serialization: ContextBudget + SlicerConfig first, scorer config after CompositeScorer stabilizes
- [ ] **JSON-03**: RegisterScorer(string name, Func<IScorer> factory) hook for future serialization extensibility
- [ ] **JSON-04**: JSON only (no YAML — minimal dependencies)

## Packaging

- [ ] **PKG-01**: Wollax.Cupel — core library with zero external dependencies beyond BCL
- [ ] **PKG-02**: Wollax.Cupel.Extensions.DependencyInjection — Microsoft.Extensions.DI integration (separate package)
- [ ] **PKG-03**: Wollax.Cupel.Tiktoken — optional token counting companion using Microsoft.ML.Tokenizers
- [ ] **PKG-04**: Wollax.Cupel.Json — JSON policy serialization companion with source-generated JsonSerializerContext
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
| *(populated after roadmap creation)* | | |
