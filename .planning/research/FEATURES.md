# Cupel Features Research

**Date**: 2026-03-10
**Dimension**: Features — Context management library ecosystem
**Confidence methodology**: HIGH (Context7/official docs), MEDIUM (WebSearch + official source), LOW (WebSearch only/unverified)

---

## 1. Ecosystem Survey: What Exists Today

### 1.1 LangChain Context Engineering (Python/TypeScript)

**Confidence**: HIGH (Context7 + official docs)

LangChain organizes context management into four strategy buckets: **write, select, compress, isolate**.

**What they offer:**
- `trim_messages()` — truncates message history by token count with configurable strategy (`"last"`, keep N recent). Accepts a `token_counter` function and `max_tokens` threshold. Can enforce boundary constraints (`start_on="human"`, `end_on=("human", "tool")`).
- `SummarizationMiddleware` — triggers LLM-based summarization when conversations exceed a token threshold (e.g., `trigger={"tokens": 4000}`), permanently replaces older messages with summaries while keeping recent N messages intact.
- `createMiddleware` with `beforeModel` / `afterModel` hooks — intercepts context before model invocation. Middleware can remove, replace, or augment messages.
- No scoring system. No ranking. No placement optimization. Selection is recency-based or role-based filtering only.
- Deeply coupled to LangGraph's `StateGraph` and `MessagesState` — not standalone.

**Key gap Cupel exploits**: LangChain treats context as messages to trim, not as items to score and optimize. There is no concept of value-weighted selection, no budget optimization, no explainability for why items were included or excluded.

### 1.2 Microsoft Semantic Kernel (.NET/Python)

**Confidence**: HIGH (Context7)

- `ChatHistoryReducer` — reduces chat history for token management. Implementation details are sparse; appears to be a simple truncation/summarization pass.
- `FunctionAdvertisementFilter` — intercepts function/tool advertisement at prompt invocation time, can vectorize available functions and chat history, then select most relevant functions to expose to the model. This is function-level context selection, not content-level.
- Context-based function selection uses vector similarity between chat history and function descriptions.
- No standalone context item scoring, budget optimization, or placement strategy.
- Tightly coupled to the Semantic Kernel pipeline and `IChatCompletionService`.

**Key gap Cupel exploits**: Semantic Kernel's context management is incidental to its chat completion pipeline. There is no first-class context selection layer. Function selection is interesting but narrow — it selects tools, not content.

### 1.3 LlamaIndex (Python)

**Confidence**: HIGH (Context7)

LlamaIndex has the most sophisticated scoring via its **Node Postprocessor** system:
- `SimilarityPostprocessor` — filters nodes below a similarity score cutoff
- `CohereRerank` — reranks nodes using Cohere's trained reranking model
- `ColbertRerank` — fine-grained token-level similarity via ColBERT v2
- `RankGPTRerank` — uses an LLM agent to rerank documents by relevance
- `LLMRerank` — LLM-based batch reranking with parsed relevance scores
- `RankLLMRerank` — uses RankZephyr or similar models for reranking
- `TimeWeightedPostprocessor` — temporal decay scoring

**Scoring model**: Nodes carry a `score` field. Postprocessors reorder and filter. Multiple postprocessors can be chained. The pattern is retrieve-then-rerank.

**Key gap Cupel exploits**: LlamaIndex postprocessors are RAG-specific (query-node similarity). They don't handle multi-kind context (messages + documents + tool outputs + memory), don't optimize for token budgets as a constraint, and have no placement strategy. No explainability for exclusion reasons.

### 1.4 Factory.ai / Cursor / Claude Code (Closed-source)

**Confidence**: MEDIUM (blog posts, no source code)

- **Factory.ai**: Layered sequential filtering — repository overviews first, then semantic search, then targeted file operations. Explicit budget awareness ("every additional token processed incurs a direct cost"). No published scoring details.
- **Cursor**: Workspace indexer with semantic search. Closed-source, IDE-locked.
- **Claude Code**: "auto-compact" at 95% context capacity. LLM-based summarization. Opaque — no user control over what gets kept or dropped.

### 1.5 Letta/MemGPT

**Confidence**: MEDIUM (blog post)

Letta frames context as an **LLM Operating System** with kernel/user space:
- **Kernel context**: System prompt, tool schemas, memory blocks (with size limits and access controls), files, system metadata
- **User context**: Message buffer, tool call history, externally retrieved context
- Memory blocks are reserved portions of the context window with size limits and metadata labels
- System tools provide privileged access to modify agent state internals (memory replace/append/rethink)

This is an architectural pattern (how to structure context), not an optimization library (how to select context given a budget).

### 1.6 JetBrains Research (2025)

**Confidence**: HIGH (peer-reviewed research)

Two primary context management techniques evaluated on SWE-agent and OpenHands:
- **Observation masking**: Preserves agent reasoning/actions, replaces older environment observations with placeholders. Rolling window approach. 2.6% solve rate improvement, 52% cheaper.
- **LLM summarization**: Separate model compresses older interactions. Causes trajectory elongation (13-15% longer runs), adds 7%+ cost per instance from summary API calls.

Key finding: Observation masking outperforms LLM summarization in efficiency and reliability. Hybrid approaches (masking primary, selective summarization) recommended.

### 1.7 No .NET Prior Art

**Confidence**: HIGH (NuGet search)

There is no standalone .NET context management library on NuGet. The closest are:
- `ModelContextProtocol` — MCP protocol implementation, not context management
- `Microsoft.Extensions.AI` — LLM integration abstraction, no context selection
- `DotnetPrompt` — prompt template library, no budget optimization

Cupel would be the first dedicated context management library in the .NET ecosystem.

---

## 2. Scoring and Ranking Approaches

### 2.1 Approaches Found in the Wild

| Approach | Used By | Complexity | Notes |
|---|---|---|---|
| Recency (keep last N) | LangChain, most chat apps | Trivial | Default everywhere. Necessary but insufficient alone. |
| Similarity score cutoff | LlamaIndex | Low | Threshold-based. Binary keep/drop. |
| Semantic similarity (embeddings) | LlamaIndex, Factory.ai, SK | Medium | Requires embedding model. Query-dependent. |
| LLM-based reranking | LlamaIndex (RankGPT, LLMRerank) | High | Expensive, non-deterministic. Best quality for RAG. |
| Trained reranker models | LlamaIndex (Cohere, ColBERT) | Medium | External dependency. Domain-specific quality varies. |
| Temporal decay | LlamaIndex, general practice | Low | Score decreases with age. Simple exponential or linear. |
| Role/kind filtering | LangChain, general practice | Low | Include/exclude by message type. |
| Priority/pinning | General practice | Low | Manual override. Essential for system messages. |

### 2.2 What's Missing (Cupel's Opportunity)

No existing solution offers **composite scoring** — combining multiple signals (recency + priority + kind + frequency + caller hints) into a single ordinal ranking that a slicer can optimize against. Everyone either uses a single signal or chains independent postprocessors without a unified score.

**Cupel's planned scorers map well to the ecosystem gaps:**
- `RecencyScorer` — table stakes, everyone does this
- `PriorityScorer` — table stakes, but Cupel's explicit priority field is cleaner than ad-hoc metadata
- `KindScorer` — differentiator: no one scores by content kind (message vs document vs tool output)
- `TagScorer` — differentiator: flexible categorical boosting
- `FrequencyScorer` — differentiator: reference frequency as relevance signal
- `ReflexiveScorer` (FutureRelevanceHint) — differentiator: forward-looking caller hint, unique to Cupel
- `CompositeScorer` — strong differentiator: unified multi-signal scoring with weighted aggregation

### 2.3 Confidence Assessment

The planned scorer suite covers the practical signals well. The one signal Cupel deliberately excludes — semantic similarity via embeddings — is the right call for core (requires embedding model dependency). The `IScorer` interface allows consumers to add embedding-based scorers without polluting core.

---

## 3. Placement Strategies

### 3.1 U-Shaped Attention Curve (Default)

**Confidence**: HIGH (Stanford "Lost in the Middle" paper, multiple follow-ups)

The "lost in the middle" phenomenon is well-established: LLMs perform best on information at the beginning (primacy) and end (recency) of context, with significant degradation for middle content. This is not a bug but an emergent property of how autoregressive models optimize for information retrieval demands during pre-training.

Key findings from 2024-2025 research:
- Training on free recall yields primacy, running span yields recency, joint training produces U-shape
- The effect is model-dependent — newer models with improved training show less pronounced U-shapes
- Scaling attention weights between initial token and others can improve middle-context use by up to 3.6%

**Implication for Cupel**: UShapedPlacer as default is well-justified by research. But the effect varies by model, which validates IPlacer being pluggable.

### 3.2 Alternative Placement Strategies

| Strategy | Description | Complexity | Confidence |
|---|---|---|---|
| Chronological | Items ordered by timestamp. Simple, predictable. | Trivial | HIGH |
| Reverse chronological | Most recent first. Common in chat. | Trivial | HIGH |
| Relevance-ordered | Highest-scored items first (at beginning for primacy). | Low | MEDIUM |
| Semantic grouping | Related items placed adjacent to each other. | Medium | MEDIUM |
| Priority-stratified | High-priority at edges, low-priority in middle. | Low | HIGH |
| Interleaved | Alternate between content types for attention diversity. | Medium | LOW |

### 3.3 Research-Backed Mitigations

- **Multi-Scale Positional Encoding (Ms-PoE)**: Model-level fix, not relevant to Cupel (we work above the model).
- **Focus Directions / Contextual Heads**: Model-internal attention mechanism. Not applicable.
- **Strategic reranking before placement**: Place most relevant at beginning and end. This IS what UShapedPlacer does.

**Assessment**: UShapedPlacer as default + IPlacer for alternatives is the right architecture. Cupel should ship with UShapedPlacer and ChronologicalPlacer. Additional placers are easy for consumers to implement.

---

## 4. Knapsack Problem Approaches for Token Budget Optimization

### 4.1 The Formal Mapping

**Confidence**: HIGH (well-established CS + confirmed by Welihinda's analysis)

Context selection maps to the **0-1 Knapsack Problem**:
- **Items** = context items (messages, documents, tool outputs)
- **Weight** w[i] = `ContextItem.Tokens` (token cost)
- **Value** v[i] = composite score from scorers
- **Capacity** W = `ContextBudget.TargetTokens` (available budget after reserves)

Budget calculation: `W = MaxTokens - OutputReserve - ReservedSlots - (MaxTokens * SafetyMarginPercent)`

### 4.2 Algorithm Options

| Algorithm | Time Complexity | Optimality | Practical Notes |
|---|---|---|---|
| **Greedy (value/weight ratio)** | O(N log N) | Approximate | Best for <500 items. Sorts by score/token ratio, fills greedily. Fast. |
| **Dynamic Programming** | O(N * W) | Optimal | W is token count (potentially thousands). Memory-intensive. Impractical for large budgets. |
| **Branch and Bound** | Exponential worst case | Optimal | Practical only for very small N. |
| **FPTAS** | O(N / epsilon) | (1-epsilon)-approximate | Theoretical interest. Over-engineered for <500 items. |

### 4.3 Practical Recommendation

For Cupel's target workload (<500 items, sub-millisecond budget):

- **GreedySlice**: Sort by score/token ratio, fill until budget exhausted. O(N log N). This is what most practitioners use. Correct default.
- **KnapsackSlice**: 0-1 DP knapsack. Optimal but O(N * W) where W is in tokens. For W=100K tokens this is impractical. **Must discretize**: bucket tokens into coarser units (e.g., 100-token blocks) to make W manageable. With W'=1000 and N=500, DP table is 500K entries — feasible.
- **QuotaSlice**: Not a knapsack variant. Enforces kind-level constraints (min/max percentage by Kind). This is a **constrained selection** problem — fill quotas first, then optimize remainder. Correct design.
- **StreamSlice**: Online/streaming variant for IAsyncEnumerable sources. Greedy with look-ahead. Necessary for large/unbounded item sets.

### 4.4 Interaction with Pinned Items

Pinned items have infinite value (must be included). They consume budget before optimization runs. If pinned items exceed budget, OverflowStrategy determines behavior. This is standard practice in constrained optimization — fix mandatory assignments, optimize the remainder.

---

## 5. Explainability and Traceability

### 5.1 What Exists Today

**Confidence**: MEDIUM (industry blogs, no formal standards)

| Solution | Explainability Level | Details |
|---|---|---|
| LangChain | None | No explanation for what was trimmed or why. Trimming is silent. |
| Semantic Kernel | None | ChatHistoryReducer provides no trace. |
| LlamaIndex | Minimal | Nodes carry scores but no exclusion reasons. |
| Factory.ai | None visible | Blog mentions "proactively surfacing relevant knowledge" but no inspection mechanism. |
| Claude Code | None | Compaction is opaque to the user. |
| LangSmith (observability) | External | Traces LLM calls and tool invocations, but context selection decisions are not traced as first-class events. |

### 5.2 What the Industry Wants

Per JetBrains and industry blogs: "Nearly 65% of enterprise AI failures in 2025 were attributed to context drift or memory loss during multi-step reasoning." Debugging these failures requires knowing what was in the context window and why.

The emerging pattern is **named, ordered processors** rather than ad-hoc string concatenation — making the compilation step observable and testable.

### 5.3 Cupel's Explainability Design (Assessment)

Cupel's planned explainability is **the strongest in the ecosystem by a wide margin**:
- `ContextResult` with optional `ContextTrace` — structured return type from day 1
- `ITraceCollector` with `NullTraceCollector` (zero-cost default) and `DiagnosticTraceCollector`
- Trace event construction gated (IsEnabled check before allocation) — performance-safe
- `SelectionReport` / `DryRun()` — included items with scores, excluded items with `ExclusionReason` enum
- `OverflowStrategy` enum (Throw | Truncate | Proceed) with optional observer callback
- No AsyncLocal — explicit trace propagation

This is a genuine differentiator. No existing solution provides exclusion reasons, score breakdowns, or dry-run capability.

---

## 6. Feature Categorization

### Table Stakes (Must Have)

These features are expected behavior. Shipping without them would make Cupel incomplete.

| Feature | Complexity | Dependencies | Rationale |
|---|---|---|---|
| **ContextItem model** with Content, Kind, Tokens, Timestamp, Priority, Tags, Pinned | S | None | The fundamental data unit. Every solution has one. |
| **ContextBudget model** with MaxTokens, TargetTokens, OutputReserve, SafetyMargin | S | None | Budget definition. Without it, there's no optimization problem. |
| **Fixed pipeline** (Classify, Score, Deduplicate, Slice, Place) | M | ContextItem, ContextBudget | The execution model. Must be predictable and inspectable. |
| **IScorer interface** + RecencyScorer + PriorityScorer | S | ContextItem | Minimum viable scoring. Recency and priority are universal. |
| **ISlicer interface** + GreedySlice | S | IScorer output | Greedy fill is the baseline algorithm everyone expects. |
| **IPlacer interface** + at least one implementation | S | ISlicer output | Items must be ordered for the output window. |
| **Pinned items** bypassing scoring | S | ContextItem.Pinned | System messages must be non-negotiable. Universal expectation. |
| **Token counting as caller responsibility** | Design decision | None | Keeps core zero-dependency. Standard in .NET ecosystem. |
| **ContextResult return type** | S | Pipeline | The output contract. Must be stable from day 1. |
| **Zero external dependencies in core** | Constraint | None | .NET convention for foundational libraries. |

### Differentiators (Competitive Advantage)

These features make Cupel worth choosing over ad-hoc solutions or adapting Python libraries.

| Feature | Complexity | Dependencies | Rationale |
|---|---|---|---|
| **CompositeScorer** with weighted aggregation | M | IScorer | No existing solution combines multiple scoring signals into a unified rank. This is the core innovation. |
| **ScaledScorer** wrapper | S | IScorer | Normalizes arbitrary scorer outputs to 0-1 range. Enables composition. |
| **KnapsackSlice** (0-1 DP with discretization) | M | IScorer, ContextBudget | Optimal budget utilization. Greedy leaves value on the table. No one else offers this. |
| **QuotaSlice** with semantic quotas | M | ISlicer, ContextItem.Kind | Percentage-based kind constraints (Require/Cap). Unique to Cupel. |
| **UShapedPlacer** | S | IPlacer | Research-backed placement. No existing library implements this. |
| **SelectionReport / DryRun()** | M | Pipeline, ContextResult | Exclusion reasons for every item. Zero competitors offer this. Killer feature. |
| **ContextTrace** with gated construction | M | ITraceCollector | Full pipeline observability with zero overhead when disabled. |
| **ExclusionReason enum** | S | SelectionReport | Structured reason codes (BudgetExceeded, DedupRemoved, QuotaCapped, etc.) |
| **OverflowStrategy** (Throw/Truncate/Proceed) | S | ContextBudget | Explicit behavior when pinned items exceed budget. No one else handles this explicitly. |
| **Named policy presets** (chat, code-review, rag, etc.) | M | All scorers, slicers, placers | Adoption lever. Living documentation. Test fixtures. |
| **CupelPolicy** as declarative, serializable config | M | Pipeline, all components | Policies as data, not just code. Enables tooling, sharing, versioning. |
| **Fluent builder** API | M | CupelPolicy, Pipeline | Developer experience. Discoverability. |
| **Ordinal-only scoring invariant** | Design decision | IScorer, ISlicer, IPlacer | Correctness guarantee. Scorers rank, slicers drop, placers position. No component crosses boundaries. |
| **KindScorer, TagScorer, FrequencyScorer** | S each | IScorer | Multi-dimensional scoring signals beyond recency/priority. |
| **ReflexiveScorer** (FutureRelevanceHint) | S | IScorer, ContextItem | Forward-looking relevance signal from caller. Novel. |
| **OriginalTokens metadata** | S | ContextItem | Density-aware scoring for pre-compressed items. |
| **First .NET context management library** | N/A | N/A | No competition in the ecosystem. Blue ocean. |

### Anti-Features (Deliberately Not Built)

| Anti-Feature | Rationale |
|---|---|
| **Built-in embedding/semantic similarity scorer** | Requires embedding model dependency. Violates zero-dependency core. Available via IScorer plugin. |
| **LLM-based reranking** | Non-deterministic, expensive, latency-destroying. Contradicts sub-millisecond target. Consumer can implement via IScorer. |
| **Compression/summarization** | Requires LLM call. Cupel scores pre-compressed items via OriginalTokens. Compression is caller's responsibility. |
| **Storage/persistence** | Cupel is stateless. No conversation history, vector stores, or caches. Storage is caller's problem. |
| **Retrieval/RAG** | Cupel scores what you give it. It does not fetch documents from external sources. |
| **LLM API integration** | Cupel prepares context. It does not call models. |
| **Middleware/call-next pipeline** | Silent-drop failure mode. Fixed pipeline with substitutable implementations is safer and more predictable. |
| **AdaptiveScorer / ML-based scoring** | Gradient-boosted on small N (<500 items) is worse than tuned heuristics. Over-engineered. |
| **YAML serialization** | Additional dependency. JSON is sufficient. Minimal-dependencies constraint. |
| **Hot reload / PolicyWatcher** | Complex threading. Not needed for v1. |
| **IContextSink (output conversion)** | Scope creep. Cupel selects; consumers convert to their model's format. |
| **Cross-language SDK** | Document the algorithm as a spec. Ship CLI only when demand evidence exists. |
| **Scorer DAG execution engine** | CompositeScorer with nesting achieves the same composition without cycle detection, topological sort, or parallel scheduling. ~30 lines vs hundreds. |

---

## 7. Feature Dependencies

```
ContextItem ──────────────────────────────────┐
ContextBudget ────────────────────────────────┤
                                              v
IScorer ─── RecencyScorer ──────────────> CompositeScorer
        ├── PriorityScorer ──────────────>     |
        ├── KindScorer ──────────────────>     |
        ├── TagScorer ────────────────────>     |
        ├── FrequencyScorer ──────────────>     |
        └── ReflexiveScorer ──────────────>     |
                                              |
ScaledScorer (wraps any IScorer) ────────>     |
                                              v
ISlicer ─── GreedySlice ─────────────────> Pipeline
        ├── KnapsackSlice ───────────────>     |
        ├── QuotaSlice (semantic quotas) ─>     |
        └── StreamSlice ─────────────────>     |
                                              v
IPlacer ─── UShapedPlacer ───────────────> ContextResult
        └── (ChronologicalPlacer) ────────>     |
                                              v
ITraceCollector ── NullTraceCollector ───> ContextTrace
               └── DiagnosticTraceCollector    |
                                              v
SelectionReport / DryRun() ──────────────> ExclusionReason
OverflowStrategy ────────────────────────> ContextBudget
                                              |
CupelPolicy (declarative config) ─────────> all above
Fluent builder ────────────────────────────> CupelPolicy
Named policies ────────────────────────────> Fluent builder
IContextSource ────────────────────────────> ContextItem (async feed)
[JsonPropertyName] ────────────────────────> all public types (concurrent)
```

### Critical Path

1. ContextItem + ContextBudget (everything depends on these)
2. IScorer + at least RecencyScorer + PriorityScorer (slicers need scores)
3. ISlicer + GreedySlice (pipeline needs a slicer)
4. IPlacer + UShapedPlacer (pipeline needs a placer)
5. Pipeline + ContextResult (the execution engine)
6. ITraceCollector + ContextTrace (explainability layer)
7. CompositeScorer + ScaledScorer (composition)
8. KnapsackSlice + QuotaSlice (advanced optimization)
9. SelectionReport / DryRun (explainability surface)
10. OverflowStrategy (error handling)
11. Fluent builder + CupelPolicy (developer experience)
12. Named policies (adoption lever, depends on everything above)
13. Serialization ([JsonPropertyName] concurrent with type definition)
14. IContextSource (async item feed, orthogonal to pipeline)

---

## 8. Key Findings and Recommendations

### The Ecosystem Gap is Real

No existing solution treats context selection as a **standalone optimization problem** with:
- Multi-signal composite scoring
- Budget-constrained optimization (knapsack)
- Research-backed placement
- Full explainability (inclusion reasons, exclusion reasons, scores, dry-run)

LangChain trims. LlamaIndex reranks. Semantic Kernel reduces. Nobody optimizes.

### Explainability is the Killer Feature

Every competitor operates as a black box. Context goes in, trimmed context comes out, and nobody can tell you why. Cupel's SelectionReport/DryRun with ExclusionReason enum is genuinely novel and addresses the #1 debugging pain point (65% of enterprise AI failures attributed to context drift/memory loss).

### The .NET Ecosystem is Wide Open

Zero competition. First-mover advantage. The only risk is that Microsoft adds context management to Semantic Kernel — but their architectural coupling to chat completion makes a standalone library unlikely.

### KnapsackSlice Needs Discretization

The 0-1 DP knapsack is O(N * W) where W is token count. For large budgets (100K+ tokens), this is impractical without discretization. Bucket tokens into coarser units (e.g., 100-token blocks). Document this trade-off. GreedySlice should be the default; KnapsackSlice is for when users need provably better optimization and accept the cost.

### Named Policies Are the Adoption Lever

Both the brainstorm tracks and ecosystem analysis converge: pre-configured policies lower the floor for adoption. `ChatSession()`, `CodeReview()`, `AgentLoop()` with `[Experimental]` are how users discover the library and learn its concepts.

### The Anti-Features List is Correct

The out-of-scope decisions align with ecosystem reality. Every solution that tried to be both context manager AND retriever AND compressor ended up coupled to a specific framework. Cupel's discipline here is a feature.
