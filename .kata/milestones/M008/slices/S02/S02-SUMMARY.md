---
id: S02
parent: M008
milestone: M008
provides:
  - crates/cupel-otel/ crate with CupelVerbosity enum (3 variants) and CupelOtelTraceCollector implementing TraceCollector
  - CupelOtelTraceCollector::SOURCE_NAME const = "cupel"
  - Root cupel.pipeline span with cupel.budget.max_tokens (i64) and cupel.verbosity (string) attributes
  - Five cupel.stage.* child spans (classify, score, deduplicate, slice, place) parented to root via Context::current_with_span
  - StageAndExclusions tier: cupel.exclusion events per excluded item with reason/item_kind/item_tokens; cupel.exclusion.count attribute on stage spans with exclusions
  - Full tier: cupel.item.included events on the Place stage span with kind/tokens/score attributes
  - PartialOrd/Ord on CupelVerbosity enabling >= tier-gating comparisons
  - 5 serial integration tests using opentelemetry-sdk InMemorySpanExporter — all 3 verbosity tiers verified
  - Pinned opentelemetry 0.27 in cupel-otel Cargo.toml; core cupel crate has zero opentelemetry dependency
requires:
  - slice: S01
    provides: TraceCollector::on_pipeline_completed hook + StageTraceSnapshot struct — the structured end-of-run data the OTel collector builds spans from
affects:
  - S03
key_files:
  - crates/cupel-otel/Cargo.toml
  - crates/cupel-otel/src/lib.rs
  - crates/cupel-otel/tests/integration.rs
key_decisions:
  - D169 — Root span requires explicit .end() via root_cx.span().end() — opentelemetry 0.27 never auto-ends on drop
  - D170 — Root span carries only cupel.budget.max_tokens and cupel.verbosity (spec-only); does NOT add cupel.total_candidates etc.
  - D171 — ExclusionReason name extracted via explicit match returning &'static str (not Debug formatting which includes struct fields)
  - CupelVerbosity derives PartialOrd/Ord (discriminant order StageOnly < StageAndExclusions < Full) for >= tier comparisons
  - Stage child spans created via tracer.start_with_context(name, &root_cx) — parent wired via context, not explicit parent ID
patterns_established:
  - OTel span hierarchy via Context::current_with_span — root context established once, all stage spans use start_with_context with root_cx
  - Explicit .end() on every span before drop — never rely on Drop for span finalization in opentelemetry 0.27
  - Integration test setup: create InMemorySpanExporter, build SdkTracerProvider with SimpleSpanProcessor, set global, run pipeline, force_flush, get_finished_spans
  - Two test helper fns: run_pipeline_and_capture (all items fit) and run_pipeline_with_exclusions_and_capture (tight budget forces exclusions)
  - #[serial] on all integration tests prevents global tracer provider interference between tests
observability_surfaces:
  - cd crates/cupel-otel && cargo test -- --nocapture shows test names and assertion failures
  - exporter.get_finished_spans() returns Vec<SpanData>; filter by span.name for specific spans; span.attributes for attribute assertions; span.events for event assertions
  - Test assertion failures name the expected key and actual span.attributes for fast diagnosis
drill_down_paths:
  - .kata/milestones/M008/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M008/slices/S02/tasks/T02-SUMMARY.md
duration: 50m
verification_result: passed
completed_at: 2026-03-24
---

# S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers)

**`crates/cupel-otel/` crate: `CupelOtelTraceCollector` implementing `TraceCollector` with 3 verbosity tiers, all 5 integration tests pass against `opentelemetry-sdk` in-memory exporter.**

## What Happened

**T01** created the `crates/cupel-otel/` crate from scratch following the `crates/cupel-testing/` template pattern. `Cargo.toml` pins `opentelemetry = "0.27"` in `[dependencies]`, `opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` + `serial_test = "0.9"` in `[dev-dependencies]`, with `cupel = { version = "1.1", path = "../cupel" }` for future `cargo package` compatibility. `src/lib.rs` defined `CupelVerbosity`, `CupelOtelTraceCollector` with `SOURCE_NAME = "cupel"`, and `exclusion_reason_name()` helper. Five `#[serial]` integration tests were written using `InMemorySpanExporter` — all failing at assertion level (0 spans from stub), giving T02 unambiguous pass/fail targets. Two API deviations from research docs were caught during compilation: `SpanData` lives at `opentelemetry_sdk::export::trace::SpanData` (not `opentelemetry_sdk::trace::SpanData`), and `span.instrumentation_scope.name()` is a method call (not field access).

**T02** replaced the no-op `on_pipeline_completed` with the full implementation. The flow: (1) get `BoxedTracer` via `global::tracer(SOURCE_NAME)`; (2) create root `cupel.pipeline` span with `cupel.budget.max_tokens` and `cupel.verbosity` attributes; (3) wrap root in `Context::current_with_span(root)`; (4) iterate `stage_snapshots`, creating `cupel.stage.{name}` child spans via `start_with_context` with root context; (5) set 3 stage attributes per span; (6) emit `cupel.exclusion` events at `StageAndExclusions`+ tier; (7) emit `cupel.item.included` events at `Full` tier on the Place stage; (8) call `.end()` on each stage span; (9) call `root_cx.span().end()` after the stage loop. The critical discovery: opentelemetry 0.27 never auto-ends spans on drop — explicit `.end()` is mandatory or spans never appear in the exporter (D169). All 5 integration tests passed after implementation.

## Verification

- `cd crates/cupel-otel && cargo test --all-targets` → 5/5 integration tests pass, 0 failures
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0, 0 warnings
- `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty (core has no OTel dep)
- `cd crates/cupel && cargo test --all-targets` → 170 tests pass, 0 failures, 0 regressions

Tests verified:
- `source_name_is_cupel` — all spans have `instrumentation_scope.name() == "cupel"`
- `hierarchy_root_and_five_stage_spans` — 6 spans total, root `cupel.pipeline` + 5 `cupel.stage.*` children with correct parent_span_id
- `stage_only_no_events` — 6 spans, 0 events on any span
- `stage_and_exclusions_emits_exclusion_events` — `cupel.exclusion` events with reason/item_kind/item_tokens on tight-budget run
- `full_emits_included_item_events_on_place` — `cupel.item.included` events with kind/tokens/score on Place stage

## Requirements Advanced

- R058 — `cupel-otel` crate now exists with `CupelOtelTraceCollector` implementing `TraceCollector`; all three verbosity tiers implemented and verified with in-memory exporter; `opentelemetry` dependency strictly in `cupel-otel`, not core `cupel`

## Requirements Validated

- None — R058 is not yet fully validated; S03 (cargo package --dry-run, spec Rust section, CHANGELOG, R058 validation) is still required

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- `SpanData` import path is `opentelemetry_sdk::export::trace::SpanData` in 0.27.1, not `opentelemetry_sdk::trace::SpanData` as documented in research — caught via compiler error in T01 and corrected
- `span.instrumentation_scope.name()` is a method call in 0.27.1, not field access — caught via compiler error in T01 and corrected
- `exclusion_reason_name` was planned as a public function but was moved to private helper — no semantic impact; the function is internal infrastructure

## Known Limitations

- Root span does not carry `cupel.total_candidates`, `cupel.included_count`, `cupel.excluded_count` — spec lists only `cupel.budget.max_tokens` and `cupel.verbosity` at the root level; the .NET implementation adds extra attributes beyond the spec (D170)
- `cargo package --dry-run` not yet run — blocked on S03 which handles packaging, spec addendum, and CHANGELOG

## Follow-ups

- S03: run `cargo package --dry-run` for `cupel-otel`; add Rust-specific section to `spec/src/integrations/opentelemetry.md`; update `CHANGELOG.md`; validate R058 in `REQUIREMENTS.md`

## Files Created/Modified

- `crates/cupel-otel/Cargo.toml` — package manifest with opentelemetry 0.27 deps pinned
- `crates/cupel-otel/src/lib.rs` — full `CupelOtelTraceCollector` implementation with all 3 verbosity tiers
- `crates/cupel-otel/tests/integration.rs` — 5 serial integration tests using `InMemorySpanExporter`, all passing

## Forward Intelligence

### What the next slice should know
- The `cupel-otel` crate is a valid Rust crate but is not registered in the workspace yet (the repo uses standalone crates, not a workspace). S03's `cargo package --dry-run` should work from `crates/cupel-otel/` directly.
- `CupelOtelTraceCollector` is a zero-state collector — it stores only `CupelVerbosity` and does all work in `on_pipeline_completed`. No fields accumulate data between `record_stage_event` / `record_item_event` calls.
- The `exclusion_reason_name` helper uses a `_ => "Unknown"` arm to handle future `ExclusionReason` variants safely; S03 should ensure the spec Rust section documents this behavior.
- `is_enabled()` unconditionally returns `true` — the global OTel tracer handles the no-op path when no provider is configured.

### What's fragile
- Global tracer provider state is process-global — the `#[serial]` attribute on all integration tests is load-bearing; if a new test is added without `#[serial]`, it may observe spans from a sibling test's provider configuration

### Authoritative diagnostics
- `exporter.get_finished_spans()` is the ground truth; filter `span.name` to find specific spans; `span.attributes` is `Vec<KeyValue>` — iterate to find by key name

### What assumptions changed
- Assumed `SpanData` would be at `opentelemetry_sdk::trace::SpanData` — it is actually at `opentelemetry_sdk::export::trace::SpanData`; any S03 documentation examples should use the correct path
