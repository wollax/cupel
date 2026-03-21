# Requirements

This file is the explicit capability and coverage contract for the project.

## Active

### R040 — Count-based quota design resolution
- Class: differentiator
- Status: active
- Description: Resolve the 5 open design questions for count-based quotas in `QuotaSlice`: (1) algorithm integration with GreedySlice/KnapsackSlice, (2) tag non-exclusivity semantics for items with multiple tags, (3) pinned item interaction with minimum-count guarantees, (4) run-time vs build-time conflict detection rules, (5) KnapsackSlice compatibility path. Output: design decision record + spec-ready pseudocode. No implementation.
- Why it matters: Percentage-based quotas solve "at least 20% messages" but not "at least 3 tool results". Count-based quotas are required for agent memory scenarios where absolute minimum counts matter more than budget percentages. The design cannot be deferred further without blocking v1.3 implementation.
- Source: user
- Primary owning slice: M002/S03
- Supporting slices: M002/S01
- Validation: unmapped
- Notes: Explicitly deferred from M001 brainstorm (March 15). Tag non-exclusivity and knapsack path are the two hardest sub-problems.

### R041 — Spec quality debt closure
- Class: quality-attribute
- Status: validated
- Description: Close ~8-10 open spec editorial issues: event ordering within pipeline stages, item_count sentinel disambiguation, observer callback normative status, GreedySlice zero-token item ordering note, KnapsackSlice floor vs truncation-toward-zero note, UShapedPlacer pinned edge case table row, CompositeScorer pseudocode storage assignment, ScaledScorer nesting depth warning.
- Why it matters: The spec is publicly served as an mdBook. Ambiguous ordering guarantees block conformance test vector authoring; misleading algorithm descriptions mislead new language binding implementors.
- Source: user
- Primary owning slice: M002/S02
- Supporting slices: none
- Validation: validated — all 20 spec/phase24 issue files closed; 13 spec files updated with ordering rules, normative alignment, algorithm clarifications, and reserved variant examples; cargo test (35 passed) and dotnet test (583 passed) both green; TOML drift guard satisfied
- Notes: Actual issue count was 20 (not ~8-10); `spec-workflow-checksum-verification.md` intentionally deferred (CI security concern, out of S02 scope).

### R042 — Metadata convention system spec
- Class: differentiator
- Status: active
- Description: Define the `"cupel:<key>"` metadata namespace in the spec, establish first-class conventions (`cupel:trust` float64 [0,1] and `cupel:source-type` string enum), and write the `MetadataTrustScorer` spec chapter with conformance vector outlines. No implementation.
- Why it matters: Without a canonical namespace, every caller invents their own trust-key schema; a `MetadataTrustScorer` built on ad hoc keys is useless across projects. Reserving the namespace now enables the ecosystem to converge before anyone serializes production data with conflicting key names.
- Source: user (brainstorm March 15 — radical ideas, survived 2 rounds)
- Primary owning slice: M002/S04
- Supporting slices: none
- Validation: unmapped
- Notes: Trust is a scoring input, not a filter. No trust gates (silent exclusion) in this spec.

### R043 — Cupel.Testing vocabulary design
- Class: differentiator
- Status: active
- Description: Define 10-15 named assertion patterns over `SelectionReport` as a vocabulary spec section: what each assertion checks, tolerance/edge cases, error message format on failure. Output is a spec-ready vocabulary document — no implementation. This is the prerequisite for the Cupel.Testing NuGet package (R021).
- Why it matters: The testing vocabulary must be designed before implementation begins — shipping a testing package with ambiguous assertion semantics (e.g. "what is high-scoring?" in `PlaceHighScorersAtEdges`) creates an unstable API surface from day one.
- Source: user (brainstorm March 15 — high-value, design-phase requirement)
- Primary owning slice: M002/S05
- Supporting slices: none
- Validation: unmapped
- Notes: No FluentAssertions dependency; no snapshot testing (ordering stability not yet guaranteed). Vocabulary design output feeds R021 implementation.

### R044 — Future features spec chapters (DecayScorer, OTel, budget simulation)
- Class: quality-attribute
- Status: active
- Description: Produce spec chapters for three deferred features: (a) `DecayScorer` — algorithm, TimeProvider injection pattern, null-timestamp policy, three curve factory methods, conformance vector outlines; (b) OpenTelemetry verbosity levels — exact `cupel.*` attributes per verbosity tier, pre-stability disclaimer; (c) budget simulation API contracts — `GetMarginalItems` and `FindMinBudgetFor` with monotonicity precondition spec. No implementation.
- Why it matters: Each spec chapter is the prerequisite blocking implementation. Starting implementation without spec means the API surface gets driven by Rust/C# type system constraints rather than semantic clarity — a pattern explicitly rejected in the M001 brainstorm.
- Source: user (brainstorm March 15 — high-value features)
- Primary owning slice: M002/S06
- Supporting slices: none
- Validation: unmapped
- Notes: DecayScorer feeds R020; OTel feeds R022; budget simulation is a new requirement (no prior R-number). TimeProvider is mandatory (not optional) — no silent default to TimeProvider.System.

### R045 — Fresh brainstorm: post-v1.2 ideas
- Class: quality-attribute
- Status: active
- Description: Run a new explorer/challenger brainstorm session (following the established .planning/brainstorms/ format) against the current v1.2 codebase state. Surface new ideas not yet in the backlog, validate or retire existing deferred ideas in light of v1.2 completion, and produce a refined idea register.
- Why it matters: The last brainstorm (March 15) was conducted before diagnostics parity shipped. With SelectionReport and run_traced now live, the landscape has shifted — new ideas around analytics, testing, and ecosystem become more concrete.
- Source: user
- Primary owning slice: M002/S01
- Supporting slices: none
- Validation: unmapped
- Notes: Brainstorm output feeds count-quota design (S03) and future features spec (S06) by surfacing new angles on those problems.

## Validated (M001)

### R001 — Rust diagnostics parity: TraceCollector + SelectionReport
- Class: core-capability
- Status: validated
- Description: The Rust crate must expose a `TraceCollector` trait (with `NullTraceCollector` and `DiagnosticTraceCollector`), `TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, a `run_traced()` pipeline method, and a `DryRun` capability — matching the diagnostics spec chapter
- Why it matters: The .NET implementation has full explainability (SelectionReport, DryRun). Without Rust parity, the Rust crate is a second-class citizen that cannot serve agent orchestration use cases requiring "why was this item excluded?"
- Source: user
- Primary owning slice: M001/S03
- Supporting slices: M001/S01, M001/S02, M001/S04
- Validation: validated — TraceCollector trait + NullTraceCollector ZST + DiagnosticTraceCollector in crate root; Pipeline::run_traced() and Pipeline::dry_run() implemented; all 5 diagnostics conformance vectors pass; cargo test --features serde → 35 passed including SelectionReport round-trip; cargo doc --no-deps → 0 warnings
- Notes: Spec chapter exists in `spec/src/diagnostics/`; conformance vectors in `spec/conformance/`; per-invocation ownership model (not stored on pipeline)

### R002 — KnapsackSlice DP table size guard (Rust + .NET)
- Class: quality-attribute
- Status: validated
- Description: `KnapsackSlice` must validate that `capacity × items` does not exceed 50 million cells before allocating the DP table, returning an error (`CupelError::TableTooLarge`) if exceeded
- Why it matters: Without a guard, a caller can trivially cause an OOM crash with large token budgets and large item counts
- Source: user
- Primary owning slice: M001/S07
- Supporting slices: M001/S06
- Validation: validated — `CupelError::TableTooLarge { candidates, capacity, cells }` added; `KnapsackSlice::slice` returns `Err(TableTooLarge)` when `(capacity as u64) * (n as u64) > 50_000_000`; `knapsack_table_too_large` unit test passes; `Slicer::slice` returns `Result` throughout pipeline; .NET guard validated in S06
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
- Status: validated
- Description: Batch-resolve high-signal Rust issues: CompositeScorer cycle detection is misleading (document or remove), UShapedPlacer/QuotaSlice panic on invariant violation, test coverage gaps, doc comment improvements
- Why it matters: Existing panic paths are correctness concerns; misleading cycle detection creates false confidence
- Source: user
- Primary owning slice: M001/S07
- Supporting slices: none
- Validation: validated — CompositeScorer DFS cycle detection removed; `Scorer::as_any` eliminated from trait and all 8 impls; `UShapedPlacer::place` refactored to explicit left/right vecs with no Vec<Option> or .expect(); 15 new unit tests added (UShapedPlacer, TagScorer, PriorityScorer, ScaledScorer, ReflexiveScorer, Pipeline); `cargo clippy --all-targets -- -D warnings` clean
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

## Validated (prior milestones)

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
- Primary owning slice: none (v1.3 implementation; spec chapter in M002/S06)
- Supporting slices: M002/S06
- Validation: unmapped
- Notes: Spec chapter designed in M002/S06 (R044); implementation deferred to v1.3

### R021 — Cupel.Testing package
- Class: quality-attribute
- Status: deferred
- Description: Fluent assertion chains over SelectionReport for test authoring
- Why it matters: Reduces boilerplate in caller tests
- Source: user
- Primary owning slice: none (v1.3 implementation; vocabulary design in M002/S05)
- Supporting slices: M002/S05
- Validation: unmapped
- Notes: Vocabulary design phase in M002/S05 (R043); implementation deferred to v1.3

### R022 — OpenTelemetry bridge
- Class: operability
- Status: deferred
- Description: Bridge ITraceCollector/TraceCollector to ActivitySource for OTel integration
- Why it matters: Production observability without custom logging
- Source: user
- Primary owning slice: none (v1.3 implementation; verbosity spec in M002/S06)
- Supporting slices: M002/S06
- Validation: unmapped
- Notes: Must be a companion package, not core — zero-dep constraint; verbosity levels spec in M002/S06 (R044)

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
| R001 | core-capability | validated | M001/S03 | S01, S02, S04 | validated |
| R002 | quality-attribute | validated | M001/S07 | S06 | validated |
| R003 | quality-attribute | validated | M001/S05 | none | validated |
| R004 | quality-attribute | validated | M001/S06 | none | validated |
| R005 | quality-attribute | validated | M001/S07 | none | validated |
| R006 | quality-attribute | validated | M001/S04 | S01 | validated |
| R010 | core-capability | validated | M001 phase 23 | none | validated |
| R011 | quality-attribute | validated | M001 phase 23 | none | validated |
| R012 | core-capability | validated | M001 phase 23 | none | validated |
| R013 | quality-attribute | validated | M001 phase 25 | none | validated |
| R014 | core-capability | validated | M001 phase 24 | none | validated |
| R020 | core-capability | deferred | none (v1.3) | M002/S06 | unmapped |
| R021 | quality-attribute | deferred | none (v1.3) | M002/S05 | unmapped |
| R022 | operability | deferred | none (v1.3) | M002/S06 | unmapped |
| R030 | anti-feature | out-of-scope | none | none | n/a |
| R031 | anti-feature | out-of-scope | none | none | n/a |
| R032 | anti-feature | out-of-scope | none | none | n/a |
| R040 | differentiator | active | M002/S03 | M002/S01 | unmapped |
| R041 | quality-attribute | validated | M002/S02 | none | validated |
| R042 | differentiator | active | M002/S04 | none | unmapped |
| R043 | differentiator | active | M002/S05 | none | unmapped |
| R044 | quality-attribute | active | M002/S06 | none | unmapped |
| R045 | quality-attribute | active | M002/S01 | none | unmapped |

## Coverage Summary

- Active requirements: 6 (R040–R045, all M002)
- Mapped to slices: 6
- Validated: 11 (R001–R006, R010–R014)
- Unmapped active requirements: 0
- Unmapped active requirements: 0
