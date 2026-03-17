# High-Value Feature Ideas: Cupel v1.2+

**Explorer:** explorer-highvalue
**Date:** 2026-03-15

Context: v1.0 shipped the full .NET pipeline (8 scorers, 4 slicers, 2 placers, CompositeScorer, QuotaSlice, policy presets, explainability trace, JSON serialization). v1.1 shipped the Rust crate with feature parity on pipeline logic but missing the .NET-exclusive DX layer. The March 10 high-value brainstorm's decisions are all shipped. This explores what's genuinely next.

---

## Proposal 1: Rust Diagnostics Parity

**Name:** Full ITraceCollector + SelectionReport in Rust Crate

**What:**
Implement the complete diagnostics stack in the Rust crate to match .NET v1.0 capability. This means:
- A `TraceCollector` trait (analogous to `ITraceCollector`) with a `NullTraceCollector` (zero-overhead) and `DiagnosticTraceCollector` (buffered in-memory)
- `SelectionReport` struct carrying per-item inclusion/exclusion reasons with scores
- `TraceEvent` enum for pipeline stage events (Classify, Score, Deduplicate, Sort, Slice, Place)
- `PipelineBuilder` with optional `.with_trace(collector)` hookup
- Optional serde serialization of `SelectionReport` for programmatic consumption

The `.NET` side already has the specification — it becomes the implementation template. The Rust version should be idiomatic: `TraceCollector` is a trait, events are passed by value, the null collector is a zero-sized type.

**Why:**
The Rust crate is published on crates.io and publicly visible, but users of it have no way to answer "why did my system prompt get dropped?" The .NET version's core pitch is "full explainability — every inclusion/exclusion has a traceable reason." Rust offers none of this. For serious agent frameworks in Rust (which are proliferating — Rig, LangChain-rs, etc.), this is a deal-breaker. Closing this gap elevates the Rust crate from "adequate port" to "first-class Cupel." It also establishes the spec pattern for future language implementations.

**Scope:** Large. Threading a trace context through 6 pipeline stages touches every stage function signature. The trait design requires careful thought around lifetimes and the zero-cost abstraction (zero-sized `NullTraceCollector` must compile to zero overhead). Estimated 2-3 implementation phases including spec conformance vectors for the diagnostics layer.

**Risks:**
- Lifetime/borrow complexity: the trace collector reference must outlive the pipeline call. This argues for `&dyn TraceCollector` or `Arc<dyn TraceCollector>` — the right choice has ergonomic implications.
- Spec gap: the .NET specification covers trace output content but not the diagnostics API contract. A new spec section is needed before or concurrent with implementation.
- Stage signature churn: every pipeline stage function gains a `trace: &T` or `trace: &mut T` parameter. This is a breaking change in internal module APIs (minor impact since they're private).

---

## Proposal 2: `Cupel.Testing` Package — Fluent Pipeline Assertions

**Name:** Dedicated Testing Library for Cupel Pipelines

**What:**
A new `Wollax.Cupel.Testing` NuGet package (zero runtime dependency — test-only) providing:

```csharp
// Assert on SelectionReport
var report = pipeline.Execute(items).Report!;

report.Should().IncludeItemWith(i => i.Kind == ContextKind.SystemPrompt);
report.Should().ExcludeItemWith(i => i.Kind == ContextKind.ToolOutput)
      .WithReason(ExclusionReason.BudgetExceeded);
report.Should().HaveTokenUtilizationAbove(0.85);
report.Should().HaveKindDistribution(ContextKind.Message, minPercent: 20, maxPercent: 60);
report.Should().HaveNoExcludedItemsWithPriorityAbove(5);
report.Should().PlaceHighScorersAtEdges(); // validates U-shaped placer behavior

// Snapshot testing for policy regression
var snapshot = report.ToSnapshot();
snapshot.ShouldMatchApproved(); // ApprovalTests integration
```

Design principles:
- Wraps `SelectionReport` — the existing trace output is the data source
- FluentAssertions-style extension methods (familiar to .NET test authors)
- No mock types — works with real pipeline instances
- Snapshot-compatible: `SelectionReport` serializes to a deterministic JSON representation for approval testing

**Why:**
Teams shipping AI products need to test their context policies. Currently the only test surface is "did the right items come back?" — list equality assertions. But context policies have emergent quality properties: "did we maintain minimum system prompt coverage?", "did we avoid flooding with tool outputs?", "are the highest-priority items being placed at edges?" These can't be expressed with list equality. A testing package makes policies first-class testable units, closes the loop between "we configured a policy" and "we verified the policy does what we think." This is high-value for any team using Cupel in production, and it's a differentiator no other context management library has.

**Scope:** Medium. The core assertions are thin wrappers over `SelectionReport` (which already exists). The package itself is ~300-500 lines. The main design work is the assertion vocabulary: which properties of a selection are worth asserting on? This requires identifying the most common context quality failure modes. Snapshot integration is optional additive scope.

**Risks:**
- API design is the hard part: assertion vocabulary must be expressive enough to be useful but not so large it becomes its own maintenance burden.
- FluentAssertions is an external dependency — need to decide: take FA as a dep, or implement chainable assertions in-house. In-house keeps zero external deps; FA buys familiar ergonomics.
- Snapshot testing requires deterministic `SelectionReport` serialization across pipeline runs — currently reports don't guarantee ordering stability for ties.

---

## Proposal 3: OpenTelemetry Pipeline Tracing

**Name:** `Cupel.Diagnostics.OpenTelemetry` Integration Package

**What:**
A `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet package that bridges the existing `ITraceCollector` interface to the OpenTelemetry .NET `ActivitySource` API:

```csharp
// Registration:
services.AddCupel()
    .AddOpenTelemetryTracing(); // emits pipeline spans to configured OTEL provider

// What it emits:
// Span: cupel.pipeline.execute
//   ├── Span: cupel.stage.classify (attributes: pinned_count, scoreable_count)
//   ├── Span: cupel.stage.score (attributes: scorer_type, item_count)
//   ├── Span: cupel.stage.deduplicate (attributes: duplicates_removed)
//   ├── Span: cupel.stage.slice (attributes: slicer_type, tokens_selected, tokens_available)
//   └── Span: cupel.stage.place (attributes: placer_type, items_placed)
// Events on parent span:
//   - item.excluded (item_id, reason, score, kind)
//   - item.included (item_id, score, kind, placement_index)
```

Semantic conventions defined in-package: `cupel.item.kind`, `cupel.item.score`, `cupel.budget.max_tokens`, `cupel.budget.tokens_used`, etc.

**Why:**
Enterprise AI applications are instrumented with OpenTelemetry. Jaeger, Honeycomb, DataDog, Grafana — every serious production environment has distributed tracing. Without OTEL integration, Cupel is opaque in production: you can't see pipeline latency in traces, you can't correlate "LLM got a bad answer" with "Cupel dropped these 5 items." The existing `ITraceCollector` is precisely the right abstraction for bridging to OTEL — this package makes the bridge explicit and standards-compliant. This is a relatively small package with an outsized impact on Cupel's enterprise credibility. It's also a strong signal: "Cupel is production-ready, not just a toy library."

**Scope:** Medium. The OTEL SDK for .NET is well-documented. `ActivitySource` + `Activity` APIs map naturally to the existing `ITraceCollector`. The bridge package is likely 200-400 lines plus tests. The semantic conventions need careful design — they should be stable since external tooling may depend on attribute names.

**Risks:**
- OTEL SDK version coupling: the `System.Diagnostics.DiagnosticSource` package ships frequently. Need to choose a floor version and test against it.
- Semantic convention design: if attribute names are wrong in v1, changing them is a breaking change for dashboards. Study existing OTEL semantic conventions (LLM-specific ones are still evolving) and pick conservative names.
- Verbosity vs signal: emitting one span per item in large pipelines (500+ items) produces very wide traces. Need configurable verbosity levels (stage-only vs. item-level).

---

## Proposal 4: Budget Simulation & What-If API

**Name:** Context Pressure Analysis and Budget Sweep

**What:**
A new `ContextAnalyzer` API (on top of the existing pipeline) for non-destructive what-if analysis:

```csharp
var analyzer = pipeline.CreateAnalyzer(items);

// What's the minimum budget to include this specific item?
int minBudget = analyzer.FindMinBudgetFor(importantItem); // binary search over DryRun

// How does inclusion change across a budget range?
BudgetCurve curve = analyzer.SweepBudget(
    from: 1000, to: 16000, steps: 16);
// curve[i] = { Budget, IncludedCount, TokenUtilization, IncludedItems }

// What's the "context pressure" right now?
ContextPressure pressure = analyzer.Measure(budget);
// pressure.ExcludedHighPriorityCount — how many priority>=7 items were cut?
// pressure.BudgetUtilization — 0.0-1.0, how full is the window?
// pressure.KindDiversity — how many distinct kinds made it in?
// pressure.AtRiskItems — items on the margin (would be dropped at 90% of current budget)

// Which items are "on the bubble"?
IReadOnlyList<ScoredItem> marginal = analyzer.GetMarginalItems(slackTokens: 200);
```

**Why:**
Adaptive agents need to know budget boundaries — not just "what fits at 8K tokens" but "what would fit at 4K, 6K, 8K, 12K?" This enables: dynamic budget selection based on query complexity, negotiation with LLM host APIs that have variable rate limits, preemptive detection of context starvation ("we're about to lose the system prompt at this budget"). The `DryRun` API already exists as a building block. `ContextAnalyzer` assembles it into actionable analytical tools. This also directly supports Smelt integration — Smelt could use budget sweep to decide how much context to request from Cupel before calling the LLM API.

**Scope:** Medium. The `DryRun` API is already implemented. `FindMinBudgetFor` is binary search over `DryRun` calls (10-15 invocations). `SweepBudget` is N parallel `DryRun` calls. `ContextPressure` is a new derived metric type computed from a `SelectionReport`. The main design work is: what metrics belong in `ContextPressure`? What makes `GetMarginalItems` useful vs. confusing?

**Risks:**
- Performance: `SweepBudget` with 16 steps × sub-millisecond pipeline = still fast (< 16ms), but users may call this in hot loops. Document that it's for analysis, not per-request execution.
- Metric design: `ContextPressure` metrics need to be meaningful, not just numbers. "AtRiskItems" is particularly nuanced — how close is "close to the margin"? Need a well-specified algorithm, not just a vague heuristic.
- Complexity creep: `ContextAnalyzer` could expand indefinitely. Scope strictly to: budget sweep, min-budget query, pressure measurement, marginal items. Anything more complex belongs in Smelt.

---

## Proposal 5: Temporal Decay Scorer with Pluggable Curves

**Name:** `DecayScorer` — Time-Based Absolute Recency

**What:**
A new `DecayScorer` scorer that computes recency as an absolute time-decay function rather than the current `RecencyScorer`'s relative ranking. The difference:
- `RecencyScorer`: ranks items relative to peers (most recent = 1.0, regardless of how old "most recent" actually is)
- `DecayScorer`: scores based on absolute elapsed time using a configurable decay function

```csharp
// Exponential half-life decay
new DecayScorer(
    referenceTime: DateTimeOffset.UtcNow, // or pipeline execution time
    decay: DecayCurve.Exponential(halfLife: TimeSpan.FromHours(6))
)
// item from 6h ago → 0.5, from 12h ago → 0.25, from 24h ago → 0.0625

// Step-function decay (domain-expert windows)
new DecayScorer(
    referenceTime: DateTimeOffset.UtcNow,
    decay: DecayCurve.Step(new[] {
        (TimeSpan.FromMinutes(30), 1.0),   // last 30m: full score
        (TimeSpan.FromHours(4),   0.7),    // 30m-4h: 70%
        (TimeSpan.FromHours(24),  0.3),    // 4h-24h: 30%
        (TimeSpan.Zero,           0.0),    // older: excluded
    })
)

// Windowed: items outside window score 0
new DecayScorer(
    referenceTime: DateTimeOffset.UtcNow,
    decay: DecayCurve.Window(maxAge: TimeSpan.FromHours(24))
)
```

The `DecayCurve` type is an abstract base with factory methods. Custom curves implement `double Score(TimeSpan age)`. Null timestamps receive a configurable fallback score (default 0.5 — "unknown age, treat as neutral").

**Why:**
The current `RecencyScorer` is a rank scorer: "most recent of my candidates wins." But in long-running agents (hours, days), "most recent" may still be 8 hours old. Rank-based recency treats a 5-minute-old message and an 8-hour-old message identically if they're the 1st and 2nd most recent. Real cognitive relevance decays over time — a tool output from 6 hours ago about a file that's since been modified is misleading, not helpful. `DecayScorer` enables time-aware context where items genuinely age out. This is especially critical for the `LongRunning` policy preset, which uses `RecencyScorer:weight=3` but would benefit more from exponential decay.

**Scope:** Medium. The scorer itself is ~100 lines. The `DecayCurve` type hierarchy adds ~50-100 lines. Spec additions needed (new scorer type with pseudocode + conformance vectors). Rust port required for feature parity. The spec is the largest investment, not the implementation.

**Risks:**
- Time dependency: `DecayScorer` requires a `referenceTime`. If not provided, it defaults to `DateTimeOffset.UtcNow` at scoring time. This introduces non-determinism — the same items may score differently in two subsequent pipeline calls. Document clearly: for deterministic tests, always provide explicit `referenceTime`.
- Conformance testing complexity: time-based tests must use fixed timestamps in all conformance vectors. This is doable but must be explicitly specified.
- Overlap with `RecencyScorer`: users may wonder "when should I use Decay vs. Recency?" Clear documentation required with decision heuristics.

---

## Proposal 6: Count-Based Quotas + Pinned Item Interaction Spec

**Name:** Complete the Quota System (Deferred from v1.0)

**What:**
Finish the quota design that was deliberately deferred in the March 10 brainstorm. Specifically:

```csharp
// Count-based quotas (deferred from v1.0 Decision 3):
policy.Quotas = new QuotaSpec()
    .RequireAtLeast(ContextKind.SystemPrompt, count: 1)        // always include ≥1 system prompt
    .RequireAtLeast(tag: "critical", count: 3)                 // always include ≥3 critical-tagged items
    .Cap(ContextKind.ToolOutput, maxCount: 10)                 // at most 10 tool outputs
    .Cap(ContextKind.ToolOutput, maxPercent: 40)               // ... or 40% of budget, whichever is less

// Pinned item interaction (the unresolved hard case):
// - 2 items are pinned with Kind=SystemPrompt
// - RequireAtLeast(SystemPrompt, count: 3) is specified
// - Budget allows only 1 more SystemPrompt after pinned items
// → Clear error: "Quota requires 3 SystemPrompt items; 2 are pinned, budget allows 1 more (quota satisfied)"
// → Or fail: "Quota requires 3 SystemPrompt items; only 2 available (pinned=2, selectable=0)"
```

The spec additions needed:
1. Count-based quota algorithm: pre-allocate count slots from budget, fill mandated kinds first, then score-optimize remaining slots
2. Pinned + quota interaction: define priority ordering (pinned items satisfy their count quotas; if pinned alone satisfy minimum, quota is green; if pinned are insufficient and budget can't make up the difference, throw)
3. `maxCount` cap semantics: does it apply before or after pinned items? (Pinned items bypass the cap — they're pinned. Cap applies only to selectable items.)

**Why:**
Count-based quotas solve a different problem than percentage-based ones. "Always include at least 1 system prompt" is a hard guarantee, not a percentage. "Never include more than 10 tool outputs" is a count ceiling, not a percentage ceiling. These are the most common quota patterns in practice. The March 10 brainstorm explicitly deferred this stating "interaction with pinned items and token budgets not fully designed; deferred" — that design work is now worth doing. The quota system is incomplete without count support, and the `QuotaSpec` API is already partially designed for it.

**Scope:** Large. The design is the majority of the work:
- Count quota algorithm for `QuotaSlice` / `GreedySlice` integration
- Pinned + count quota interaction semantics (must be spec-level)
- Conflicting count + percentage quota detection at `Build()` time
- New conformance test vectors for count quotas (minimum 8-10 vectors)
- Rust port of count quota logic

**Risks:**
- The deferred reason was "the semantics aren't fully designed" — this is still true. Getting the semantics wrong in v1.2 and needing to fix them in v1.3 is worse than not shipping them. Requires a dedicated design phase before implementation.
- Count quota + KnapsackSlice interaction: knapsack with mandatory item inclusion is a constrained variant. May need a separate implementation path rather than a clean generalization.
- Quota conflicts: `RequireAtLeast(SystemPrompt, 2).Cap(SystemPrompt, 1)` must be caught at `Build()` time. Detection logic adds non-trivial validation surface.

---

## Summary Matrix

| Idea | Impact | Effort | Risk | Strategic Fit |
|------|--------|--------|------|--------------|
| Rust Diagnostics Parity | Very High | Large | Medium | Closes first-class Rust gap |
| Cupel.Testing Package | High | Medium | Low | Enables quality engineering on policies |
| OpenTelemetry Integration | High | Medium | Medium | Enterprise production-readiness |
| Budget Simulation API | High | Medium | Low | Adaptive agents + Smelt integration |
| Temporal Decay Scorer | Medium-High | Medium | Low | Quality improvement for long-running agents |
| Count-Based Quotas | High | Large | High | Completes the quota system |

**Strategic grouping:**
- **Production readiness**: OpenTelemetry + Cupel.Testing (teams can operate Cupel confidently in production)
- **Rust completeness**: Rust Diagnostics (Rust crate becomes a first-class Cupel)
- **Capability gaps**: Count-Based Quotas + Temporal Decay (complete features that are partially designed)
- **Ecosystem integration**: Budget Simulation (Smelt can consume it directly)
