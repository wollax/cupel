# Cupel Roadmap

## Milestones

| Milestone | Status | Phases |
|-----------|--------|--------|
| v1.0 Core Library | 🔄 current | 1–10 |

---

### v1.0 Core Library (In Progress)

#### Phase 1: Project Scaffold & Core Models

**Goal:** Establish the project infrastructure and load-bearing core types that every subsequent phase depends on. Getting these wrong creates breaking changes.

**Dependencies:** None

**Requirements:** PIPE-01, PIPE-02, API-05, JSON-01, PKG-01

**Success Criteria:**
1. Solution builds with `TreatWarningsAsErrors`, `Nullable enable`, SourceLink, and `Microsoft.CodeAnalysis.PublicApiAnalyzers` enforced across all projects
2. `ContextItem` is immutable (`{ get; init; }`), sealed, with `[JsonPropertyName]` on all public properties, and compiles with zero warnings
3. `ContextBudget` model validates inputs (non-negative tokens, margin percentage 0–100) and exposes all budget parameters
4. `ContextItem.Tokens` is a required non-nullable `int` — no tokenizer dependency exists in the core package
5. `dotnet list Wollax.Cupel package` returns zero external dependencies; BenchmarkDotNet project exists with empty-pipeline baseline

**Plans:** 5 plans

Plans:
- [ ] 01-01-PLAN.md — Solution scaffold and build infrastructure
- [ ] 01-02-PLAN.md — Smart enums (ContextKind, ContextSource) via TDD
- [ ] 01-03-PLAN.md — ContextItem sealed record via TDD
- [ ] 01-04-PLAN.md — ContextBudget validated model via TDD
- [ ] 01-05-PLAN.md — Benchmark baseline and PublicAPI verification

---

#### Phase 2: Interfaces & Diagnostics Infrastructure

**Goal:** Define all pipeline stage contracts and build the tracing infrastructure that every component will use. These interfaces are the API's load-bearing surface — they must be right before any implementations ship.

**Dependencies:** Phase 1 (core models)

**Requirements:** SCORE-01, SLICE-01, PLACE-01, API-04, TRACE-01, TRACE-02, TRACE-03, TRACE-04

**Success Criteria:**
1. `IScorer`, `ISlicer`, `IPlacer`, and `IContextSource` interfaces compile and are documented with XML doc comments
2. `ContextResult` return type exists with `Items` and optional `ContextTrace` from day one
3. `NullTraceCollector` (singleton, no-op) and `DiagnosticTraceCollector` both implement `ITraceCollector`
4. Trace event construction is provably gated — a benchmark with `NullTraceCollector` shows zero allocations from trace paths
5. Trace propagation is explicit (parameter passing) — no `AsyncLocal` or ambient state anywhere in the codebase

---

#### Phase 3: Individual Scorers

**Goal:** Implement all six built-in scorers as pure, stateless functions. Each scorer is independently testable with ordinal relationship assertions.

**Dependencies:** Phase 2 (IScorer interface)

**Requirements:** SCORE-02, SCORE-03, SCORE-04, SCORE-05, SCORE-06, SCORE-07

**Success Criteria:**
1. All six scorers (`RecencyScorer`, `PriorityScorer`, `KindScorer`, `TagScorer`, `FrequencyScorer`, `ReflexiveScorer`) implement `IScorer` and produce output conventionally in 0.0–1.0
2. `RecencyScorer` uses relative timestamp ranking within the input set (not absolute distance from `DateTime.Now`)
3. Each scorer has tests asserting ordinal relationships ("A scores higher than B"), not exact floating-point values
4. No LINQ, closure captures, or boxing in any scorer's `Score()` method — verified by benchmark with `[MemoryDiagnoser]` showing zero Gen0 collections

---

#### Phase 4: Composite Scoring

**Goal:** Enable multi-signal scoring through composition. `CompositeScorer` is the mechanism that makes Cupel's scoring genuinely powerful — weighted combination of arbitrary scorer trees.

**Dependencies:** Phase 3 (individual scorers)

**Requirements:** SCORE-08, SCORE-09

**Success Criteria:**
1. `CompositeScorer` combines multiple scorers with `WeightedAverage` aggregation and normalizes weights internally (relative, not absolute)
2. Nested `CompositeScorer` instances compose correctly — a composite containing a composite produces valid ordinal rankings
3. `ScaledScorer` wraps any scorer and normalizes its output to 0–1 range
4. Stable sort with secondary tiebreaking (timestamp or insertion index) is used — verified by test with items that produce identical composite scores

---

#### Phase 5: Pipeline Assembly & Basic Execution

**Goal:** Wire the fixed pipeline together: Classify → Score → Deduplicate → Slice → Place. This phase delivers the first end-to-end context selection with `GreedySlice`, `UShapedPlacer`, and the fluent builder API.

**Dependencies:** Phase 4 (CompositeScorer), Phase 2 (ISlicer, IPlacer, ITraceCollector)

**Requirements:** PIPE-03, PIPE-04, PIPE-05, SLICE-02, PLACE-02, PLACE-03, API-02

**Success Criteria:**
1. `CupelPipeline.CreateBuilder()` produces a builder that validates at `Build()` time and returns a working pipeline
2. Pipeline executes stages in fixed order (Classify → Score → Deduplicate → Slice → Place) — no stage reordering is possible
3. Pinned items bypass scoring entirely and enter at the Placer stage — verified by test showing pinned items appear in output regardless of score
4. `GreedySlice` fills budget by score/token ratio in O(N log N); `UShapedPlacer` places high-scoring items at start and end; `ChronologicalPlacer` orders by timestamp
5. Full pipeline benchmark with 100/250/500 items completes under 1ms; tracing-disabled path shows zero Gen0 allocations

---

#### Phase 6: Advanced Slicers & Quota System

**Goal:** Deliver the optimization and constraint slicers that differentiate Cupel from naive truncation. `KnapsackSlice` provides provably optimal selection; `QuotaSlice` enforces semantic balance; `StreamSlice` handles unbounded sources.

**Dependencies:** Phase 5 (pipeline assembly, GreedySlice as reference)

**Requirements:** SLICE-03, SLICE-04, SLICE-05, SLICE-06

**Success Criteria:**
1. `KnapsackSlice` with 100-token bucket discretization produces selections with equal or better budget utilization than `GreedySlice` on test cases — and the trade-off is documented
2. `QuotaSlice` enforces `Require(Kind, minPercent)` and `Cap(Kind, maxPercent)` constraints, rejecting configurations where quotas exceed 100%
3. `StreamSlice` processes `IAsyncEnumerable` sources without materializing the full collection
4. Pinned items that conflict with quota constraints produce clear, actionable error messages — not silent failures

---

#### Phase 7: Explainability & Overflow Handling

**Goal:** Ship the killer differentiator: every inclusion and exclusion has a traceable reason. `SelectionReport`, `DryRun()`, and `OverflowStrategy` make Cupel's decisions transparent and controllable.

**Dependencies:** Phase 5 (working pipeline), Phase 2 (ContextTrace, ITraceCollector)

**Requirements:** TRACE-05, TRACE-06

**Success Criteria:**
1. `SelectionReport` lists included items with their scores and excluded items each with an `ExclusionReason` enum value
2. `DryRun()` returns the full report without side effects — calling it twice with the same input produces identical results
3. `OverflowStrategy.Throw` raises on budget overflow, `Truncate` truncates excess items, `Proceed` continues with optional observer callback invoked
4. Observer callback receives overflow details (how many tokens over budget, which items caused it)

---

#### Phase 8: Policy System & Named Presets

**Goal:** Lower the adoption floor with declarative policies and opinionated presets. Policies are the primary way most users will configure Cupel — they tie scorer weights, slicer choice, and placer together into a single serializable unit.

**Dependencies:** Phase 5 (pipeline builder), Phase 4 (CompositeScorer), Phase 6 (all slicers)

**Requirements:** API-01, API-03, POLICY-01, POLICY-02, POLICY-03

**Success Criteria:**
1. `CupelPolicy` is a declarative config object that fully specifies a pipeline (scorers + weights, slicer, placer, budget, quotas)
2. `CupelOptions.AddPolicy("intent", policy)` enables intent-based lookup; explicit policy construction also works
3. 7+ named presets exist (chat, code-review, rag, document-qa, tool-use, long-running, debugging) — each marked `[Experimental]`
4. Named presets compile, produce valid pipelines, and serve as test fixtures — each preset has at least one integration test

---

#### Phase 9: Serialization & JSON Package

**Goal:** Enable policies to be stored, shared, and loaded from JSON. The `Wollax.Cupel.Json` package provides source-generated serialization with polymorphic type discriminators for scorer/slicer/placer configs.

**Dependencies:** Phase 8 (CupelPolicy), Phase 4 (CompositeScorer stabilized)

**Requirements:** JSON-02, JSON-03, JSON-04, PKG-04

**Success Criteria:**
1. `ContextBudget` and slicer configs round-trip through JSON serialization without data loss
2. `RegisterScorer(string name, Func<IScorer> factory)` hook exists for consumer-defined scorers to participate in deserialization
3. `CupelJsonContext` is a source-generated `JsonSerializerContext` with polymorphic `$type` discriminators
4. No YAML or other format support exists — JSON is the only serialization format

---

#### Phase 10: Companion Packages & Release

**Goal:** Ship the complete package suite to nuget.org. DI integration, Tiktoken companion, and the publish pipeline complete the v1.0 deliverable.

**Dependencies:** Phase 9 (JSON package), Phase 5+ (core pipeline)

**Requirements:** PKG-02, PKG-03, PKG-05

**Success Criteria:**
1. `Wollax.Cupel.Extensions.DependencyInjection` provides `AddCupel()` with `IOptions<CupelOptions>`, keyed services for named pipelines, and correct lifetimes (transient pipeline/trace, singleton scorers/slicers/placers)
2. `Wollax.Cupel.Tiktoken` bridges `Microsoft.ML.Tokenizers` and works on .NET 10
3. Consumption tests run against packed `.nupkg` files (not `<ProjectReference>`) and pass in CI
4. All four packages publish to nuget.org with identical version, SourceLink enabled, NuGet Trusted Publishing via OIDC
5. `PublicAPI.Shipped.txt` is finalized for v1.0.0

---

## Progress Summary

| Phase | Name | Requirements | Status |
|-------|------|-------------|--------|
| 1 | Project Scaffold & Core Models | PIPE-01, PIPE-02, API-05, JSON-01, PKG-01 | ○ planned |
| 2 | Interfaces & Diagnostics Infrastructure | SCORE-01, SLICE-01, PLACE-01, API-04, TRACE-01, TRACE-02, TRACE-03, TRACE-04 | ○ planned |
| 3 | Individual Scorers | SCORE-02, SCORE-03, SCORE-04, SCORE-05, SCORE-06, SCORE-07 | ○ planned |
| 4 | Composite Scoring | SCORE-08, SCORE-09 | ○ planned |
| 5 | Pipeline Assembly & Basic Execution | PIPE-03, PIPE-04, PIPE-05, SLICE-02, PLACE-02, PLACE-03, API-02 | ○ planned |
| 6 | Advanced Slicers & Quota System | SLICE-03, SLICE-04, SLICE-05, SLICE-06 | ○ planned |
| 7 | Explainability & Overflow Handling | TRACE-05, TRACE-06 | ○ planned |
| 8 | Policy System & Named Presets | API-01, API-03, POLICY-01, POLICY-02, POLICY-03 | ○ planned |
| 9 | Serialization & JSON Package | JSON-02, JSON-03, JSON-04, PKG-04 | ○ planned |
| 10 | Companion Packages & Release | PKG-02, PKG-03, PKG-05 | ○ planned |
