# Cupel

## What This Is

Cupel is a .NET context management library for coding agents. Given a set of context items (messages, documents, tool outputs, memory) and a token budget, Cupel determines the optimal context window — maximizing information density while respecting the attention mechanics of any LLM. It serves both autonomous agent orchestration (Smelt spawning subagents) and interactive human-agent sessions.

Part of the Wollax agentic development stack: **Assay** (spec-driven development) → **Smelt** (orchestration) → **Cupel** (context management).

## Core Value

Given candidates and a budget, return the optimal context selection with full explainability — every inclusion and exclusion has a traceable reason.

## Requirements

### Validated

(None yet — ship to validate)

### Active

**Pipeline Engine**
- [ ] ContextItem model with Content (non-nullable string), Kind, Tokens, Timestamp, Source, Tags, Priority, Pinned, OriginalTokens, FutureRelevanceHint, and extensible Metadata
- [ ] ContextBudget model with MaxTokens, TargetTokens, ReservedSlots, OutputReserve, EstimationSafetyMarginPercent
- [ ] Fixed pipeline: Classify → Score → Deduplicate → Slice → Place
- [ ] Ordinal-only scoring invariant: scorers rank, slicers drop, placers position — no component crosses this boundary
- [ ] Pinned items bypass scoring, enter pipeline at Placer stage

**Scorers**
- [ ] IScorer interface (output conventionally 0.0–1.0, documented not enforced by type)
- [ ] Built-in scorers: RecencyScorer, PriorityScorer, KindScorer, TagScorer, FrequencyScorer, ReflexiveScorer (FutureRelevanceHint)
- [ ] CompositeScorer with configurable aggregation (WeightedAverage, nested composites)
- [ ] ScaledScorer wrapper for scorers that don't naturally produce 0–1 output

**Slicers**
- [ ] ISlicer interface
- [ ] Built-in slicers: GreedySlice, KnapsackSlice, QuotaSlice, StreamSlice
- [ ] Semantic quotas: percentage-based Require(Kind, minPercent) / Cap(Kind, maxPercent)
- [ ] Pinned item + quota interaction is specified behavior with clear errors on conflict

**Placement**
- [ ] IPlacer interface (pluggable, not hardcoded)
- [ ] UShapedPlacer as default implementation (primacy + recency attention curve)

**Explainability**
- [ ] ContextResult return type from day 1: Items + optional ContextTrace
- [ ] ITraceCollector with NullTraceCollector (no-op default) and DiagnosticTraceCollector
- [ ] Trace event construction gated (IsEnabled check before allocation)
- [ ] Explicit trace propagation (no AsyncLocal)
- [ ] SelectionReport / DryRun() — included items with scores, excluded items with ExclusionReason enum
- [ ] OverflowStrategy enum (Throw | Truncate | Proceed) + optional observer callback

**API Surface**
- [ ] CupelPolicy: declarative, serializable config tying pipeline together
- [ ] Fluent builder: CupelPipeline.CreateBuilder() over fixed pipeline (no call-next middleware)
- [ ] Both explicit policy and intent-based lookup (CupelOptions.AddPolicy("intent", policy))
- [ ] IContextSource interface (IAsyncEnumerable<ContextItem>) in core
- [ ] Token counting is caller's responsibility: ContextItem.Tokens is required non-nullable int

**Named Policies**
- [ ] 7+ built-in policies: chat, code-review, rag, document-qa, tool-use, long-running, debugging
- [ ] [Experimental] attribute on preset methods
- [ ] Policy presets serve as living documentation and test fixtures

**Serialization**
- [ ] [JsonPropertyName] on all public types from day 1
- [ ] Incremental serialization: ContextBudget + SlicerConfig first, scorer config after CompositeScorer stabilizes
- [ ] RegisterScorer(string name, Func<IScorer> factory) hook designed in v1 for future serialization
- [ ] JSON only (no YAML — minimal dependencies)

**Packaging**
- [ ] Wollax.Cupel — core library, zero external dependencies
- [ ] Wollax.Cupel.Extensions.DependencyInjection — Microsoft.Extensions.DI integration (separate package)
- [ ] Wollax.Cupel.Tiktoken — optional token counting companion
- [ ] Wollax.Cupel.Json — JSON policy serialization companion
- [ ] Published to nuget.org as public packages

### Out of Scope

- **Storage / persistence** — Cupel does not manage conversation history, vector stores, or caches. Storage is the caller's problem.
- **Retrieval / RAG** — Cupel does not fetch documents from external sources; it scores what you give it.
- **Tokenizer in core** — Cupel accepts pre-counted token lengths. Optional companion packages provide tokenizer support.
- **LLM API integration** — Cupel does not call models; it prepares context that you send to models.
- **Embedding / semantic search** — Available as optional scorer plugin, never required.
- **Compression / summarization** — Cupel scores pre-compressed items via OriginalTokens metadata. Actual compression is the caller's responsibility.
- **LLM-specific adapters in Cupel** — No Cupel.Adapters.Anthropic/OpenAI packages. Adapters are owned by consumers (Smelt, user code).
- **IContextSink** — Cupel selects context; output conversion is the consumer's responsibility.
- **Scorer DAG execution engine** — CompositeScorer with nesting achieves the same result. Revisit if demand materializes.
- **Hot reload / PolicyWatcher** — Complex threading concerns. Phase 3+ at earliest.
- **YAML policy serialization** — Contradicts minimal-dependencies constraint.
- **AdaptiveScorer / ML-based scoring** — Gradient-boosted on small N is worse than tuned heuristics. Named policies + tuning guide instead.
- **Cross-language SDK / CLI** — Document algorithm as spec in README. CLI when demand evidence exists.

## Context

**Ecosystem position**: Third tool in the Wollax agentic development stack. Assay (specs) and Smelt (orchestration) are existing public GitHub repos. Cupel completes the trilogy — the metallurgical assaying metaphor (test the ore → extract the metal → refine the output).

**Problem**: Context windows are finite, degrading resources. LLM performance degrades before theoretical limits (Anthropic data: 17-point MRCR drop at 1M context). "Lost in the middle" phenomenon means placement matters. Tool outputs consume 80%+ of tokens in typical agent trajectories. Multi-agent orchestration multiplies the problem. No standalone, framework-agnostic layer treats context selection as a policy/optimization problem.

**Prior art**: Kata Context (product-form, not library), LangChain context_engineering (Python/LangGraph locked), Cursor workspace indexer (closed-source, IDE-locked), Claude Code compaction (model-specific, opaque), manual prompt engineering (doesn't scale).

**Design philosophy**: Heuristics over magic. No hidden ML models. Transparent, configurable, inspectable scoring. Composable pipeline stages. Every item in the returned window carries its score and inclusion/exclusion reason.

**Performance target**: Sub-millisecond overhead for typical workloads (<500 items). Context management must never be the bottleneck.

**Integration model**: Standalone first. Smelt integration comes later at the call site, not in Cupel's library code. Cupel knows nothing about Assay or Smelt.

## Constraints

- **Tech stack**: C# / .NET 10. Core library has zero external dependencies beyond BCL.
- **Performance**: Full pipeline < 1ms for < 500 items. No allocations on hot paths when tracing disabled.
- **API stability**: [JsonPropertyName] on all public types from day 1. ContextResult return type from day 1. Breaking changes only before v1.0.
- **Dependencies**: Core package must remain zero-dependency. Optional features via companion NuGet packages.
- **Distribution**: Public nuget.org. Semantic versioning. Open source.

## Key Decisions

| Decision | Rationale | Outcome |
| --- | --- | --- |
| Content is non-nullable string on ContextItem | Simplifies API, maximizes debuggability for dry-run/trace. Memory is caller's problem at <500 items. | — Pending |
| Token counting is caller's responsibility | Keeps pipeline dependency-free. Works with any tokenizer (tiktoken, cl100k, Llama encoders) at zero coupling cost. | — Pending |
| CompositeScorer over scorer DAG | Nested composites achieve any DAG-like composition without cycle detection, topological sort, or parallel scheduling overhead. ~30 lines vs hundreds. | — Pending |
| Fixed pipeline over middleware | Call-next middleware silent-drop failure mode is worse than fixed pipeline's predictable behavior. Users substitute implementations, not reorder stages. | — Pending |
| Ordinal-only scoring invariant | Scorers that eliminate items bias the Slicer's input. KnapsackSlice is provably correct only on complete candidate sets. | — Pending |
| IPlacer interface (not hardcoded U-shape) | U-shaped attention curve is model-dependent and actively contested. Every other component is composable — Placer should be too. | — Pending |
| Both explicit policy and intent-based lookup | Explicit for orchestrator-level control (Smelt). Intent-based for quick integration and adoption. | — Pending |
| Separate DI package | Keeps core zero-dependency. Wollax.Cupel.Extensions.DependencyInjection for MS.Extensions.DI users. | — Pending |
| Public nuget.org from day 1 | Forces API discipline. Assay is already public. No proprietary logic to protect — value is in design quality. | — Pending |
| No IContextSink | Cupel selects; consumers convert. Output adapters are scope creep. | — Pending |

---
*Last updated: 2026-03-10 after initialization*
