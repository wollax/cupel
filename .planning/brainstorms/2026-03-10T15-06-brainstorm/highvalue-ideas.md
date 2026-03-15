# High-Value Architecture & Design Proposals for Cupel

*Explorer: explorer-highvalue | Date: 2026-03-10*

---

## Idea 1: Composable Scorer Graph (DAG over Linear Chain)

**Name**: Scorer DAG

**What**: Replace the linear scorer chain (`RecencyScorer → PriorityScorer → KindScorer → ...`) with a directed acyclic graph of scorer nodes. Nodes have configurable aggregation strategies at merge points: `Sum`, `Max`, `Min`, `WeightedAverage`, `Product`. This enables parallel scoring branches whose results are combined before the next stage. Example: run `RecencyScorer` and `FrequencyScorer` in parallel, aggregate via `WeightedAverage`, then pass that composite score into `PriorityScorer` downstream.

```
RecencyScorer ──┐
                ├─[WeightedAverage]─► PriorityScorer ─► FinalScore
FrequencyScorer─┘
```

**Why**: The linear chain forces an implicit ordering that privileges later scorers. A DAG makes scoring dependencies explicit and composable. Users can encode domain knowledge in the graph topology itself rather than fighting scorer ordering. Enables "use whichever of these signals is stronger" patterns without hacks. The abstraction also unlocks parallel execution of independent scorer branches for performance.

**Scope**: Medium-large. The core change is the execution engine: replace `IEnumerable<IScorer>` with a `ScorerGraph` type. Individual scorers remain unchanged — only the orchestration layer changes. Public API surfaces a fluent graph builder.

**Risks**:
- Graph topology validation (cycle detection) adds complexity and a potential failure mode at configuration time
- Users who only need a simple chain now face a more complex mental model — need to make the simple case still simple (e.g., `ScorerGraph.Linear(...)`)
- Parallel execution introduces non-determinism concerns if scorers have hidden side effects

---

## Idea 2: Policy-as-Code with Full Serialization + Hot Reload

**Name**: CupelPolicy Serialization & GitOps

**What**: Make `CupelPolicy` fully serializable to JSON and YAML, with a published schema. Policies become artifacts that can be version-controlled, diffed, reviewed in PRs, and deployed independently of application code. Support hot-reload via a `PolicyWatcher` that monitors a policy file and applies updates without restarting the application. Include policy versioning (semver-compatible) and a migration/compatibility story for evolving policies.

Example policy file:
```json
{
  "$schema": "https://cupel.dev/schema/policy/v1.json",
  "version": "1.2.0",
  "budget": { "maxTokens": 8000, "outputReserve": 1000 },
  "scorers": [
    { "type": "recency", "weight": 0.4 },
    { "type": "priority", "weight": 0.6 }
  ],
  "slicer": { "type": "knapsack" },
  "quotas": { "system_message": { "min": 0.1 }, "tool_call": { "max": 0.4 } }
}
```

**Why**: Policies are configuration, not code. Keeping them in code means redeploys for every tuning iteration. Serialization enables: A/B testing different policies, per-tenant policy overrides, hot-tuning in production, policy review in CI, and eventual policy marketplaces or sharing. This is the difference between Cupel being a library and Cupel being a platform.

**Scope**: Medium. The data model is mostly already there (CupelPolicy exists as a config object). The work is: JSON/YAML converters for all types, schema generation, a `PolicyLoader` abstraction, and `PolicyWatcher` (optional, additive). The riskiest part is maintaining deserialization compatibility as the policy schema evolves.

**Risks**:
- Schema evolution is permanently hard. v2 policies must be readable by v1 consumers or migration tooling must exist
- Custom scorers (user-defined) can't be serialized without a type registry / discriminated union pattern — need an extension point for custom type names
- YAML is convenient but adds a dependency (System.Text.Yaml or external)

---

## Idea 3: Explainability Trace Layer

**Name**: ContextTrace / Pipeline Diagnostics

**What**: Every pipeline stage emits structured trace events. The output of `CupelPipeline.Run(items, budget)` is not just `IReadOnlyList<ContextItem>` but a `ContextResult` containing both the selected items and a `ContextTrace` — a structured log of every scoring decision, ranking position, include/exclude reason, quota check, and placement decision. Callers can opt into trace capture for debugging/logging without paying the overhead in production (traces are collected lazily or behind a flag).

```csharp
var result = policy.Apply(candidates, budget);
// result.Items — the selected context
// result.Trace — why each item was/wasn't included
// result.Trace.Scorings["item-42"] — per-scorer breakdown
// result.Trace.ExcludedItems — what was cut and why
```

**Why**: "Policy, not storage" is the pitch, but without explainability you get a black box that's hard to tune and impossible to debug. When a user says "why did Cupel drop my system prompt?" they need an answer. Trace data also enables: automated policy quality metrics, test assertions on include/exclude decisions (not just the final list), and observability dashboards. This is the difference between a library people trust and one they fear.

**Scope**: Medium. Requires threading a trace context through the pipeline, which touches every stage. The key insight: make `ITraceCollector` optional with a `NullTraceCollector` default — zero overhead in production. Full trace mode activated by passing a `DiagnosticTraceCollector` to the policy runner.

**Risks**:
- Trace context threading pollutes every interface signature if done naively — should use an ambient context pattern or pass a single collector object rather than threading it through every method
- Trace output can be large for big item sets — need to think about trace summarization vs. full verbosity modes
- Risk of over-indexing on explainability at the expense of the clean API surface

---

## Idea 4: Fluent Pipeline Builder with Middleware Semantics

**Name**: Middleware Pipeline API

**What**: Model the Cupel pipeline as ASP.NET-style middleware, where each stage is a `PipelineMiddleware<TContext>` that can call `next(context)` or short-circuit. Users build pipelines with a fluent builder:

```csharp
var pipeline = CupelPipeline.CreateBuilder()
    .UseClassifier(new KindClassifier())
    .UseScorer(new RecencyScorer(halfLife: TimeSpan.FromHours(1)))
    .UseScorer(new PriorityScorer())
    .UseDeduplicator(new SemanticDeduplicator())
    .UseSlicer(new KnapsackSlicer())
    .UsePlacer(new UShapedPlacer())
    .Build();
```

Each stage receives a `PipelineContext` carrying the current item list + metadata, transforms it, and passes it on. Middleware can be injected, decorated, or replaced. This enables cross-cutting concerns (logging, timing, caching) to be added as middleware without modifying core stages.

**Why**: The middleware pattern is instantly recognizable to .NET developers. It makes the pipeline composition concrete and inspectable, not implicit. Extension is natural — add a custom deduplication pass or a caching layer without touching core code. The builder pattern also enables compile-time pipeline validation (required stages present) vs. runtime discovery of misconfiguration.

**Scope**: Medium. The pipeline execution engine needs refactoring, but the individual stage implementations (scorers, slicers, placer) stay intact. Public API surface is the builder — a clean, additive change.

**Risks**:
- Middleware pipelines can be hard to type-safely compose if intermediate `PipelineContext` types evolve — need a stable context contract
- Over-engineering risk: if the current 5-stage pipeline is fixed and well-understood, middleware flexibility adds complexity without payoff
- Debugging middleware chains is harder than a flat sequence

---

## Idea 5: Statistical Token Budget with Adaptive Slack

**Name**: Probabilistic Budget Model

**What**: Rather than treating token counts as exact integers, model them as distributions. Every `ContextItem` carries a `TokenCount` that can be `Exact(n)`, `Estimated(mean: n, stddev: s)`, or `Unknown`. The `ContextBudget.MaxTokens` gains a `SafetyMargin` (e.g., 5%) that automatically adjusts the target based on the uncertainty profile of the selected items. If all selected items have exact counts, margin is zero. If many are estimated, margin grows. The slicer picks items accounting for the probability that the total might exceed budget.

Additionally: expose a `TokenCountProvider` delegate on `CupelPolicy` so callers can plug in a real tokenizer (tiktoken, Llama tokenizer, etc.) without making it a core dependency.

**Why**: Real-world token counts are fuzzy. Pre-tokenization estimates can be ±15% off. Hard budget limits with fuzzy inputs lead to either over-filling (bad, can truncate or fail) or excessive conservatism (wasted context). Modeling uncertainty explicitly lets the system make principled decisions: "these 3 items are estimated, add 8% slack." The pluggable `TokenCountProvider` is the correct way to handle the tokenizer-agnostic constraint without baking it in.

**Scope**: Small-medium. The `ContextItem.Tokens` field type changes from `int` to a value type (e.g., `TokenCount`). Slicers need to handle the probabilistic case. The `TokenCountProvider` hook is small and additive.

**Risks**:
- Changing `Tokens` from `int` to a struct is a breaking API change — must be done early or behind an overload
- The probabilistic slicer math can get complex fast — risk of over-engineering if "Estimated ± 10%" is good enough with a simple fixed margin
- Most users just want `int` — make `TokenCount` implicitly convertible from `int` so the simple case stays simple

---

## Idea 6: Semantic Quota System

**Name**: Kind/Tag Quotas

**What**: Add a first-class `QuotaSpec` to `CupelPolicy` that constrains the proportion of the final context by Kind and/or Tag. Quotas are enforced by the slicer stage after scoring. Example: "system_message must be ≥5% of budget", "tool_call_result must be ≤30%", "tagged:critical must always include at least N items." The slicer becomes quota-aware: it maximizes score subject to quota constraints, not just token budget.

```csharp
policy.Quotas = new QuotaSpec()
    .Require(Kind.SystemMessage, minPercent: 5)
    .Cap(Kind.ToolCallResult, maxPercent: 30)
    .RequireAtLeast(tag: "critical", count: 3);
```

**Why**: Scoring produces a ranked list, but without quotas, a greedy slicer will fill the budget with whatever scores highest — which is often a flood of tool results or repetitive assistant turns. Quotas encode diversity requirements that scoring alone cannot express. This is the mechanism that prevents context homogenization, which is a known quality failure mode for LLM context windows.

**Scope**: Small-medium. The data model (`QuotaSpec`) is new but simple. The integration point is the slicer — it needs quota-awareness. `GreedySlice` becomes quota-constrained greedy. `KnapsackSlice` gains quota constraints (a mixed-integer constraint, can be approximated). The `QuotaSlice` already exists — this proposal formalizes and elevates it to a first-class policy feature rather than an optional slicer.

**Risks**:
- Quota constraints can make the optimization problem harder — quota-constrained knapsack is NP-hard in the general case; need to decide on approximation strategy
- Conflicting quotas (e.g., min% + max% that don't add up to 100%) need clear error reporting at configuration time
- Users may over-specify quotas and be confused when budget compliance and quota compliance are in tension

---

## Idea 7: Typed Smelt Integration Contract

**Name**: Smelt Adapter / IContextSource

**What**: Define a clean integration seam for how upstream orchestration (Smelt) provides context candidates to Cupel. Rather than requiring callers to construct `ContextItem` lists manually, expose an `IContextSource` abstraction with typed adapters for common LLM message formats. Provide out-of-box adapters:

- `AnthropicMessageAdapter`: converts `Message[]` → `ContextItem[]`
- `OpenAIMessageAdapter`: converts `ChatMessage[]` → `ContextItem[]`
- `SmeltConversationAdapter`: converts Smelt's native turn format

Also define `IContextSink` for the output side — allowing the placer output to be consumed as typed message lists, not just `ContextItem[]`. This makes Cupel a zero-friction drop-in at the Smelt boundary.

**Why**: The hardest part of adopting a context management library is the integration boundary — converting your existing message types to the library's types and back. First-class adapters lower adoption friction dramatically. For the Wollax stack specifically, a `SmeltConversationAdapter` means Smelt can call `CupelPolicy.Apply(smeltConversation, budget)` directly. This is the difference between "interesting library" and "I actually use this."

**Scope**: Small for the abstraction, medium-large for the adapters (each adapter requires understanding the source message schema). The core library only needs `IContextSource` and `IContextSink`. Adapters ship as separate NuGet packages (`Cupel.Adapters.Anthropic`, etc.) to avoid dependency creep.

**Risks**:
- Adapter packages create ongoing maintenance burden as upstream SDKs evolve their message types
- The `IContextSource` abstraction must be rich enough to infer `Kind`, `Tokens`, `Timestamp` etc. from source types — this inference is lossy and may require heuristics
- Risk of over-committing to Smelt coupling early when the Smelt API isn't stable yet

---

## Summary Matrix

| Idea | Impact | Effort | Risk | Priority |
|------|--------|--------|------|----------|
| Scorer DAG | High | Medium-Large | Medium | Phase 2 |
| Policy Serialization | Very High | Medium | Medium | Phase 1 |
| Explainability Trace | Very High | Medium | Low | Phase 1 |
| Middleware Pipeline API | High | Medium | Low | Phase 1 |
| Statistical Budget | Medium | Small-Medium | Low | Phase 1 |
| Semantic Quotas | High | Small-Medium | Medium | Phase 1 |
| Smelt Adapters | High | Medium-Large | Medium | Phase 2 |

**Phase 1 recommendations**: Trace + Middleware API + Policy Serialization form a coherent foundation. Statistical Budget and Semantic Quotas are additive. Scorer DAG and Smelt Adapters are Phase 2 once the core is validated.
