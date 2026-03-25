# M008: Rust OpenTelemetry Bridge — Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library for coding agents. It selects and ranks context items (messages, documents, tool outputs) within a token budget using a fixed pipeline: Classify → Score → Deduplicate → Sort → Slice → Place.

## Why This Milestone

The .NET side already ships `Wollax.Cupel.Diagnostics.OpenTelemetry` — a companion NuGet that bridges `ITraceCollector` to `ActivitySource`, emitting structured traces at three verbosity tiers. Rust callers have no equivalent. M008 closes this parity gap with a `cupel-otel` companion crate using the same `cupel.*` attribute names, the same three verbosity tiers, and the same separation-of-concerns design (core stays zero-dep; OTel is an opt-in companion).

## User-Visible Outcome

### When this milestone is complete, a Rust caller can:

- Add `cupel-otel` as a dependency
- Construct a `CupelOtelTraceCollector::new(CupelVerbosity::StageAndExclusions)` and pass it to `pipeline.run_traced(items, &mut collector)`
- See `cupel.pipeline` / `cupel.stage.*` spans with correct attributes in any OTel-compatible backend (Jaeger, Honeycomb, console exporter)
- Use the same canonical source name `"cupel"` in `with_batch_exporter` or equivalent tracer config

### Entry point / environment

- Entry point: `crates/cupel-otel/` — new Rust crate
- Environment: library; callers integrate into their own Rust binaries/services
- Live dependencies involved: `opentelemetry` crate (0.x API); `opentelemetry-sdk` in dev-dependencies for testing

## Completion Class

- Contract complete means: `cargo test --all-targets` passes in both `crates/cupel` and `crates/cupel-otel`; `cargo clippy --all-targets -- -D warnings` clean; `cargo package` exits 0 for `cupel-otel`
- Integration complete means: in-memory SDK exporter captures real spans with correct attribute names, event payloads, span hierarchy, and verbosity-tier gating — proven by integration tests
- Operational complete means: none (library)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- `CupelOtelTraceCollector` implements `TraceCollector` and can be passed directly to `pipeline.run_traced()` — no wrapper needed
- In-memory exporter captures: root `cupel.pipeline` span with budget/verbosity/count attributes; 5 `cupel.stage.*` child spans with `item_count_in`/`item_count_out`; `cupel.exclusion` events on the correct stages at `StageAndExclusions`; `cupel.item.included` events on the place stage at `Full`
- Verbosity gating is correct: `StageOnly` emits no events; `StageAndExclusions` emits exclusion events only; `Full` emits both
- `cargo package --dry-run` exits 0; `cargo clippy --all-targets -- -D warnings` clean in both crates

## Risks and Unknowns

- **`TraceCollector` trait missing `on_pipeline_completed` hook** — The .NET bridge's entire design relies on `ITraceCollector.OnPipelineCompleted(report, budget, stageSnapshots)` to emit all spans at the end from structured data. The Rust `TraceCollector` trait has no equivalent hook. S01 must add this as a defaulted no-op method (`fn on_pipeline_completed`) to core `cupel` before `cupel-otel` can implement it. This is a core crate change. Adding a defaulted method is non-breaking for existing `TraceCollector` implementors (NullTraceCollector, DiagnosticTraceCollector). Core crate semver must remain 1.x — this is additive.
- **`opentelemetry` API churn** — The `opentelemetry` crate has had significant API changes across 0.x versions. The implementation must pin to a specific minor version and document it.
- **Rust span trait not dyn-compatible** — The OTel `Span` trait is not object-safe; spans cannot be stored as `Box<dyn Span>`. The `on_pipeline_completed` structured-handoff pattern (build spans from snapshot data at completion, not during stage execution) sidesteps this entirely — no live span storage needed.

## Existing Codebase / Prior Art

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — the canonical implementation to port; `OnPipelineCompleted` is the entry point; stage-to-exclusion mapping is in `GetExclusionsForStage`
- `crates/cupel/src/diagnostics/trace_collector.rs` — `TraceCollector` trait; `S01` must add `fn on_pipeline_completed` defaulted no-op here; `DiagnosticTraceCollector` must call it at the end of `into_report()`
- `crates/cupel/src/diagnostics/mod.rs` — `TraceEvent`, `PipelineStage`, `ExclusionReason`, `InclusionReason`, `SelectionReport`, `ContextBudget` — all types consumed by the OTel collector
- `crates/cupel-testing/` — companion crate pattern to follow for `cupel-otel` crate structure
- `spec/src/integrations/opentelemetry.md` — normative spec for attribute names, verbosity tiers, and hierarchy; Rust implementation must conform

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R058 — this milestone fully implements it (new requirement)

## Scope

### In Scope

- Additive `on_pipeline_completed` hook on `TraceCollector` trait in `crates/cupel` (defaulted no-op; non-breaking)
- `StageTraceSnapshot` struct in `crates/cupel` (stage name, item_count_in, item_count_out, duration, excluded items for that stage) — passed to `on_pipeline_completed`
- `crates/cupel-otel/` new crate: `CupelOtelTraceCollector`, `CupelVerbosity` enum, `add_cupel_instrumentation` helper
- Canonical OTel source name: `"cupel"` (Rust; not `"Wollax.Cupel"` — Rust uses the crate name convention)
- Three verbosity tiers: `StageOnly`, `StageAndExclusions`, `Full`
- All `cupel.*` attribute names matching the spec exactly
- `cargo package` exit 0 for `cupel-otel`

### Out of Scope / Non-Goals

- Publishing to crates.io (user will do this when ready)
- `tracing` crate integration (direct `opentelemetry` API only, per discussion)
- OTel for the core `cupel` pipeline itself (no opentelemetry dep in core, ever — R032)
- Attribute name stabilization (still pre-stable per D043)
- `cupel-testing` changes (unrelated)

## Technical Constraints

- `crates/cupel` must remain zero-dependency in production (no `opentelemetry` in `[dependencies]`, only in `[dev-dependencies]` of `cupel-otel`)
- `TraceCollector::on_pipeline_completed` must be a defaulted no-op so existing `NullTraceCollector` and `DiagnosticTraceCollector` implementations don't break
- `cupel-otel` depends on `cupel` via `{ version = "1.1", path = "../cupel" }` — same pattern as `cupel-testing`
- `opentelemetry` crate version must be pinned to avoid API churn; document the pinned version in the crate's README

## Integration Points

- `pipeline/mod.rs` → must call `collector.on_pipeline_completed(report, budget, stage_snapshots)` at the end of `run_traced`; this requires `StageTraceSnapshot` to be built from stage timing data already recorded during the run

## Open Questions

- **Source name: `"cupel"` or `"cupel-otel"`?** Current thinking: `"cupel"` — the canonical name should identify the library, not the bridge package. Callers use it in `with_batch_exporter` config. Matches the spirit of `"Wollax.Cupel"` in .NET. Decide in S01 when writing the spec addendum.
- **Stage duration: available from `TraceEvent.duration_ms`?** The existing `TraceEvent` struct has `duration_ms: f64`. `StageTraceSnapshot` can derive its duration from the matching `TraceEvent` — no new timing machinery needed. Confirm in S01.
