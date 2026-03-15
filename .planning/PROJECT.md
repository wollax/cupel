# Cupel

## What This Is

Cupel is a .NET context management library for coding agents. Given a set of context items (messages, documents, tool outputs, memory) and a token budget, Cupel determines the optimal context window — maximizing information density while respecting the attention mechanics of any LLM. It serves both autonomous agent orchestration (Smelt spawning subagents) and interactive human-agent sessions.

Part of the Wollax agentic development stack: **Assay** (spec-driven development) → **Smelt** (orchestration) → **Cupel** (context management).

## Current State

**Shipped:** v1.0 Core Library (2026-03-14)
- 4,303 lines of C# across 56 source files
- 11,175 lines of test code across 49 test files (641 tests)
- 4 NuGet packages: Core, DI Extensions, Tiktoken, Json
- Language-agnostic specification with 28 conformance test vectors
- Rust crate implementation (assay-cupel) passing all conformance vectors

## Current Milestone: v1.1 Rust Crate Migration & crates.io Publishing

**Goal:** Pull the `assay-cupel` Rust crate from `wollax/assay` into the cupel monorepo, publish as `cupel-rs` on crates.io, and have assay reference it as a crates.io consumer.

**Target features:**

- Rust crate at `crates/cupel/` in the cupel repo (standalone, no Cargo workspace)
- Published to crates.io as `cupel-rs` v1.0.0 (spec-aligned versioning)
- Conformance test vectors: single source of truth at `conformance/`, vendored copy in crate with CI drift guard
- Dual-language CI/CD (Rust jobs added to GitHub Actions)
- `rust-toolchain.toml` at repo root, `.editorconfig` extended for Rust
- Assay updated to consume `cupel-rs` from crates.io (not path dependency)
- `crates/assay-cupel/` deleted from assay repo after migration verified
- Local dev workflow documented (`[patch.crates-io]` pattern)

## Core Value

Given candidates and a budget, return the optimal context selection with full explainability — every inclusion and exclusion has a traceable reason.

## Requirements

### Validated

- ContextItem model with Content, Kind, Tokens, Timestamp, Source, Tags, Priority, Pinned, OriginalTokens, FutureRelevanceHint, Metadata — v1.0
- ContextBudget model with MaxTokens, TargetTokens, ReservedSlots, OutputReserve, EstimationSafetyMarginPercent — v1.0
- Fixed pipeline: Classify → Score → Deduplicate → Slice → Place — v1.0
- Ordinal-only scoring invariant — v1.0
- Pinned items bypass scoring — v1.0
- IScorer interface with 6 built-in scorers + CompositeScorer + ScaledScorer — v1.0
- ISlicer interface with GreedySlice, KnapsackSlice, QuotaSlice, StreamSlice — v1.0
- IPlacer interface with UShapedPlacer and ChronologicalPlacer — v1.0
- ContextResult, SelectionReport, DryRun, OverflowStrategy explainability — v1.0
- CupelPolicy declarative config + 7 named presets + CupelOptions intent-based lookup — v1.0
- Fluent builder API via CupelPipeline.CreateBuilder() — v1.0
- IContextSource interface (IAsyncEnumerable<ContextItem>) — v1.0
- JSON serialization with source-generated JsonSerializerContext — v1.0
- 4 NuGet packages (Core, DI, Tiktoken, Json) published to nuget.org — v1.0

### Active

(See REQUIREMENTS.md for v1.1 requirements)

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

## Context

**Ecosystem position**: Third tool in the Wollax agentic development stack. Assay (specs) and Smelt (orchestration) are existing public GitHub repos. Cupel completes the trilogy — the metallurgical assaying metaphor (test the ore → extract the metal → refine the output).

**Problem**: Context windows are finite, degrading resources. LLM performance degrades before theoretical limits (Anthropic data: 17-point MRCR drop at 1M context). "Lost in the middle" phenomenon means placement matters. Tool outputs consume 80%+ of tokens in typical agent trajectories. Multi-agent orchestration multiplies the problem. No standalone, framework-agnostic layer treats context selection as a policy/optimization problem.

**Design philosophy**: Heuristics over magic. No hidden ML models. Transparent, configurable, inspectable scoring. Composable pipeline stages. Every item in the returned window carries its score and inclusion/exclusion reason.

**Performance target**: Sub-millisecond overhead for typical workloads (<500 items). Context management must never be the bottleneck.

**Integration model**: Standalone first. Smelt integration comes later at the call site, not in Cupel's library code. Cupel knows nothing about Assay or Smelt.

## Constraints

- **Tech stack**: C# / .NET 10 + Rust. Core library has zero external dependencies beyond BCL.
- **Performance**: Full pipeline < 1ms for < 500 items. No allocations on hot paths when tracing disabled.
- **API stability**: [JsonPropertyName] on all public types. ContextResult return type. Breaking changes only before v1.0 (now shipped).
- **Dependencies**: Core package must remain zero-dependency. Optional features via companion NuGet packages.
- **Distribution**: Public nuget.org + crates.io. Semantic versioning. Open source.

## Key Decisions

| Decision | Rationale | Outcome |
| --- | --- | --- |
| Content is non-nullable string on ContextItem | Simplifies API, maximizes debuggability for dry-run/trace. Memory is caller's problem at <500 items. | Good |
| Token counting is caller's responsibility | Keeps pipeline dependency-free. Works with any tokenizer (tiktoken, cl100k, Llama encoders) at zero coupling cost. | Good |
| CompositeScorer over scorer DAG | Nested composites achieve any DAG-like composition without cycle detection, topological sort, or parallel scheduling overhead. ~30 lines vs hundreds. | Good |
| Fixed pipeline over middleware | Call-next middleware silent-drop failure mode is worse than fixed pipeline's predictable behavior. Users substitute implementations, not reorder stages. | Good |
| Ordinal-only scoring invariant | Scorers that eliminate items bias the Slicer's input. KnapsackSlice is provably correct only on complete candidate sets. | Good |
| IPlacer interface (not hardcoded U-shape) | U-shaped attention curve is model-dependent and actively contested. Every other component is composable — Placer should be too. | Good |
| Both explicit policy and intent-based lookup | Explicit for orchestrator-level control (Smelt). Intent-based for quick integration and adoption. | Good |
| Separate DI package | Keeps core zero-dependency. Wollax.Cupel.Extensions.DependencyInjection for MS.Extensions.DI users. | Good |
| Public nuget.org from day 1 | Forces API discipline. Assay is already public. No proprietary logic to protect — value is in design quality. | Good |
| No IContextSink | Cupel selects; consumers convert. Output adapters are scope creep. | Good |
| ContextBudget as sealed class (not record) | Prevents with-expressions bypassing constructor validation | Good |
| Language-agnostic specification | Enables multi-language implementations with conformance guarantee | Good |

---
*Last updated: 2026-03-14 — v1.0 shipped, v1.1 active*
