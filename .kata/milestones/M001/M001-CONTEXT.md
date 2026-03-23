# M001: v1.2 Rust Parity & Quality Hardening — Context

**Gathered:** 2026-03-17
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library. v1.2 closes the feature gap between the Rust and .NET implementations (diagnostics parity) and addresses accumulated quality debt across both codebases.

## Why This Milestone

The Rust crate (published to crates.io as v1.1.0) is missing the core explainability feature that the .NET library has had since v1.0: `SelectionReport` and `DryRun`. Agent orchestration use cases need "why was this item excluded?" in Rust. At the same time, 90+ issues have accumulated from prior PR reviews. The combination of diagnostics parity + quality batch is the right unit of work for v1.2.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Call `pipeline.run_traced(&mut collector)` on the Rust crate and receive a `SelectionReport` with per-item inclusion/exclusion reasons
- Call `pipeline.dry_run(items)` on the Rust crate to inspect selection decisions without committing to output
- Serialize a `SelectionReport` to JSON using the `serde` feature (matching the .NET JSON output shape)
- Build their project with `cargo clippy --all-targets` and see zero new warnings from the crate's own code
- Trust that `KnapsackSlice` on both .NET and Rust will return an error rather than OOM on very large inputs

### Entry point / environment

- Entry point: `cargo add cupel` / `dotnet add package Wollax.Cupel`
- Environment: library crate, no server; verified via `cargo test` + doctests + conformance vectors
- Live dependencies involved: none (zero-dep core)

## Completion Class

- Contract complete means: all conformance vectors pass (including new diagnostics vectors); doctests pass; `cargo clippy --all-targets` clean; `cargo deny check` clean
- Integration complete means: serde round-trip on SelectionReport passes; diagnostics conformance vectors verified against Rust implementation
- Operational complete means: n/a (library, not service)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- Rust: `pipeline.run_traced()` produces a `SelectionReport` whose inclusion/exclusion reasons match the diagnostics conformance vectors
- Rust: `pipeline.dry_run()` returns a `SelectionReport` without modifying pipeline state
- Rust + .NET: `KnapsackSlice` with capacity × items > 50M returns `Err(CupelError::TableTooLarge)` / equivalent .NET error
- CI: `cargo clippy --all-targets -- -D warnings` passes with zero warnings

## Risks and Unknowns

- **TraceCollector zero-cost abstraction** — Rust monomorphization should give zero overhead for `NullTraceCollector`, but LLVM inlining behavior should be verified with a test or inline comment. Mitigation: document the invariant; add a compile-time check if feasible.
- **Pipeline API surface for `run_traced`** — The current `Pipeline::run()` returns `Vec<ContextItem>`. Adding `run_traced()` requires a decision on whether it's a new method or a unified method. Per-spec: per-invocation ownership. Decision: new method.
- **Diagnostics conformance vectors** — A single example vector exists (from phase 25-03). New vectors for each exclusion reason need to be written before implementation and used as the verification target. This creates a slight chicken-and-egg: write vectors first, implement against them.
- **QH issue scope uncertainty** — 90 open issues; not all are worth fixing. The triage pass in S06/S07 will scope the batch. Expect 10-20 substantive fixes per quality slice.

## Existing Codebase / Prior Art

- `crates/cupel/src/pipeline/` — Rust pipeline implementation; `run()` method is the integration point for `run_traced()`
- `crates/cupel/src/error.rs` — `CupelError` enum; needs `TableTooLarge` variant in S07
- `crates/cupel/src/slicer/knapsack.rs` — DP table allocation target for R002
- `src/Wollax.Cupel/Diagnostics/` — .NET reference implementation for SelectionReport, TraceCollector, etc.
- `src/Wollax.Cupel/Slicing/KnapsackSlice.cs` — .NET knapsack target for R002
- `spec/src/diagnostics/` — Authoritative spec for all diagnostic types (TraceCollector, Events, ExclusionReasons, SelectionReport)
- `spec/conformance/` — Conformance vectors; drift-guarded against `crates/cupel/conformance/`
- `.github/workflows/ci-rust.yml` — CI target for S05 (clippy --all-targets, deny.toml)
- `.planning/issues/open/` — 90 open issues for S06/S07 triage

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R001 — Rust diagnostics parity (S01-S04)
- R002 — KnapsackSlice DP guard (S06, S07)
- R003 — CI quality hardening (S05)
- R004 — .NET quality hardening (S06)
- R005 — Rust quality hardening (S07)
- R006 — Diagnostics serde (S04)

## Scope

### In Scope

- Full Rust diagnostics implementation: TraceEvent, ExclusionReason, InclusionReason, SelectionReport, TraceCollector trait, NullTraceCollector, DiagnosticTraceCollector, run_traced, dry_run
- Serde coverage for all new diagnostic types
- Diagnostics conformance vectors (write and verify)
- KnapsackSlice DP guard (Rust + .NET)
- CI: clippy --all-targets, cargo-deny unmaintained
- .NET quality batch: ~15-20 high-signal issues from backlog (naming, docs, test gaps)
- Rust quality batch: ~10-15 high-signal issues from backlog (panic paths, cycle detection, test gaps)

### Out of Scope / Non-Goals

- `tracing` crate integration (companion crate only, never core)
- `Arc<dyn TraceCollector>` ownership model
- Serializable pipeline config
- DecayScorer, Cupel.Testing package, OpenTelemetry bridge (deferred to v1.3)
- Count-based quotas, metadata convention system, ProfiledPlacer

## Technical Constraints

- Rust core must remain zero external dependencies beyond std; serde stays behind feature flag
- MSRV 1.85.0 (pinned in `rust-toolchain.toml`); Edition 2024
- All new public Rust types must be `#[non_exhaustive]` where forward-compat matters
- .NET: .NET 10, zero external dependencies in Wollax.Cupel core
- Conformance vectors must be authored in `spec/conformance/` first; vendored copies in `crates/cupel/conformance/` via drift guard

## Integration Points

- `spec/conformance/` → `crates/cupel/conformance/` — drift guard ensures vendored vectors stay in sync
- `crates.io` — v1.2.0 publish after milestone complete
- `nuget.org` — new .NET packages if API surface changes (unlikely for QH work)

## Open Questions

- Should `run_traced` replace `run` or coexist? → Coexist: `run` stays unchanged; `run_traced` is additive. Avoids breaking changes.
- How many diagnostics conformance vectors to write? → One per exclusion reason variant (4 active: BudgetExceeded, Deduplicated, NegativeTokens, PinnedOverride) + one happy-path inclusion vector = minimum 5 new vectors.
