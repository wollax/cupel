# Requirements

This file is the explicit capability and coverage contract for the project.

## Active

### R001 — Rust diagnostics parity: TraceCollector + SelectionReport
- Class: core-capability
- Status: active
- Description: The Rust crate must expose a `TraceCollector` trait (with `NullTraceCollector` and `DiagnosticTraceCollector`), `TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, a `run_traced()` pipeline method, and a `DryRun` capability — matching the diagnostics spec chapter
- Why it matters: The .NET implementation has full explainability (SelectionReport, DryRun). Without Rust parity, the Rust crate is a second-class citizen that cannot serve agent orchestration use cases requiring "why was this item excluded?"
- Source: user
- Primary owning slice: M001/S03
- Supporting slices: M001/S01, M001/S02, M001/S04
- Validation: unmapped
- Notes: Spec chapter exists in `spec/src/diagnostics/`; conformance vectors in `spec/conformance/`; per-invocation ownership model (not stored on pipeline)

### R002 — KnapsackSlice DP table size guard (Rust + .NET)
- Class: quality-attribute
- Status: active
- Description: `KnapsackSlice` must validate that `capacity × items` does not exceed 50 million cells before allocating the DP table, returning an error (`CupelError::TableTooLarge`) if exceeded
- Why it matters: Without a guard, a caller can trivially cause an OOM crash with large token budgets and large item counts
- Source: user
- Primary owning slice: M001/S07
- Supporting slices: M001/S06
- Validation: unmapped
- Notes: Rust slice: `crates/cupel/src/slicer/knapsack.rs`; .NET slice: `src/Wollax.Cupel/Slicing/KnapsackSlice.cs`. Requires `CupelError::TableTooLarge` variant (Rust) — `#[non_exhaustive]` on CupelError already in place (RAPI-01).

### R003 — CI coverage: clippy --all-targets + cargo-deny unmaintained
- Class: quality-attribute
- Status: validated
- Description: Rust CI must run `cargo clippy --all-targets` to lint integration tests, examples, and benchmarks; `deny.toml` must flag unmaintained crates as warnings
- Why it matters: Current CI misses lint on test/example code; unmaintained crates are a supply-chain risk
- Source: user
- Primary owning slice: M001/S05
- Supporting slices: none
- Validation: validated — all four `cargo clippy` invocations in `ci-rust.yml` and `release-rust.yml` now include `--all-targets`; `deny.toml` has `unmaintained = "workspace"` under `[advisories]`; both local clippy checks (default + serde) and `cargo deny check` exit 0
- Notes: Issues: `2026-03-14-clippy-all-targets.md`, `2026-03-14-cargo-deny-unmaintained-warn.md`. Note: cargo-deny 0.19.0 uses scope values for `unmaintained` (not severity values) — `"workspace"` used instead of `"warn"` (see D030)

### R004 — .NET codebase quality hardening
- Class: quality-attribute
- Status: validated
- Description: Batch-resolve high-signal .NET issues: XML doc gaps, naming inconsistencies (OverflowStrategyValue → OverflowStrategy), defensive coding improvements, test coverage gaps (scorer test coverage, missing edge-case tests)
- Why it matters: 90 open issues; letting them accumulate makes each subsequent release harder
- Source: user
- Primary owning slice: M001/S06
- Supporting slices: none
- Validation: validated — all 20 triage items resolved in S06: OverflowStrategyValue → OverflowStrategy rename, QuotaBuilder epsilon fix, caller-facing error messages, enum integer anchors, ContextItem XML docs, interface contract docs (ITraceCollector constancy, ISlicer sort precondition, ContextResult.Report nullability, SelectionReport reference), 6 new tests (net +5 after duplicate removal); `dotnet build` 0 errors/warnings; 658 tests pass
- Notes: High-signal issues: `2026-03-14-overflow-strategy-value-naming.md`, `scorer-test-gaps.md`, `2026-03-13-phase4-pr-review-suggestions.md`, `007-contextitem-xml-docs.md`, `phase02-review-*.md`

### R005 — Rust codebase quality hardening
- Class: quality-attribute
- Status: active
- Description: Batch-resolve high-signal Rust issues: CompositeScorer cycle detection is misleading (document or remove), UShapedPlacer/QuotaSlice panic on invariant violation, test coverage gaps, doc comment improvements
- Why it matters: Existing panic paths are correctness concerns; misleading cycle detection creates false confidence
- Source: user
- Primary owning slice: M001/S07
- Supporting slices: none
- Validation: unmapped
- Notes: High-signal issues: `2026-03-14-composite-scorer-cycle-detection-ineffective.md`, `2026-03-14-u-shaped-placer-expect-on-option-vec.md`, `2026-03-14-quota-slice-expect-on-sub-budget.md`, `2026-03-14-unbounded-scaled-nesting-depth.md`

### R006 — Diagnostics serde coverage
- Class: quality-attribute
- Status: validated
- Description: All diagnostic types (`SelectionReport`, `TraceEvent`, `ExclusionReason`, `InclusionReason`) must support Serialize/Deserialize behind the `serde` feature flag
- Why it matters: Consistent with existing crate convention (ContextItem, ContextBudget have serde); callers need to persist or transmit diagnostic reports
- Source: user
- Primary owning slice: M001/S04
- Supporting slices: M001/S01
- Validation: validated — `cargo test --features serde` passes with wire-format assertions for all variants, round-trips for all 8 ExclusionReason and 3 InclusionReason variants, SelectionReport full round-trip, validation-rejection test, and graceful unknown-variant test; validation-on-deserialize pattern applied matching ContextBudget
- Notes: Must follow validation-on-deserialize pattern established for ContextBudget

## Validated

### R010 — ContextBudget.unreserved_capacity() helper (Rust + .NET)
- Class: core-capability
- Status: validated
- Description: `ContextBudget` exposes `unreserved_capacity()` = `MaxTokens - OutputReserve - sum(ReservedSlots)` in both .NET and Rust
- Why it matters: Callers frequently need to know how much budget is actually available for content
- Source: user
- Primary owning slice: M001 (phase 23, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phase 23-03; `unreserved_capacity()` in Rust crate, `UnreservedCapacity` property in .NET

### R011 — Rust API future-proofing (#[non_exhaustive], derives)
- Class: quality-attribute
- Status: validated
- Description: `CupelError` and `OverflowStrategy` have `#[non_exhaustive]`; concrete slicer/placer structs derive `Debug`, `Clone`, `Copy`
- Why it matters: Enables additive API evolution without breaking downstream code
- Source: user
- Primary owning slice: M001 (phase 23, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phase 23-01

### R012 — ContextKind factory methods and TryFrom<&str>
- Class: core-capability
- Status: validated
- Description: `ContextKind` provides factory methods for all known kinds and implements `TryFrom<&str>` for idiomatic error propagation
- Why it matters: Ergonomic construction; error-propagating parsing instead of panic
- Source: user
- Primary owning slice: M001 (phase 23, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phase 23-02

### R013 — Conformance drift guard + vector quality
- Class: quality-attribute
- Status: validated
- Description: CI drift guard diffs `spec/conformance/` against `crates/cupel/conformance/` and fails on divergence; misleading comments in conformance vectors fixed; diagnostics conformance vector schema documented
- Why it matters: Prevents spec/implementation divergence as spec evolves
- Source: user
- Primary owning slice: M001 (phase 25, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phases 25-01, 25-02, 25-03

### R014 — Diagnostics specification chapter
- Class: core-capability
- Status: validated
- Description: Language-agnostic diagnostics spec chapter exists covering TraceCollector contract, event types, exclusion/inclusion reasons, SelectionReport structure, and ownership model
- Why it matters: Spec-first guarantees cross-language conformance; Rust implementation can be built against verifiable contracts
- Source: user
- Primary owning slice: M001 (phase 24, completed)
- Supporting slices: none
- Validation: validated
- Notes: Completed in phases 24-01, 24-02; lives in `spec/src/diagnostics/`

## Deferred

### R020 — DecayScorer with TimeProvider injection
- Class: core-capability
- Status: deferred
- Description: Built-in time-decay scorer with injectable TimeProvider for testability
- Why it matters: Common use case; RecencyScorer only does ordinal ranking, not true decay curves
- Source: user
- Primary owning slice: none (v1.3)
- Supporting slices: none
- Validation: unmapped
- Notes: Deferred to v1.3 — not needed for v1.2 quality hardening focus

### R021 — Cupel.Testing package
- Class: quality-attribute
- Status: deferred
- Description: Fluent assertion chains over SelectionReport for test authoring
- Why it matters: Reduces boilerplate in caller tests
- Source: user
- Primary owning slice: none (v1.3)
- Supporting slices: none
- Validation: unmapped
- Notes: Deferred to v1.3

### R022 — OpenTelemetry bridge
- Class: operability
- Status: deferred
- Description: Bridge ITraceCollector/TraceCollector to ActivitySource for OTel integration
- Why it matters: Production observability without custom logging
- Source: user
- Primary owning slice: none (v1.3)
- Supporting slices: none
- Validation: unmapped
- Notes: Must be a companion package, not core — zero-dep constraint

## Out of Scope

### R030 — Storage / persistence
- Class: anti-feature
- Status: out-of-scope
- Description: Cupel does not manage conversation history, vector stores, or caches
- Why it matters: Prevents scope creep; storage is the caller's problem
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Explicitly excluded; caller passes pre-loaded items

### R031 — LLM API integration
- Class: anti-feature
- Status: out-of-scope
- Description: Cupel does not call models; it prepares context
- Why it matters: Keeps library framework-agnostic and zero-dependency
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Adapters owned by consumers (Smelt, user code)

### R032 — tracing crate integration in core
- Class: anti-feature
- Status: out-of-scope
- Description: The `tracing` crate is a companion concern, never core
- Why it matters: Zero-dep constraint; belongs in a companion crate
- Source: user
- Primary owning slice: none
- Supporting slices: none
- Validation: n/a
- Notes: Per .planning/REQUIREMENTS.md Out of Scope

## Traceability

| ID | Class | Status | Primary owner | Supporting | Proof |
|---|---|---|---|---|---|
| R001 | core-capability | active | M001/S03 | S01, S02, S04 | unmapped |
| R002 | quality-attribute | active | M001/S07 | S06 | unmapped |
| R003 | quality-attribute | validated | M001/S05 | none | validated |
| R004 | quality-attribute | validated | M001/S06 | none | validated |
| R005 | quality-attribute | active | M001/S07 | none | unmapped |
| R006 | quality-attribute | validated | M001/S04 | S01 | validated |
| R010 | core-capability | validated | M001 phase 23 | none | validated |
| R011 | quality-attribute | validated | M001 phase 23 | none | validated |
| R012 | core-capability | validated | M001 phase 23 | none | validated |
| R013 | quality-attribute | validated | M001 phase 25 | none | validated |
| R014 | core-capability | validated | M001 phase 24 | none | validated |
| R020 | core-capability | deferred | none | none | unmapped |
| R021 | quality-attribute | deferred | none | none | unmapped |
| R022 | operability | deferred | none | none | unmapped |
| R030 | anti-feature | out-of-scope | none | none | n/a |
| R031 | anti-feature | out-of-scope | none | none | n/a |
| R032 | anti-feature | out-of-scope | none | none | n/a |

## Coverage Summary

- Active requirements: 5
- Mapped to slices: 5
- Validated: 6
- Unmapped active requirements: 0
