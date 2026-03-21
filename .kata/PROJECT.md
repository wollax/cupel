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

**v1.2 complete (M001 all 7 slices done, v1.2.0 tag pending manual publish):**
- RAPI-01 through RAPI-05 done (non_exhaustive, derives, ContextKind factory methods, unreserved_capacity)
- Diagnostics spec chapter written (TraceCollector, Events, ExclusionReasons, SelectionReport)
- Conformance vector drift guard in CI; misleading vector comments fixed
- S01: All 8 diagnostic types implemented in Rust with 5 conformance vectors
- S02: `TraceCollector` trait, `NullTraceCollector` ZST, `DiagnosticTraceCollector` — all re-exported; 12 behavioral contract tests pass
- S03: `Pipeline::run_traced()` and `Pipeline::dry_run()` implemented; all diagnostics conformance vectors pass
- S04: All diagnostic types serde-complete; internally-tagged wire format; R006 validated
- S05: `cargo clippy --all-targets` and `cargo-deny` unmaintained check in CI; R003 validated
- S06: 20 .NET triage items resolved — KnapsackSlice DP guard, epsilon fix, naming/error/enum hardening, interface contract docs, 6 new tests (net +5); 658 .NET tests pass; R004 validated
- S07: `CupelError::TableTooLarge` + KnapsackSlice 50M-cell guard; `Slicer::slice → Result`; CompositeScorer DFS + `as_any` removed; `UShapedPlacer` explicit left/right vecs; 15 new unit tests; release-rust.yml job-scoped permissions; R002 + R005 validated

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

- [x] M001: v1.2 Rust Parity & Quality Hardening — Close diagnostics gap between Rust and .NET, harden API surface, batch quality issues; ship v1.2 (all 7 slices complete; v1.2.0 tag pending manual publish)
- [ ] M002: v1.3 Design Sprint — Resolve deferred design problems (count-based quotas, Cupel.Testing vocabulary, metadata convention system, future features specs) and close spec quality debt; produce spec chapters and design decision records ready for v1.3 implementation (S01–S02 complete; S03–S06 pending)
