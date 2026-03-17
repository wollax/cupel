# M001: v1.2 Rust Parity & Quality Hardening

**Vision:** Close the diagnostics gap between the Rust and .NET Cupel implementations, harden the Rust API surface, and batch-address accumulated quality debt in both codebases — shipping a v1.2.0 release to crates.io and updated NuGet packages.

## Success Criteria

- `pipeline.run_traced(&mut collector)` returns a `SelectionReport` in Rust with per-item inclusion/exclusion reasons
- `pipeline.dry_run(items)` works in Rust, returning a SelectionReport without side effects
- All new diagnostic types serialize/deserialize correctly under the `serde` feature
- New diagnostics conformance vectors pass in CI
- `cargo clippy --all-targets -- -D warnings` passes with zero warnings
- `KnapsackSlice` returns an error (not OOM) when capacity × items > 50M cells in both .NET and Rust
- High-signal issues from the `.planning/issues/open/` backlog are resolved in batches

## Key Risks / Unknowns

- **run_traced API surface** — Must coexist with existing `run()` without breaking callers; per-invocation ownership model (TraceCollector passed at call time, not stored on pipeline)
- **Zero-cost NullTraceCollector** — Monomorphization must compile to no-ops; verify with a note or test that the disabled path allocates nothing
- **Diagnostics conformance vector authoring** — Vectors must be authored in `spec/conformance/` before or during S01; vendored via drift guard; minimum 5 vectors (4 exclusion reasons + 1 inclusion)
- **QH issue scope** — 90 open issues; S06/S07 triage determines the batch; expect ~15-20 .NET and ~10-15 Rust fixes

## Proof Strategy

- run_traced API surface → retire in S03 by implementing `run_traced()` and verifying it produces correct SelectionReport against conformance vectors
- Zero-cost NullTraceCollector → retire in S02 by implementing `NullTraceCollector` with `is_enabled: false` and documenting the invariant in code
- Diagnostics conformance vectors → retire in S04 by authoring vectors in `spec/` and verifying all pass in CI

## Verification Classes

- Contract verification: `cargo test`, `cargo clippy --all-targets`, conformance vector CI checks, `cargo deny check`, .NET test suite
- Integration verification: serde round-trip on SelectionReport; diagnostics conformance vectors run against Rust implementation
- Operational verification: none (library, not service)
- UAT / human verification: none — library with machine-verifiable contracts

## Milestone Definition of Done

This milestone is complete only when all are true:

- All 7 slices are marked complete with summaries
- `cargo test` passes with no failures (Rust)
- `cargo clippy --all-targets -- -D warnings` passes with zero new warnings
- `cargo deny check` passes
- Diagnostics conformance vectors present in `spec/conformance/` and `crates/cupel/conformance/`, all passing in CI
- `.NET` test suite (641+) passes with no regressions
- `KnapsackSlice` DP guard implemented and tested in both languages
- v1.2.0 tag ready (actual publish is a separate manual step)

## Requirement Coverage

- Covers: R001, R002, R003, R004, R005, R006
- Partially covers: none
- Leaves for later: R020, R021, R022 (deferred to v1.3)
- Orphan risks: none

## Slices

- [ ] **S01: Diagnostics Data Types** `risk:low` `depends:[]`
  > After this: `TraceEvent`, `ExclusionReason`, `InclusionReason`, and `SelectionReport` types exist in the Rust crate with full doc comments — `cargo test` and `cargo doc` pass; diagnostics conformance vectors authored in `spec/conformance/`.

- [ ] **S02: TraceCollector Trait & Implementations** `risk:medium` `depends:[S01]`
  > After this: `TraceCollector` trait, `NullTraceCollector` (zero-cost), and `DiagnosticTraceCollector` (buffered recording with `TraceDetailLevel`) are implemented, tested, and documented.

- [ ] **S03: Pipeline run_traced & DryRun** `risk:medium` `depends:[S01,S02]`
  > After this: `pipeline.run_traced(&mut collector)` and `pipeline.dry_run(items)` exist in Rust; all diagnostics conformance vectors pass in CI.

- [ ] **S04: Diagnostics Serde Integration** `risk:low` `depends:[S01,S02,S03]`
  > After this: all diagnostic types (`TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`) serialize/deserialize correctly under `--features serde`; serde round-trip tests pass.

- [ ] **S05: CI Quality Hardening** `risk:low` `depends:[]`
  > After this: `cargo clippy --all-targets -- -D warnings` and `cargo-deny` with unmaintained warning run in CI; no pre-existing warnings are introduced.

- [ ] **S06: .NET Quality Hardening** `risk:medium` `depends:[]`
  > After this: ~15-20 high-signal .NET issues resolved (naming, XML docs, test gaps, defensive coding); .NET test suite passes with no regressions; `KnapsackSlice` DP guard added to .NET.

- [ ] **S07: Rust Quality Hardening** `risk:medium` `depends:[S05]`
  > After this: ~10-15 high-signal Rust issues resolved (CompositeScorer cycle detection, UShapedPlacer/QuotaSlice panic paths, test gaps); `KnapsackSlice` DP guard added to Rust (`CupelError::TableTooLarge`); `cargo clippy --all-targets` clean.

## Boundary Map

### S01 → S02

Produces:
- `src/diagnostics/mod.rs` (or equivalent) — `TraceEvent` enum, `PipelineStage` enum, `OverflowEvent` struct
- `ExclusionReason` enum — all 8 variants (4 active + 4 reserved), data-carrying fields per spec
- `InclusionReason` enum — `Scored`, `Pinned`, `ZeroToken` variants
- `SelectionReport` struct — `included: Vec<IncludedItem>`, `excluded: Vec<ExcludedItem>`, stage-level timing
- `IncludedItem` struct — `item: ContextItem`, `score: f64`, `reason: InclusionReason`
- `ExcludedItem` struct — `item: ContextItem`, `score: Option<f64>`, `reason: ExclusionReason`
- `spec/conformance/diag-*.toml` — minimum 5 diagnostics conformance vectors

Consumes: nothing (builds on existing model types)

### S01 → S04

Produces:
- All diagnostic types above — S04 adds `#[derive(Serialize, Deserialize)]` behind `serde` feature

### S02 → S03

Produces:
- `TraceCollector` trait — `is_enabled() -> bool`, `record_stage_event(TraceEvent)`, `record_item_event(TraceEvent)`
- `NullTraceCollector` — ZST, `is_enabled` returns `false`, no-op record methods
- `DiagnosticTraceCollector` — `is_enabled` returns `true`, buffers events in insertion order, `detail_level: TraceDetailLevel`
- `TraceDetailLevel` enum — `Stage` (0), `Item` (1)
- `DiagnosticTraceCollector::into_report() -> SelectionReport` — extracts accumulated report

Consumes from S01:
- `TraceEvent`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `IncludedItem`, `ExcludedItem`

### S03 → S04

Produces:
- `Pipeline::run_traced<C: TraceCollector>(&self, items, collector: &mut C) -> Vec<ContextItem>` — pipeline stage instrumentation wired to collector
- `Pipeline::dry_run(&self, items) -> SelectionReport` — runs pipeline with DiagnosticTraceCollector, discards Vec output

Consumes from S01:
- All diagnostic types (TraceEvent, ExclusionReason, etc.)

Consumes from S02:
- `TraceCollector` trait, `DiagnosticTraceCollector`, `NullTraceCollector`

### S05 → S07

Produces:
- Updated `ci-rust.yml` — `cargo clippy --all-targets -- -D warnings`
- Updated `deny.toml` — `unmaintained = "warn"`
- Baseline: any pre-existing clippy warnings surfaced by `--all-targets` are known before S07 starts

Consumes: nothing

### S06 (standalone)

Produces:
- Resolved .NET issues: naming fixes, XML doc additions, test coverage additions, KnapsackSlice DP guard
- No new .NET public API (all fixes are additive or internal)

Consumes: nothing

### S07 (depends on S05 baseline)

Produces:
- Resolved Rust issues: CompositeScorer cycle detection fix/removal, UShapedPlacer invariant hardening, QuotaSlice expect removal, test additions
- `CupelError::TableTooLarge` variant + KnapsackSlice guard
- All pre-existing clippy --all-targets warnings fixed

Consumes from S05:
- `ci-rust.yml` with `--all-targets` (so S07 doesn't introduce new clippy violations)
