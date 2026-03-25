---
id: T01
parent: S02
milestone: M008
provides:
  - crates/cupel-otel/ crate scaffold (Cargo.toml + src/lib.rs + tests/integration.rs)
  - CupelVerbosity enum with 3 variants (StageOnly, StageAndExclusions, Full)
  - CupelOtelTraceCollector struct with SOURCE_NAME const and no-op TraceCollector impl
  - exclusion_reason_name() helper function (non_exhaustive-safe variant name extraction)
  - 5 failing integration tests using InMemorySpanExporter + #[serial] — unambiguous T02 targets
key_files:
  - crates/cupel-otel/Cargo.toml
  - crates/cupel-otel/src/lib.rs
  - crates/cupel-otel/tests/integration.rs
key_decisions:
  - opentelemetry_sdk::export::trace::SpanData is the correct import path (not opentelemetry_sdk::trace::SpanData) in 0.27.1
  - span.instrumentation_scope.name() is a method call (not field access) in 0.27.1
  - serial_test 0.9 #[serial] attribute chosen for global tracer provider isolation
patterns_established:
  - Integration test helper pattern: create InMemorySpanExporter, build SdkTracerProvider with simple exporter, set global, run pipeline with collector, force_flush, get_finished_spans
  - Two helper fns: run_pipeline_and_capture(verbosity) for no-exclusion tests, run_pipeline_with_exclusions_and_capture(verbosity) for tight-budget tests
observability_surfaces:
  - cargo test -- --nocapture in crates/cupel-otel shows assertion failure messages naming expected vs actual span count
duration: 35m
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T01: Scaffold cupel-otel crate and write failing integration tests

**New `crates/cupel-otel/` crate: `CupelVerbosity`, stub `CupelOtelTraceCollector`, and 5 serial integration tests failing at assertion level with 0 spans from stub.**

## What Happened

Created `crates/cupel-otel/` from scratch following the `crates/cupel-testing/` template pattern. The `Cargo.toml` pins `opentelemetry = "0.27"` in `[dependencies]` and `opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` + `serial_test = "0.9"` in `[dev-dependencies]`, with `cupel = { version = "1.1", path = "../cupel" }` for future `cargo package` compatibility.

`src/lib.rs` defines `CupelVerbosity` (3 variants, derives `Debug/Clone/Copy/PartialEq/Eq`) and `CupelOtelTraceCollector` with `SOURCE_NAME = "cupel"`. The `TraceCollector` impl has no-op bodies for `record_stage_event`, `record_item_event`, and `on_pipeline_completed`. Also provides `exclusion_reason_name()` helper with exhaustive `match` + `_ => "Unknown"` for the `#[non_exhaustive]` `ExclusionReason`.

`tests/integration.rs` sets up two test helper fns: `run_pipeline_and_capture` (1000-token budget, all items fit) and `run_pipeline_with_exclusions_and_capture` (60-token budget, forces 2 of 3 items to be excluded). Five `#[serial]` tests: `source_name_is_cupel`, `hierarchy_root_and_five_stage_spans`, `stage_only_no_events`, `stage_and_exclusions_emits_exclusion_events`, `full_emits_included_item_events_on_place`.

One small deviation from the research docs: `SpanData` lives at `opentelemetry_sdk::export::trace::SpanData` in 0.27.1, not `opentelemetry_sdk::trace::SpanData`. Also `span.instrumentation_scope.name()` is a method (not field). Both were caught via compiler errors and fixed before the final compile.

## Verification

- `cd crates/cupel-otel && cargo build` → exit 0
- `cd crates/cupel-otel && cargo test --all-targets 2>&1 | grep FAILED` → 5 failures (`full_emits_included_item_events_on_place`, `hierarchy_root_and_five_stage_spans`, `source_name_is_cupel`, `stage_and_exclusions_emits_exclusion_events`, `stage_only_no_events`) — all assertion-level failures, none are compile/link errors
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0, no warnings
- `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty (core unchanged)

## Diagnostics

Run `cd crates/cupel-otel && cargo test -- --nocapture` to see assertion failure messages that name expected vs actual span count. Each test names the expected behavior so T02 knows exactly what it must produce.

## Deviations

- `SpanData` import path corrected to `opentelemetry_sdk::export::trace::SpanData` (research cheatsheet listed wrong path)
- `span.instrumentation_scope.name()` used as method call (research cheatsheet listed as field access)

## Known Issues

None — all 5 tests fail at the correct level for the stub state.

## Files Created/Modified

- `crates/cupel-otel/Cargo.toml` — package manifest with opentelemetry 0.27 deps pinned
- `crates/cupel-otel/src/lib.rs` — CupelVerbosity enum, CupelOtelTraceCollector stub, exclusion_reason_name helper
- `crates/cupel-otel/tests/integration.rs` — 5 serial integration tests in failing state
