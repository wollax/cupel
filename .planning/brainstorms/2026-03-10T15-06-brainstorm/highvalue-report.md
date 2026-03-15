# Cupel High-Value Architecture — Consolidated Report

*Explorer: explorer-highvalue | Challenger: challenger-highvalue | Date: 2026-03-10*
*2 rounds of debate. Source proposals: highvalue-ideas.md*

---

## Executive Summary

7 proposals entered debate. 5 survived with significant scoping. 1 was replaced by a simpler alternative. 1 was split into a core abstraction (kept) and adapter implementations (deferred to consumers). The debate produced tighter scope, a cleaner phase ordering, and several specific implementation constraints that weren't in the original proposals.

---

## Decision 1: ContextResult Return Type + Explainability Trace

**Decision**: Ship first, before the API surface hardens. This is the most breaking change.

**What**: Change `CupelPipeline.Apply()` return type from `IReadOnlyList<ContextItem>` to `ContextResult`, which carries both the selected items and an optional `ContextTrace`.

```csharp
public sealed record ContextResult(
    IReadOnlyList<ContextItem> Items,
    ContextTrace? Trace  // null when tracing is disabled
);
```

**Implementation constraints from debate**:
- Trace event *construction* must be gated, not just collection. An `if (trace.IsEnabled)` guard is required before allocating any trace event objects. "NullTraceCollector = zero overhead" is only true if allocation is also skipped.
- Do **not** use `AsyncLocal<ITraceCollector>` — does not reliably flow across all async contexts. Pass the `ITraceCollector` explicitly through the pipeline, even though it touches more interfaces.
- The `ITraceCollector` default is a `NullTraceCollector` (no-op). Full tracing enabled by injecting a `DiagnosticTraceCollector`.

**Why**: Explainability is what makes Cupel trustworthy rather than opaque. "Why did my system prompt get dropped?" must have an answerable path. Trace data also enables test assertions on include/exclude decisions independent of final list comparison. The return type change must precede any stable API surface — doing it in v2 breaks every existing caller.

**Phase**: 1 — first feature shipped.

---

## Decision 2: TokenCountProvider Delegate + EstimationSafetyMarginPercent

**Decision**: Ship the delegate. Drop the probabilistic distribution model entirely.

**What**:
```csharp
// On ContextBudget:
public float EstimationSafetyMarginPercent { get; init; } = 0f;

// On CupelPolicy:
public Func<ContextItem, int>? TokenCountProvider { get; init; }
```

`TokenCountProvider` is called when an item's token count is not pre-computed. Callers can plug in a real tokenizer (tiktoken, etc.) without Cupel having a tokenizer dependency.

`EstimationSafetyMarginPercent` replaces the full probabilistic model: if set to 10, the effective budget is `MaxTokens * 0.90`. Simple, explicit, no discriminated union types.

**Rejected from original proposal**: The `TokenCount` as `Exact/Estimated/Unknown` discriminated union. The `Unknown` state is a landmine — slicer behavior on unknown counts is undefined and surprising. A simple safety margin and a provider delegate solve 95% of the real problem.

**Phase**: 1.

---

## Decision 3: Semantic Quotas (Percentage-Only)

**Decision**: Ship percentage-based quotas. Defer count-based quotas.

**What**:
```csharp
policy.Quotas = new QuotaSpec()
    .Require(Kind.SystemMessage, minPercent: 5)
    .Cap(Kind.ToolCallResult, maxPercent: 30);
    // count-based: deferred to Phase 2
```

Quotas are enforced at the slicer stage after scoring. They prevent context homogenization — the failure mode where a greedy slicer floods the budget with whatever scores highest (typically tool results or repetitive assistant turns).

**Implementation constraints from debate**:
- `minPercent`/`maxPercent` are fractions of `MaxTokens` (not of actually-selected tokens). This must be documented explicitly — the two interpretations produce different numbers for sparse budgets.
- **Pinned item interaction must be defined**: if 2 items tagged "critical" are pinned and a `RequireAtLeast(Kind.SystemMessage, 3)` quota exists but budget allows only 1 more, the quota fails visibly with a clear error (not silently). This interaction must be specified, not discovered. *(Count-based quotas are deferred partly because this interaction is complex — defer until the semantics are fully designed.)*
- Quota-constrained greedy is fine for Phase 1. Quota-constrained knapsack is NP-hard in general — use approximation or greedy with backtracking.
- Conflicting quota specifications (min + max constraints that can't be simultaneously satisfied) must be caught at `Build()` time with a descriptive error.

**Phase**: 1.

---

## Decision 4: IContextSource in Core (Adapters in Consumer Repos)

**Decision**: Define `IContextSource` in core Cupel. Adapters belong in consumer code (Smelt, user repos), not in Cupel packages.

**What**:
```csharp
public interface IContextSource
{
    IAsyncEnumerable<ContextItem> GetCandidatesAsync(CancellationToken ct = default);
}
```

**Rejected from original proposal**: `IContextSink` (output adapters) — scope creep. Cupel selects context; output conversion is the consumer's responsibility. Rejected `Cupel.Adapters.Anthropic` packages — creates Anthropic SDK version coupling that Cupel would own forever.

**Why**: The integration seam is real and worth naming formally. But the adapter implementations are best owned by whoever owns the source types. Smelt owns the Smelt→ContextItem conversion. User code owns any OpenAI→ContextItem conversion. Cupel only needs the abstraction.

**Phase**: 1 (the interface is small and foundational).

---

## Decision 5: Fluent Builder (No Call-Next Middleware Semantics)

**Decision**: Fluent builder over a fixed pipeline. Drop call-next middleware semantics entirely.

**What**:
```csharp
var pipeline = CupelPipeline.CreateBuilder()
    .WithClassifier(new KindClassifier())
    .WithScorer(new RecencyScorer())
    .WithScorer(new PriorityScorer())
    .WithSlicer(new KnapsackSlicer())
    .WithPlacer(new UShapedPlacer())
    .Build(); // validates required stages are present
```

This is a **configuration builder over a fixed pipeline**, not a middleware chain. The stage sequence (Classify → Score → Deduplicate → Slice → Place) is deliberately fixed. Users can't reorder stages; they can only substitute implementations.

**Why fluent builder vs. constructor**: The builder is the discoverability surface. `CreateBuilder().` + IntelliSense is how new users learn the pipeline exists and what stages are configurable. Constructor-based APIs with optional parameters become migration problems when new optional stages are added (parameter count explosion or breaking changes). The builder absorbs optional stage additions without touching existing call sites.

**Rejected from original proposal**: Call-next middleware semantics. A misconfigured middleware that forgets to call `next()` silently drops all remaining items — worse than any current failure mode. The fixed pipeline fails predictably; middleware semantics make failure unpredictable.

**Phase**: 1 polish — lands after ContextResult, TokenCountProvider, and Quotas since it has no temporal dependency on API hardening. But it's not cosmetic — it's the public ergonomics surface.

---

## Decision 6: CompositeScorer (Replaces Scorer DAG)

**Decision**: Implement `CompositeScorer` with configurable aggregation. Reject the full Scorer DAG for v1.

**What**:
```csharp
new CompositeScorer(
    scorers: new IScorer[] { new RecencyScorer(), new FrequencyScorer() },
    strategy: AggregationStrategy.WeightedAverage(weights: [0.6, 0.4])
)
```

Composites can be nested — a `CompositeScorer` can contain another `CompositeScorer` — achieving any DAG-like composition without a DAG execution engine.

**Why rejected the DAG**: The DAG execution engine (cycle detection, topological sort, parallel branch scheduling) is hundreds of lines of infrastructure for a problem that `CompositeScorer` solves with ~30 lines. Parallel branch execution offers no performance benefit when scoring in-memory lists of 100–1000 items — `Task` scheduling overhead exceeds scoring time. Revisit if a real use case requires it.

**Critical constraint surfaced in debate — IScorer normalization**:
- `IScorer` output **must be 0.0–1.0** for `CompositeScorer` aggregation to be meaningful. A `RecencyScorer` emitting [0, 1] combined with a `PriorityScorer` emitting [1, 10] via WeightedAverage produces garbage.
- **Do not make this an interface contract** (a `NormalizedScore` return type doesn't enforce bounds at compile time anyway, and it breaks third-party scorers that work on different natural scales).
- **Instead**:
  1. XML doc warning on `IScorer`: "Implementations should return values in [0.0, 1.0]. Violating this may produce unexpected CompositeScorer results."
  2. `CompositeScorer` validates input scores — clamp in release, throw in debug (or always clamp with optional `StrictMode = true`).
  3. Ship `ScaledScorer(IScorer inner, double min, double max)` wrapper for scorers that don't naturally produce 0-1 output.

**Phase**: Phase 1 for `CompositeScorer` + `ScaledScorer`. Scorer DAG revisited in Phase 3 if demand materializes.

---

## Decision 7: Policy Serialization (Incremental Stable Subsets)

**Decision**: Serialize stable subsets incrementally. `[JsonPropertyName]` attributes from day 1 on all public types. No YAML. No hot reload in v1.

**Phase 1** (stable types only):
- `ContextBudget` serialization (MaxTokens, OutputReserve, EstimationSafetyMarginPercent)
- `SlicerConfig` serialization (discriminated union on slicer type + parameters)
- `PolicyLoader` abstraction (load policy from JSON stream/file)
- `RegisterScorer(string name, Func<IScorer> factory)` hook **designed now**, even though custom scorers aren't serializable yet. Explicit constraint in docs: "Only built-in scorers are round-trippable."

**Phase 2**: Scorer serialization after `CompositeScorer` API settles.
**Phase 3**: Hot reload / `PolicyWatcher` (FileSystemWatcher + thread-safety concerns out of scope for v1).

**Critical implementation constraint from debate**:
> `[JsonPropertyName]` attributes must be applied to all public types **at definition time**, before any serialization is exposed. Retroactive application changes the wire format for anyone who was already using `JsonSerializer.Serialize` directly (they were). Attribute the types early; expose `PolicyLoader` later.

**Why not YAML**: Contradicts "minimal dependencies" constraint. JSON only for v1.

**Why not "API frozen first"**: No pre-1.0 API is truly frozen. The right condition is "serialize subsets that are clearly stable" — `ContextBudget` and `SlicerConfig` qualify. Volatile scorer config defers to Phase 2.

---

## Phase Ordering (Final)

| Phase | Items | Rationale |
|---|---|---|
| 1a | `ContextResult` return type + `ContextTrace` | Most breaking — must land before API hardens |
| 1b | `TokenCountProvider` + `EstimationSafetyMarginPercent` | Unblocks real usage; small, clean |
| 1b | Semantic Quotas (percentage-only) | Context homogenization fix; independent of above |
| 1b | `IContextSource` in core | Small interface, foundational |
| 1c | `CompositeScorer` + `ScaledScorer` | Scorer composition primitive |
| 1d | Fluent builder (no call-next) | Discoverability surface; no temporal dependency |
| 1d | `[JsonPropertyName]` on all public types | Wire format stability; should happen during type definition |
| 2 | Budget + Slicer serialization + `PolicyLoader` | After 1d types are attributed |
| 2 | Scorer serialization | After CompositeScorer API settles |
| 3 | Scorer DAG | Only if demand materializes |
| 3 | Hot reload / PolicyWatcher | Separate concern, complex threading |
| Consumer | Adapters (Anthropic, OpenAI, Smelt) | Owned by consumer repos, not Cupel |

---

## Rejected Proposals (Final)

| Proposal | Rejection Reason |
|---|---|
| Scorer DAG (full) | `CompositeScorer` solves 95% of the problem with 5% of the complexity |
| Probabilistic TokenCount distribution | `EstimationSafetyMarginPercent` is sufficient; `Unknown` state is a landmine |
| IContextSink (output adapters) | Scope creep; Cupel ends at context selection |
| `Cupel.Adapters.*` packages | Creates SDK version coupling Cupel would own forever; belongs in consumers |
| YAML policy serialization | Contradicts minimal-dependencies goal |
| PolicyWatcher / hot reload | Complex threading and FileSystemWatcher concerns; Phase 3 at earliest |
| Count-based quotas | Interaction with pinned items and token budgets not fully designed; deferred |
| Call-next middleware semantics | Silent-drop failure mode worse than fixed pipeline |

---

## Key Architectural Invariants (Surfaced by Debate)

1. **IScorer output is conventionally 0.0–1.0** — enforced by `CompositeScorer` clamping, not by interface type
2. **`[JsonPropertyName]` on all public types from day 1** — wire format stability requires early attribution
3. **ContextResult is the return type from day 1** — deferring breaks callers
4. **Trace event construction is gated** — `IsEnabled` check before any allocation, not just before collection
5. **No AsyncLocal for trace propagation** — explicit pass-through only
6. **Pinned item + quota interaction is specified behavior** — not discovered at runtime
7. **`RegisterScorer` registry designed in Phase 1** — even if unused until Phase 2 serialization
8. **`IContextSink` does not exist** — Cupel selects; consumers convert
