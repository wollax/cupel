# Cupel High-Value Features — Consolidated Report

*Explorer: explorer-highvalue | Challenger: challenger-highvalue | Date: 2026-03-15*
*2 rounds of debate. Source proposals: highvalue-ideas.md*

---

## Executive Summary

6 proposals entered debate. 5 survive with significant scoping and design constraints. 1 was reshaped from a wrapper type into extension methods. 1 was downgraded to a design-phase-only deliverable. The debate resolved two non-trivial design questions: the right shape for SelectionReport analytics (extension methods, not a wrapper type), and the right fix for time-dependent scorer staleness (`TimeProvider` injection, not per-execution context). Both resolutions are cleaner than the original proposals.

---

## Decision 1: Rust Diagnostics Parity

**Decision:** Ship — spec section as standalone deliverable first, implementation follows.

**What:**
Full diagnostics stack in the Rust crate, mirroring the .NET `ITraceCollector` / `SelectionReport` / `DiagnosticTraceCollector` system:
- `TraceCollector` trait with `NullTraceCollector` (zero-sized type, zero overhead) and `DiagnosticTraceCollector` (buffered in-memory)
- `SelectionReport` struct carrying per-item inclusion/exclusion reasons with scores
- `TraceEvent` enum for pipeline stage events (Classify, Score, Deduplicate, Sort, Slice, Place)
- `PipelineBuilder::with_trace(collector)` hookup
- Optional `serde` serialization of `SelectionReport`

**Implementation constraints from debate:**
- **Spec-first sequencing is mandatory.** Do not co-develop spec and implementation — the Rust lifetime constraints (`&dyn TraceCollector` vs `Arc<dyn>`) will pressure-shape the API toward Rust ergonomics rather than semantic clarity. Write the diagnostics spec chapter first (language-agnostic, as a new chapter in `/spec/`), review it independently, then implement in both Rust and .NET if .NET needs any adjustments.
- **`&dyn` vs `Arc<dyn>` is a spec-level decision.** `&dyn` forces a lifetime parameter on `Pipeline::run()`, cascading into `PipelineBuilder`. `Arc<dyn>` is ergonomic but has runtime cost. The spec must define the ownership model explicitly before it becomes an uncuttable API surface on crates.io.
- **Zero-overhead null path must be verified.** `NullTraceCollector` being a zero-sized type means rustc should optimize away all trace branches when it's the concrete type. Verify with `cargo asm` or a micro-benchmark that the null path produces no allocations.

**Why:**
The Rust crate is published on crates.io without diagnostics. For any user debugging a production agent pipeline, "why did this item get dropped?" has no answerable path in Rust. The .NET explainability system is Cupel's core quality differentiator — the Rust crate needs it too to be a first-class Cupel, not just an adequate port. The spec gap (diagnostics API contract not yet written) means this is design work, not just porting work.

**Phase ordering:** Spec chapter (standalone) → Implementation.

---

## Decision 2: `Cupel.Testing` Package

**Decision:** Ship — vocabulary design phase first; in-house assertion chains, no FluentAssertions dependency, no snapshot/ApprovalTests integration.

**What:**
`Wollax.Cupel.Testing` NuGet package (test-only, zero runtime dependency) providing fluent assertion chains over `SelectionReport`:

```csharp
var report = pipeline.Execute(items).Report!;

report.Should().IncludeItemWith(i => i.Kind == ContextKind.SystemPrompt);
report.Should().ExcludeItemWith(i => i.Kind == ContextKind.ToolOutput)
      .WithReason(ExclusionReason.BudgetExceeded);
report.Should().HaveTokenUtilizationAbove(0.85);
report.Should().HaveKindInIncluded(ContextKind.Message);
report.Should().HaveAtLeastNExclusions(3);
report.Should().PlaceItemAtEdge(specificItem);  // validates U-shaped placer
```

**Implementation constraints from debate:**
- **Vocabulary design is a dedicated work item, not a side-effect of implementation.** The question "what are the meaningful assertions over a `SelectionReport`?" must be answered before a single line of the assertion library is written. Output: a list of 10-15 named assertion patterns with precise specifications (what does each assert, what are the tolerance/edge cases, what error message does it produce on failure). This can be published as a spec section.
- **No FluentAssertions dependency.** FA is heavy test infrastructure. Implement chainable assertions in-house (~100 lines for the chain plumbing). The error messages should be as informative as FA's — this is implementation work, not shortcut justification.
- **No snapshot/ApprovalTests integration.** `SelectionReport` ordering stability for ties is not yet guaranteed, which is a precondition for snapshot testing. Descope this entirely. If snapshot testing becomes valuable later, it can be added to a `Cupel.Testing.Snapshots` package after the ordering guarantee is shipped.
- **`PlaceHighScorersAtEdges()` must have a precise specification.** "High scorer at edge" needs: what counts as high-scoring (top N by score? top quartile?), what counts as at-edge (first/last N positions? first/last 20%?), what's the tolerance for ties. This is a vocabulary design question, not an implementation question.

**Why:**
Context policies have emergent quality properties that list-equality assertions cannot capture: minimum kind coverage, exclusion-reason verification, budget utilization ranges, placement correctness. A testing package names these properties precisely, making policies first-class testable units. This is a differentiator — no other context management library has it.

**Phase ordering:** Vocabulary design (spec section, ~10-15 named patterns with specs) → Implementation.

---

## Decision 3: OpenTelemetry Integration

**Decision:** Ship — stage-only default verbosity; item-level opt-in; `cupel.*` attribute namespace explicitly marked pre-stable.

**What:**
`Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet package bridging `ITraceCollector` to `System.Diagnostics.ActivitySource`:

```csharp
services.AddCupel()
    .AddOpenTelemetryTracing(options =>
    {
        options.Verbosity = TraceVerbosity.StageOnly;       // default
        // options.Verbosity = TraceVerbosity.StageAndExclusions;
        // options.Verbosity = TraceVerbosity.Full;          // item-level events
    });
```

Stage-only emits one Activity per pipeline stage (6 total), tagged with stage-level attributes. Full verbosity adds item-level events per included/excluded item.

**Implementation constraints from debate:**
- **`stage-only` is the default; item-level events are opt-in.** 500 items × item-level events = 1000 OTEL events per pipeline call. In high-throughput environments this is cardinality explosion. Document the tradeoff explicitly.
- **Own `cupel.*` attribute namespace; do not chase `gen_ai.*`.** OpenTelemetry LLM SIG semantic conventions have been in draft for 18+ months with multiple revisions. Aligning prematurely risks dashboard breakage on every SIG revision. Use `cupel.pipeline.stage`, `cupel.item.kind`, `cupel.budget.max_tokens`, etc. Document explicitly in package README: "Attribute names are pre-stable and subject to change as OTEL LLM semantic conventions stabilize."
- **Verbosity levels must be specified before implementation.** Exact set of attributes per verbosity level, with no overlap ambiguity.

**Why:**
Enterprise AI applications are instrumented with OpenTelemetry. Without it, Cupel is opaque in production — you can't see pipeline latency in distributed traces, can't correlate "LLM degraded output" with "Cupel dropped high-priority items." The existing `ITraceCollector` is precisely the bridge point. This is a relatively small package (~300 lines) with outsized impact on enterprise production-readiness.

---

## Decision 4: Budget Simulation — Scoped API

**Decision:** Ship scoped — `GetMarginalItems` and `FindMinBudgetFor` as methods on `CupelPipeline`; `SweepBudget` moves to Smelt.

**What:**
Two methods on `CupelPipeline` (or as extension methods):

```csharp
// Items that would be excluded at (budget.MaxTokens - slackTokens):
// single additional DryRun call, diffed against primary result
IReadOnlyList<ScoredItem> marginal = pipeline.GetMarginalItems(items, budget, slackTokens: 200);

// Minimum budget required to include a specific item:
// binary search over DryRun calls (~10-15 invocations)
int minBudget = pipeline.FindMinBudgetFor(items, targetItem, searchCeiling: 32000);
```

**Implementation constraints from debate:**
- **`FindMinBudgetFor` requires monotonicity.** Item inclusion is monotonic w.r.t. budget only for pipelines without `QuotaSlice` — increasing budget can shift quota percentage allocations, which can exclude items that fit at smaller budgets. Guard at the call site: `if (pipeline.Slicer is QuotaSlice) throw new NotSupportedException("FindMinBudgetFor is not supported with quota-constrained slicers — quota percentage changes with budget can produce non-monotonic inclusion.")`. Document clearly.
- **`SweepBudget` belongs in Smelt.** Linear stepping (`from: 1000, to: 16000, steps: 16`) encodes assumptions about which budgets are meaningful. Smelt knows its provider-specific context window sizes (4K, 8K, 16K, 32K, 128K) and should own the sweep logic over those discrete points. Cupel's job is `DryRun`, not the sweep strategy.
- **`GetMarginalItems` is a single additional `DryRun` call** with `budget.MaxTokens - slackTokens`. The diff is: items in primary result but not in margin-budget result. This is deterministic and well-specified.

**Separate: SelectionReport extension methods (from ContextPressure debate):**

Three extension methods shipped in `Wollax.Cupel` core (or `Wollax.Cupel.Analytics`):

```csharp
report.BudgetUtilization(budget)                    // tokens used / tokens available
report.KindDiversity()                              // distinct ContextKind values in Included
report.ExcludedItemCount(Func<ContextItem, bool>)   // general predicate, consumer owns threshold
```

No `ContextAnalyzer` type. No `ContextPressure` wrapper type. The metrics are derivations from a single `SelectionReport` — extension methods are the right shape for point computations with no "pass this measurement somewhere as a unit" use case.

**Why:**
Adaptive agents need to understand budget boundaries. `GetMarginalItems` tells you what you're "about to lose" as budgets tighten. `FindMinBudgetFor` tells you the cost of including a specific item. These are pure Cupel-layer analytics. The extension methods add observable quality metrics without adding a wrapper type.

---

## Decision 5: `DecayScorer` — Built-in Scorer with `TimeProvider` Injection

**Decision:** Ship — `TimeProvider` injection resolves staleness; `IDecayCurve` internal, 3 factory methods public; null-timestamp policy configurable.

**What:**

```csharp
// Production (live clock, fresh on every Score() call):
new DecayScorer(
    timeProvider: TimeProvider.System,
    decay: DecayCurve.Exponential(halfLife: TimeSpan.FromHours(6)),
    nullTimestampScore: 0.5)  // score for items with no timestamp

// Tests (deterministic):
var fakeTime = new FakeTimeProvider(DateTimeOffset.Parse("2026-03-15T09:00Z"));
new DecayScorer(timeProvider: fakeTime, decay: DecayCurve.Step(new[] {
    (TimeSpan.FromMinutes(30), 1.0),
    (TimeSpan.FromHours(4),   0.7),
    (TimeSpan.FromHours(24),  0.3),
    (TimeSpan.Zero,           0.0),
}))

// Three public factory methods:
DecayCurve.Exponential(halfLife: TimeSpan)
DecayCurve.Step(IReadOnlyList<(TimeSpan maxAge, double score)> windows)
DecayCurve.Window(maxAge: TimeSpan)  // binary: 1.0 if within window, 0.0 if outside
```

**Implementation constraints from debate:**
- **`TimeProvider` is mandatory, not optional.** No constructor overload that defaults to `TimeProvider.System` silently — the caller must explicitly supply it. This makes the time dependency visible at the call site and prevents accidental test non-determinism. `TimeProvider` is in `System.Threading` (BCL, .NET 8+); `FakeTimeProvider` is in `Microsoft.Extensions.TimeProvider.Testing`.
- **`IDecayCurve` is internal.** The three concrete curves cover the 95% use case. Cupel's extensibility contract is `IScorer` — a user who needs Sigmoid decay implements `IScorer` directly, not `IDecayCurve`. Making `IDecayCurve` public commits a curve interface to the stable API surface without data justifying it. If usage patterns in the wild justify making it public, that's a v1.3 decision.
- **`nullTimestampScore` is configurable.** Pinned timestamp handling (null = 0.0, or 0.5, or 1.0) is opinionated; the library must not choose for the consumer. Default to `0.5` (neutral) with documentation explaining the choice.
- **Spec additions required.** New scorer type with pseudocode, edge cases (null timestamp, zero half-life, age exactly at window boundary), and conformance vectors with fixed `referenceTime` in all test cases. Rust port required for feature parity.

**Why:**
`RecencyScorer` is rank-based: "most recent of my candidates wins." In long-running agent trajectories, this treats a 5-minute-old message and an 8-hour-old message identically if they're the 1st and 2nd most recent. `DecayScorer` computes absolute time-based relevance. This is the difference between "this is the most recent item" and "this item is still relevant." The 5-line DIY alternative doesn't standardize null-timestamp handling, doesn't document the `referenceTime` pinning pattern for tests, and doesn't appear in IntelliSense for discoverability. Built-in is justified.

---

## Decision 6: Count-Based Quotas — Design Phase Only

**Decision:** Design/spec phase only. No implementation until the spec is complete and reviewed.

**What the design phase produces:**
A spec decision record (not a code change) covering:
1. Count quota algorithm for `QuotaSlice` / `GreedySlice` integration
2. Tag-based count quota semantics: if an item has tags `["critical", "urgent"]`, does it count toward `RequireAtLeast(tag: "critical")` and `RequireAtLeast(tag: "urgent")` simultaneously? (Non-exclusivity is the likely answer, but must be specified.)
3. Pinned item + count quota interaction: pinned items satisfy their own count quotas; if pinned items alone don't meet the minimum and budget can't supply enough selectable items, the outcome must be specified (throw? proceed? log warning?)
4. Run-time vs. build-time conflict detection: `RequireAtLeast(2).Cap(1)` is a build-time catch; `RequireAtLeast(2)` with only 1 candidate in the item set is a run-time condition. Both must be specified.
5. Count quota + `KnapsackSlice` interaction: mandatory item inclusion in knapsack is a constrained variant. Determine if this requires a separate implementation path or a clean generalization, before committing to either.

**Why deferred:**
The March 10 brainstorm explicitly deferred count-based quotas because "the interaction with pinned items and token budgets not fully designed." That design problem is still unresolved. The percentage-based quota system (shipped as `QuotaSlice`) is complete. Count-based quotas require a dedicated design decision record before any implementation begins — shipping them without it risks locking in semantics that need to be revised.

---

## Phase Ordering (Recommended)

| Phase | Deliverables | Notes |
|-------|-------------|-------|
| A | `DecayScorer` spec chapter + implementation | No inter-dependencies; spec + code can be one phase |
| A | `SelectionReport` extension methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`) | Small, additive, no new types |
| B | Rust diagnostics spec chapter (language-agnostic) | Must precede Rust implementation |
| B | `Cupel.Testing` vocabulary design (spec section, 10-15 patterns) | Must precede implementation |
| C | Rust diagnostics implementation | Depends on B spec |
| C | `Cupel.Testing` implementation | Depends on B vocabulary |
| C | OpenTelemetry package | Depends on verbosity-level spec |
| D | Budget simulation (`GetMarginalItems`, `FindMinBudgetFor`) | Depends on C (uses DryRun, needs stable SelectionReport) |
| E | Count quota design decision record | No code; pure design |
| F | Count quota implementation | Depends on E spec review |

---

## Rejected / Reshaped Items

| Item | Outcome | Reason |
|------|---------|--------|
| `ContextAnalyzer` / `ContextPressure` type | Reshaped → 3 extension methods | Wrapper type adds allocation and API surface with no justified consumer for "pressure as a unit" |
| `SweepBudget` in Cupel | Moved to Smelt | Linear budget stepping encodes consumer-specific assumptions; Smelt knows its provider's window sizes |
| `IDecayCurve` public interface | Made internal | `IScorer` is the extensibility contract; no usage data to justify a second extension point |
| Snapshot/ApprovalTests in `Cupel.Testing` | Removed | `SelectionReport` ordering stability for ties is a precondition; ordering guarantee not yet shipped |
| FluentAssertions dependency | Removed | Heavy test infrastructure; in-house chain plumbing is ~100 lines |
| Count-Based Quotas implementation | Deferred to design phase | Design problems (tag non-exclusivity, pinned interaction, knapsack path) explicitly unresolved |
| `DecayScorer` mandatory constructor `referenceTime` | Replaced by `TimeProvider` | Constructor-captured time is stale in long-lived pipelines; `TimeProvider.GetUtcNow()` per invocation is fresh and deterministic in tests |

---

## Key Architectural Decisions (Surfaced by Debate)

1. **Spec-first for Rust diagnostics** — implementation must not lead spec; Rust lifetime constraints will bias API toward Rust ergonomics if spec is written after
2. **`TimeProvider` injection for time-dependent scorers** — `_timeProvider.GetUtcNow()` per `Score()` invocation; no constructor-time clock capture
3. **`IDecayCurve` internal** — `IScorer` is the single extensibility contract; second extension points need usage data to justify
4. **Extension methods over wrapper types for analytics** — `SelectionReport` extension methods deliver analytical value without committing to a new public type
5. **`FindMinBudgetFor` requires monotonicity guarantee** — runtime guard against `QuotaSlice`, documented explicitly
6. **OTEL `stage-only` default** — item-level events are opt-in; cardinality explosion is a production risk at scale
7. **`cupel.*` OTEL namespace is pre-stable** — explicitly document; do not chase `gen_ai.*` until SIG stabilizes
