# M008: Rust OpenTelemetry Bridge

**Vision:** Add a `cupel-otel` companion crate that bridges the Rust `TraceCollector` abstraction to the `opentelemetry` API — emitting `cupel.pipeline` / `cupel.stage.*` spans and events at configurable verbosity tiers, achieving parity with the existing `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet package. Core `cupel` crate remains zero-dependency; OTel is strictly opt-in.

## Success Criteria

- `CupelOtelTraceCollector::new(CupelVerbosity::StageOnly)` implements `TraceCollector` and can be passed directly to `pipeline.run_traced(items, &mut collector)` with no wrapper
- In-memory exporter captures a root `cupel.pipeline` span with `cupel.budget.max_tokens`, `cupel.verbosity`, `cupel.total_candidates`, `cupel.included_count`, `cupel.excluded_count` attributes
- Each of the 5 stage spans (`cupel.stage.classify`, etc.) carries `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out`
- `StageAndExclusions` tier emits `cupel.exclusion` events with `reason`, `item_kind`, `item_tokens` on the correct stage span
- `Full` tier additionally emits `cupel.item.included` events on the place stage span
- `StageOnly` emits no events (only spans and attributes)
- `cargo test --all-targets` passes in both `cupel` and `cupel-otel`; `cargo clippy --all-targets -- -D warnings` clean in both
- `cargo package --dry-run` exits 0 for `cupel-otel`
- Core `crates/cupel` crate has no `opentelemetry` dependency in `[dependencies]`

## Key Risks / Unknowns

- **`TraceCollector` trait missing `on_pipeline_completed` hook** — The .NET bridge's entire design depends on a structured end-of-run handoff that doesn't exist in the Rust trait yet. Must add it in S01 before the OTel crate can be built. Additive defaulted method; non-breaking for existing implementors.
- **`opentelemetry` crate API surface and version pinning** — The `opentelemetry` 0.x API has been volatile across minor versions. Need to identify the right version and the exact span-creation and attribute APIs to use before implementation.

## Proof Strategy

- **Missing `on_pipeline_completed` hook** → retire in S01 by adding the defaulted method to `TraceCollector`, building `StageTraceSnapshot`, and having `DiagnosticTraceCollector::into_report` call it — proven by the existing 167 `cargo test --all-targets` tests all still passing after the core change
- **`opentelemetry` API version** → retire in S02 by implementing the full `CupelOtelTraceCollector` against a pinned version and proving it works with the in-memory SDK exporter in integration tests

## Verification Classes

- Contract verification: unit tests (verbosity gating, stage-to-exclusion mapping, attribute presence) + integration tests using `opentelemetry-sdk` in-memory exporter; `cargo package --dry-run`; `cargo clippy --all-targets -- -D warnings`
- Integration verification: real span hierarchy captured by in-memory exporter; attribute values match spec exactly; `CupelOtelTraceCollector` usable as a drop-in `TraceCollector` with no adapter
- Operational verification: none (library)
- UAT / human verification: none

## Milestone Definition of Done

This milestone is complete only when all are true:

- `TraceCollector::on_pipeline_completed` exists as a defaulted no-op in `crates/cupel`; `pipeline.run_traced` calls it at completion; `StageTraceSnapshot` struct is defined and populated
- `crates/cupel-otel/` exists with `CupelOtelTraceCollector`, `CupelVerbosity`, and (optional) `add_cupel_instrumentation` tracer builder helper
- All three verbosity tiers implemented and tested with in-memory exporter
- All `cupel.*` attribute names match `spec/src/integrations/opentelemetry.md` exactly
- `cargo test --all-targets` passes in both crates; `cargo clippy --all-targets -- -D warnings` clean in both
- `cargo package --dry-run` exits 0 for `cupel-otel`
- Core `crates/cupel` has zero `opentelemetry` production dependency
- R058 marked validated in `.kata/REQUIREMENTS.md`

## Requirement Coverage

- Covers: R058
- Partially covers: none
- Leaves for later: R055 (ProfiledPlacer — externally blocked), R057 (TimestampCoverage split — low demand)
- Orphan risks: none

## Slices

- [x] **S01: Add on_pipeline_completed hook to core cupel TraceCollector** `risk:medium` `depends:[]`
  > After this: `TraceCollector` trait has a defaulted `on_pipeline_completed(&self, report: &SelectionReport, budget: &ContextBudget, stage_snapshots: &[StageTraceSnapshot])` method; `pipeline.run_traced` calls it; all 167 existing tests pass — proven by `cargo test --all-targets`.

- [ ] **S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers)** `risk:medium` `depends:[S01]`
  > After this: `cupel-otel` crate compiles, `CupelOtelTraceCollector` implements `TraceCollector`, and integration tests using the `opentelemetry-sdk` in-memory exporter prove all three verbosity tiers emit the correct spans, attributes, and events.

- [ ] **S03: Crate packaging, spec addendum, and R058 validation** `risk:low` `depends:[S02]`
  > After this: `cargo package --dry-run` exits 0; `spec/src/integrations/opentelemetry.md` has a Rust-specific section; CHANGELOG.md updated; R058 validated — milestone complete.

## Boundary Map

### S01 → S02

Produces:
- `TraceCollector::on_pipeline_completed(&mut self, report: &SelectionReport, budget: &ContextBudget, stage_snapshots: &[StageTraceSnapshot])` — defaulted no-op on trait
- `StageTraceSnapshot` struct: `stage: PipelineStage`, `item_count_in: usize`, `item_count_out: usize`, `duration_ms: f64`, `excluded: Vec<ExcludedItem>`
- `Pipeline::run_traced` calls `collector.on_pipeline_completed(...)` at end, after `collector.into_report()` — or before, passing `&report` and snapshots built from `TraceEvent` data
- All existing tests continue to pass (additive, non-breaking)

Consumes:
- nothing (first slice; builds on existing `TraceCollector` trait and `run_traced` machinery)

### S02 → S03

Produces:
- `crates/cupel-otel/` crate with `CupelOtelTraceCollector : TraceCollector`, `CupelVerbosity` enum (StageOnly, StageAndExclusions, Full)
- Canonical OTel source name `"cupel"` in `CupelOtelTraceCollector::SOURCE_NAME` const
- Integration tests in `crates/cupel-otel/tests/` using `opentelemetry-sdk` in-memory exporter
- Pinned `opentelemetry` version documented in Cargo.toml and README

Consumes from S01:
- `TraceCollector::on_pipeline_completed` — the implementation hook
- `StageTraceSnapshot` — the structured data the OTel collector builds spans from

### S01 + S02 → S03

Produces:
- `cargo package --dry-run` exit 0 for `cupel-otel`
- Rust-specific section in `spec/src/integrations/opentelemetry.md` (source name, Cargo.toml snippet, usage example)
- `CHANGELOG.md` entry under Unreleased
- R058 marked validated in `.kata/REQUIREMENTS.md`

Consumes:
- All S02 outputs stable and tested
