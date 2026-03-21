# Cupel

## What This Is

Cupel is a dual-language (.NET + Rust) context management library for coding agents. Given a set of context items (messages, documents, tool outputs, memory) and a token budget, it determines the optimal context window — maximizing information density while respecting LLM attention mechanics. It is part of the Wollax agentic development stack: **Assay** (spec-driven development) → **Smelt** (orchestration) → **Cupel** (context management).

## Core Value

Given candidates and a budget, return the optimal context selection with full explainability — every inclusion and exclusion has a traceable reason.

## Current State

**v1.1 shipped (2026-03-15):**
- .NET: 4,303 lines C#, 56 source files, 641 tests, 4 NuGet packages published to nuget.org
- Rust: 4,550 lines, 11 source files, 94 tests (28 conformance + 33 serde + 33 doctests), published to crates.io v1.1.0
- Language-agnostic specification with 28 conformance test vectors
- Dual-language CI/CD (path-filtered GitHub Actions), OIDC trusted publishing
- Optional serde feature with validation-on-deserialize for ContextBudget

**v1.2 in progress (M001/S01–S02 complete):**
- RAPI-01 through RAPI-05 done (non_exhaustive, derives, ContextKind factory methods, unreserved_capacity)
- Diagnostics spec chapter written (TraceCollector, Events, ExclusionReasons, SelectionReport)
- Conformance vector drift guard in CI; misleading vector comments fixed
- S01: All 8 diagnostic types implemented in Rust (`TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, etc.) with 5 conformance vectors
- S02: `TraceCollector` trait, `NullTraceCollector` ZST, `DiagnosticTraceCollector` with `into_report` — all re-exported from crate root; 12 behavioral contract tests pass, zero clippy/doc warnings

## Architecture / Key Patterns

- **Tech stack**: C# / .NET 10 + Rust (Edition 2024, MSRV 1.85)
- **Core crate**: `crates/cupel/src/` — `lib.rs`, `model/`, `pipeline/`, `scorer/`, `slicer/`, `placer/`, `error.rs`
- **Core .NET**: `src/Wollax.Cupel/` — 4 packages: Core, DI, Tiktoken, Json
- **Specification**: `spec/` — mdBook, language-agnostic, 28 required conformance vectors + optional diagnostics vectors
- **Fixed pipeline**: Classify → Score → Deduplicate → Slice → Place
- **Ordinal-only scoring invariant**: scorers rank but never eliminate — slicers see complete candidate sets
- **Zero external deps in core**: companion packages provide DI, tokenizer, JSON, serde
- **Per-invocation trace ownership**: TraceCollector is passed at call time, not stored on pipeline
- **CI**: `.github/workflows/ci-rust.yml` + `ci-dotnet.yml` with path filtering; `release-rust.yml` + `release-dotnet.yml`

## Capability Contract

See `.kata/REQUIREMENTS.md` for the explicit capability contract, requirement status, and coverage mapping.

## Milestone Sequence

- [ ] M001: v1.2 Rust Parity & Quality Hardening — Close diagnostics gap between Rust and .NET, harden API surface, batch quality issues; ship v1.2
