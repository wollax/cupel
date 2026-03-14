# Cupel Roadmap

## Milestones

| Milestone | Status | Phases |
|-----------|--------|--------|
| v1.0 Core Library | 🔧 gap closure | 1–15 |
| v1.1 Rust Crate Migration & crates.io Publishing | ○ planned | 16–21 |

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
- [x] 01-01-PLAN.md — Solution scaffold and build infrastructure
- [x] 01-02-PLAN.md — Smart enums (ContextKind, ContextSource) via TDD
- [x] 01-03-PLAN.md — ContextItem sealed record via TDD
- [x] 01-04-PLAN.md — ContextBudget validated model via TDD
- [x] 01-05-PLAN.md — Benchmark baseline and PublicAPI verification

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

**Plans:** 2 plans in 2 waves

Plans:
- [x] 02-01-PLAN.md — Tracing infrastructure and supporting types (TDD): enums, TraceEvent, ScoredItem, ITraceCollector, NullTraceCollector, DiagnosticTraceCollector
- [x] 02-02-PLAN.md — Pipeline interfaces, ContextResult, and zero-allocation benchmark (TDD): IScorer, ISlicer, IPlacer, IContextSource, ContextResult, SelectionReport, PublicAPI, benchmark

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

**Plans:** 3 plans in 2 waves

Plans:
- [x] 03-01-PLAN.md — Rank-based and passthrough scorers (RecencyScorer, PriorityScorer, ReflexiveScorer)
- [x] 03-02-PLAN.md — Lookup-based and frequency scorers (KindScorer, TagScorer, FrequencyScorer)
- [x] 03-03-PLAN.md — Scorer zero-allocation benchmark

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

**Plans:** 3 plans in 2 waves

Plans:
- [x] 04-01-PLAN.md — CompositeScorer with weighted average and cycle detection
- [x] 04-02-PLAN.md — ScaledScorer with min-max normalization
- [x] 04-03-PLAN.md — Stable sort tiebreaking test and composite scorer benchmark

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

**Plans:** 3 plans in 3 waves

Plans:
- [x] 05-01-PLAN.md — GreedySlice, UShapedPlacer, and ChronologicalPlacer implementations (TDD)
- [x] 05-02-PLAN.md — CupelPipeline, PipelineBuilder, and end-to-end pipeline execution (TDD)
- [x] 05-03-PLAN.md — Full pipeline benchmark with zero-allocation verification

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

**Plans:** 5 plans in 3 waves

Plans:
- [x] 06-01-PLAN.md — KnapsackSlice with 0/1 DP and bucket discretization (wave 1)
- [x] 06-02-PLAN.md — QuotaSlice decorator with Require/Cap constraints and QuotaBuilder (wave 1)
- [x] 06-03-PLAN.md — IAsyncSlicer interface and StreamSlice implementation (wave 1)
- [x] 06-04-PLAN.md — Builder integration and pipeline dispatch for new slicers (wave 2)
- [x] 06-05-PLAN.md — Slicer benchmarks: KnapsackSlice, QuotaSlice, StreamSlice (wave 3)

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

**Plans:** 3 plans in 3 waves

Plans:
- [x] 07-01-PLAN.md — Explainability and overflow data types (TDD): enums, records, ReportBuilder (wave 1)
- [x] 07-02-PLAN.md — Pipeline integration: ExecuteCore, exclusion tracking, overflow handling, DryRun (wave 2)
- [x] 07-03-PLAN.md — PublicAPI surface update and end-to-end integration tests (wave 3)

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

**Plans:** 3 plans in 2 waves

Plans:
- [x] 08-01-PLAN.md — Enums (ScorerType, SlicerType, PlacerType), ScorerEntry, QuotaEntry, CupelPolicy sealed class (TDD) (wave 1)
- [x] 08-02-PLAN.md — CupelPresets (7 named presets with [Experimental]) and CupelOptions intent-based registry (TDD) (wave 2)
- [x] 08-03-PLAN.md — PipelineBuilder.WithPolicy() integration and end-to-end policy integration tests (wave 2)

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

**Plans:** 3 plans in 2 waves

Plans:
- [x] 09-01-PLAN.md — JSON package scaffold, CupelJsonContext, CupelJsonSerializer, round-trip tests
- [x] 09-02-PLAN.md — Custom scorer registration and unknown-type error handling
- [x] 09-03-PLAN.md — Path-aware validation error handling

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

**Plans:** 3 plans in 2 waves

Plans:
- [x] 10-01-PLAN.md — DI integration package (AddCupel, keyed services, lifetime tests)
- [x] 10-02-PLAN.md — Tiktoken bridge package (TiktokenTokenCounter, WithTokenCount)
- [x] 10-03-PLAN.md — CI/CD workflows, consumption tests, PublicAPI finalization

---

#### Phase 11: Language-Agnostic Specification

**Goal:** Document Cupel's context selection algorithm as a formal, language-agnostic specification that consumers can implement in any language. This becomes the canonical reference for all implementations (C#, Rust, etc.).

**Dependencies:** Phase 5 (pipeline assembly — need working end-to-end pipeline to specify against)

**Requirements:** TBD

**Success Criteria:**
1. Specification document covers all pipeline stages (Classify → Score → Deduplicate → Slice → Place) with precise algorithmic descriptions
2. Scorer contracts, slicer strategies, and placer algorithms are defined independently of any language runtime
3. Test vectors / conformance suite defined so implementations can validate correctness
4. Specification versioned and published (e.g., as a standalone document or GitHub Pages)

**Plans:** 3 plans in 3 waves

Plans:
- [x] 11-01-PLAN.md — mdBook scaffold, introduction, data model, and pipeline stage chapters (wave 1)
- [x] 11-02-PLAN.md — Scorer, slicer, and placer algorithm specification chapters (wave 2)
- [x] 11-03-PLAN.md — Conformance test suite (TOML vectors), conformance chapters, and GitHub Pages workflow (wave 3)

---

#### Phase 12: Rust Crate (Assay)

**Goal:** Implement the Cupel specification as a Rust crate (`assay`) for use by [Assay](https://github.com/wollax/assay) and other Rust projects. This is the first non-C# implementation and validates the specification's language-independence.

**Dependencies:** Phase 11 (specification — implement against the spec, not the C# code)

**Requirements:** TBD

**Success Criteria:**
1. `assay` crate implements the core pipeline stages defined in the specification
2. Passes the conformance test vectors from the specification
3. Published to crates.io with documentation
4. Assay project can consume the crate as a dependency

**Plans:** 3 plans in 3 waves

Plans:
- [x] 12-01-PLAN.md — Crate scaffold, data model types, error enum, Scorer trait, and all 8 scorer implementations (wave 1)
- [x] 12-02-PLAN.md — Slicer trait, 3 slicers, Placer trait, 2 placers, pipeline stages, and Pipeline orchestrator (wave 2)
- [x] 12-03-PLAN.md — Conformance test suite: copy 28 TOML vectors, build test runner, pass all required tests (wave 3)

---

#### Phase 13: Budget Contract Implementation

**Goal:** Wire the two unused `ContextBudget` properties into the pipeline so the public API contract matches runtime behavior. `ReservedSlots` subtracts per-kind token reservations from the slicer's available budget; `EstimationSafetyMarginPercent` applies a safety margin to the effective token ceiling.

**Dependencies:** Phase 5 (pipeline execution), Phase 6 (slicers)

**Requirements:** (audit gap closure — no new requirement IDs)

**Gap Closure:** Closes audit items:
- `ContextBudget.ReservedSlots` property exists in public API but pipeline ignores it
- `ContextBudget.EstimationSafetyMarginPercent` property exists in public API but pipeline ignores it
- `REQUIREMENTS.md` shows PKG-02, PKG-03, PKG-05 as `[ ] planned` despite being implemented

**Success Criteria:**
1. Pipeline execution subtracts `ReservedSlots` token totals from the slicer's available budget — verified by test showing reduced capacity
2. Pipeline execution applies `EstimationSafetyMarginPercent` as a multiplicative reduction to effective budget — verified by test showing margin-adjusted selection
3. Existing tests continue to pass (default values are zero/empty, preserving backward compatibility)
4. `REQUIREMENTS.md` checkboxes updated for PKG-02, PKG-03, PKG-05

**Plans:** 2 plans in 2 waves

Plans:
- [x] 13-01-PLAN.md — ReservedSlots & EstimationSafetyMarginPercent pipeline wiring (TDD)
- [x] 13-02-PLAN.md — Spec documentation updates for budget contract

---

#### Phase 14: Policy Type Completeness

**Goal:** Make `ScaledScorer` and `StreamSlice` reachable from the declarative policy, JSON serialization, and DI paths — closing the integration gap where these components are only usable via manual builder. Align DI lifetimes to the documented singleton specification.

**Dependencies:** Phase 8 (CupelPolicy), Phase 9 (JSON serialization), Phase 10 (DI package)

**Requirements:** (audit gap closure — no new requirement IDs)

**Gap Closure:** Closes audit items:
- `ScaledScorer` has no `ScorerType` enum value — unreachable from policy/JSON/DI paths
- `StreamSlice` has no `SlicerType` enum value — unreachable from policy/DI paths
- DI lifetime divergence: ROADMAP specifies singleton scorers/slicers/placers but implementation creates per-resolve

**Success Criteria:**
1. `ScorerType.Scaled` enum value exists; `CupelPolicy` can declare a `ScaledScorer` wrapping any inner scorer
2. `SlicerType.Stream` enum value exists; `CupelPolicy` can declare `StreamSlice` configuration
3. JSON round-trip of policies containing `ScaledScorer` and `StreamSlice` succeeds
4. DI-resolved scorers, slicers, and placers are singletons — verified by reference equality test
5. All existing tests continue to pass

**Plans:** 3 plans in 2 waves

Plans:
- [x] 14-01-PLAN.md — Enum extensions, ScorerEntry/CupelPolicy model changes, PipelineBuilder wiring (wave 1)
- [x] 14-02-PLAN.md — JSON serialization: BuiltInScorerTypes enum-derived refactor and round-trip tests (wave 1)
- [x] 14-03-PLAN.md — DI singleton lifetime fix, InternalsVisibleTo, PublicAPI updates (wave 2)

---

#### Phase 15: Conformance Hardening

**Goal:** Strengthen the conformance guarantee by promoting `QuotaSlice` to the required tier and backfilling missing verification documents for phases 01 and 09.

**Dependencies:** Phase 11 (specification), Phase 12 (Rust crate)

**Requirements:** (audit gap closure — no new requirement IDs)

**Gap Closure:** Closes audit items:
- `quota-basic.toml` is in optional tier instead of required — weakens conformance for most complex slicer
- Missing VERIFICATION.md for Phase 01
- Missing VERIFICATION.md for Phase 09

**Success Criteria:**
1. `quota-basic.toml` moved from `conformance/optional/` to `conformance/required/` in the spec
2. Rust conformance test runner updated to include the new required vector — all required tests pass
3. VERIFICATION.md exists for Phase 01 with retroactive verification notes
4. VERIFICATION.md exists for Phase 09 with retroactive verification notes

**Plans:** TBD

---

### v1.1 Rust Crate Migration & crates.io Publishing (Planned)

#### Phase 16: Pre-flight & Crate Scaffold

**Goal:** Establish all pre-conditions before moving any Rust source files. Verify the `cupel-rs` crate name is available on crates.io, decide workspace layout and conformance vector strategy, write a complete standalone `Cargo.toml`, and configure Rust toolchain files. This phase is entirely pre-code gates — getting these wrong produces hard-to-reverse consequences.

**Dependencies:** Phase 15 (Conformance Hardening — `quota-basic.toml` promoted to required tier, conformance vector vendoring must account for this)

**Requirements:** MIGRATE-01, MIGRATE-02, MIGRATE-03, MIGRATE-04, MIGRATE-05

**Success Criteria:**
1. `cupel-rs` name availability verified on crates.io (or fallback name selected if squatted)
2. `rust-toolchain.toml` at repo root pins Rust 2024 edition with MSRV 1.85, includes `rustfmt` and `clippy` components
3. `.editorconfig` extended with Rust-specific rules; `.gitignore` includes `/crates/cupel/target/`
4. Standalone `Cargo.toml` at `crates/cupel/` with all required fields (`name`, `version`, `edition`, `rust-version`, `license`, `repository`, `description`, `categories`, `keywords`, `include`) and chosen version strategy — no workspace-inherited fields
5. `cargo check --manifest-path crates/cupel/Cargo.toml` passes on an empty `lib.rs` placeholder

**Plans:** TBD

---

#### Phase 17: Crate Migration & Conformance Verification

**Goal:** Move all Rust source files from `wollax/assay` into `crates/cupel/src/`, update conformance test vector paths, and verify the complete crate compiles, passes all conformance tests, and packages correctly for publishing.

**Dependencies:** Phase 16 (crate scaffold exists)

**Requirements:** MIGRATE-06, MIGRATE-07, CONFORM-01, CONFORM-02, CONFORM-03

**Success Criteria:**
1. All 26 `.rs` source files from `assay/crates/assay-cupel/src/` live at `cupel/crates/cupel/src/` and compile cleanly
2. Conformance test runner resolves vectors from `conformance/required/` (including promoted `quota-basic.toml`) via `CARGO_MANIFEST_DIR`-relative path with CI diff guard preventing divergence
3. `cargo fmt --check`, `cargo clippy -- -D warnings`, and `cargo test` all pass with 28+ conformance vectors (28 original + promoted quota)
4. `cargo package --list` confirms all `.toml` conformance vectors appear in the tarball
5. Unpacked tarball verification: `tar xvf *.crate && cargo test` passes inside the unpacked directory

**Plans:** TBD

---

#### Phase 18: Dual-Language CI

**Goal:** Wire Rust CI into GitHub Actions alongside the existing .NET workflows. Separate workflow files with path filters ensure Rust changes trigger Rust CI and .NET changes trigger .NET CI. Release pipeline verified with dry-run before first publish.

**Dependencies:** Phase 17 (crate compiles and tests pass locally)

**Requirements:** CI-01, CI-02, CI-04, CI-05

**Success Criteria:**
1. `ci-rust.yml` triggers on PRs touching `crates/**`, `conformance/**`, `rust-toolchain.toml`, and self-referencing workflow path; runs `cargo fmt --check`, `cargo clippy -- -D warnings`, `cargo test`, and `cargo-deny check`
2. `release-rust.yml` with `workflow_dispatch` trigger, `dry-run` input, and `release` GitHub environment completes dry-run without error
3. Existing `.NET` CI workflow has path filters so Rust-only changes do not trigger .NET builds
4. `cargo-deny` configuration (`deny.toml`) exists and passes in CI
5. GitHub branch protection accepts skipped Rust CI status check on .NET-only PRs (and vice versa)

**Plans:** TBD

---

#### Phase 19: First Publish & Assay Switchover

**Goal:** Publish `cupel-rs` to crates.io, configure OIDC trusted publishing for future releases, and update `wollax/assay` to consume the crate from the registry instead of a path dependency. Delete the old `assay-cupel` directory from assay after verification.

**Dependencies:** Phase 18 (CI passing, release workflow verified dry-run)

**Requirements:** CI-03, SWITCH-01, SWITCH-02, SWITCH-03, SWITCH-04

**Success Criteria:**
1. `cupel-rs` is live on crates.io with correct metadata (readme rendered, categories visible, docs.rs build triggered)
2. OIDC trusted publishing configured on crates.io settings page — subsequent publishes use `release-rust.yml` workflow without personal API tokens
3. `wollax/assay` replaces `assay-cupel` path dependency with `cupel-rs = "VERSION"` from registry; imports renamed; all assay tests pass
4. `assay/crates/assay-cupel/` directory deleted from assay repo
5. `[patch.crates-io]` local development pattern documented in assay contributing guide

**Plans:** TBD

---

#### Phase 20: Serde Feature Flag

**Goal:** Add optional `serde` feature flag gating `Serialize`/`Deserialize` derives on all public data types. This is the highest-value differentiator for Rust consumers — most LLM-application users need serialization. Requires careful `ContextBudget` custom deserializer to maintain constructor validation invariants.

**Dependencies:** Phase 19 (crate published — this is a post-publish enhancement)

**Requirements:** ENHANCE-01, ENHANCE-02, ENHANCE-03

**Success Criteria:**
1. `features = ["serde"]` in `Cargo.toml` gates `serde::Serialize` and `serde::Deserialize` derives on all public data types (`ContextItem`, `ContextBudget`, `ContextKind`, `ContextSource`, `ScoredItem`, etc.)
2. `ContextBudget` uses a custom deserializer that validates inputs through the constructor — blind deserialization around validation is not possible
3. `cargo test` passes with `--features serde` and without (feature is additive, not breaking)
4. Crate re-published to crates.io as a minor version bump with the new feature

**Plans:** TBD

---

#### Phase 21: docs.rs Documentation & Examples

**Goal:** Make `cupel-rs` discoverable and approachable on docs.rs with crate-level quickstart documentation, module-level doc comments, and runnable examples. This is the single largest factor in new-user conversion from the docs.rs landing page.

**Dependencies:** Phase 20 (serde feature — docs should document the serde feature)

**Requirements:** ENHANCE-04, ENHANCE-05

**Success Criteria:**
1. Crate-level documentation in `lib.rs` includes a quickstart example that compiles as a doctest
2. Every public module has a `//!` module-level doc comment explaining its purpose; `[package.metadata.docs.rs]` configured with `all-features = true`
3. `examples/basic_pipeline.rs` exists and runs with `cargo run --example basic_pipeline`
4. `cargo doc --no-deps --all-features` builds with zero warnings

**Plans:** TBD

---

## Progress Summary

| Phase | Name | Requirements | Status |
|-------|------|-------------|--------|
| 1 | Project Scaffold & Core Models | PIPE-01, PIPE-02, API-05, JSON-01, PKG-01 | ● complete |
| 2 | Interfaces & Diagnostics Infrastructure | SCORE-01, SLICE-01, PLACE-01, API-04, TRACE-01, TRACE-02, TRACE-03, TRACE-04 | ● complete |
| 3 | Individual Scorers | SCORE-02, SCORE-03, SCORE-04, SCORE-05, SCORE-06, SCORE-07 | ● complete |
| 4 | Composite Scoring | SCORE-08, SCORE-09 | ● complete |
| 5 | Pipeline Assembly & Basic Execution | PIPE-03, PIPE-04, PIPE-05, SLICE-02, PLACE-02, PLACE-03, API-02 | ● complete |
| 6 | Advanced Slicers & Quota System | SLICE-03, SLICE-04, SLICE-05, SLICE-06 | ● complete |
| 7 | Explainability & Overflow Handling | TRACE-05, TRACE-06 | ● complete |
| 8 | Policy System & Named Presets | API-01, API-03, POLICY-01, POLICY-02, POLICY-03 | ● complete |
| 9 | Serialization & JSON Package | JSON-02, JSON-03, JSON-04, PKG-04 | ● complete |
| 10 | Companion Packages & Release | PKG-02, PKG-03, PKG-05 | ● complete |
| 11 | Language-Agnostic Specification | TBD | ● complete |
| 12 | Rust Crate (Assay) | TBD | ● complete |
| 13 | Budget Contract Implementation | audit gap closure | ● complete |
| 14 | Policy Type Completeness | audit gap closure | ● complete |
| 15 | Conformance Hardening | audit gap closure | ○ planned |
| 16 | Pre-flight & Crate Scaffold | MIGRATE-01–05 | ○ planned |
| 17 | Crate Migration & Conformance Verification | MIGRATE-06–07, CONFORM-01–03 | ○ planned |
| 18 | Dual-Language CI | CI-01–02, CI-04–05 | ○ planned |
| 19 | First Publish & Assay Switchover | CI-03, SWITCH-01–04 | ○ planned |
| 20 | Serde Feature Flag | ENHANCE-01–03 | ○ planned |
| 21 | docs.rs Documentation & Examples | ENHANCE-04–05 | ○ planned |
