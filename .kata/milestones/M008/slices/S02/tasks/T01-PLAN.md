---
estimated_steps: 6
estimated_files: 4
---

# T01: Scaffold cupel-otel crate and write failing integration tests

**Slice:** S02 — Implement CupelOtelTraceCollector (all 3 verbosity tiers)
**Milestone:** M008

## Description

Creates the `crates/cupel-otel/` crate from scratch: `Cargo.toml` with all dependencies pinned, `src/lib.rs` with stub types that compile but do nothing, and `tests/integration.rs` with 5 integration tests that compile but fail at runtime (no spans captured). This task establishes the unambiguous pass/fail targets for T02 to make green.

The crate follows the `crates/cupel-testing/` template pattern: standalone (no workspace), edition = "2024", rust-version = "1.85", with minimal `include` list in `Cargo.toml`.

The integration tests use `opentelemetry_sdk::testing::trace::InMemorySpanExporterBuilder`, set a global tracer provider, run a real `cupel` pipeline with the stub `CupelOtelTraceCollector`, then assert on the exported spans. They compile but fail because the stub emits nothing. The `#[serial]` attribute serializes all tests to avoid global tracer provider interference.

## Steps

1. Create `crates/cupel-otel/src/` directory; write `crates/cupel-otel/Cargo.toml` with package metadata, `opentelemetry = "0.27"` + `cupel = { version = "1.1", path = "../cupel" }` in `[dependencies]`, and `opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` + `serial_test = "0.9"` in `[dev-dependencies]`.

2. Write `crates/cupel-otel/src/lib.rs` with:
   - `CupelVerbosity` enum with 3 variants: `StageOnly`, `StageAndExclusions`, `Full` (derive `Debug, Clone, Copy, PartialEq, Eq`)
   - `CupelOtelTraceCollector` struct with a `verbosity: CupelVerbosity` field and `pub const SOURCE_NAME: &'static str = "cupel"`
   - `impl CupelOtelTraceCollector { pub fn new(verbosity: CupelVerbosity) -> Self }` constructor
   - `impl TraceCollector for CupelOtelTraceCollector` with: `is_enabled()` returning `true`, `record_stage_event` as no-op, `record_item_event` as no-op, `on_pipeline_completed` as a no-op (empty body — does not emit any spans)

3. Write `crates/cupel-otel/tests/integration.rs` with a test helper `run_pipeline_and_capture(verbosity)` that: creates an `InMemorySpanExporterBuilder::new().build()` exporter, builds an `SdkTracerProvider` with `with_simple_exporter(exporter.clone())`, sets it as global via `global::set_tracer_provider(provider.clone())`, builds a minimal cupel pipeline (ReflexiveScorer + GreedySlice + ChronologicalPlacer, budget 1000 tokens), runs `pipeline.run_traced(items, &mut CupelOtelTraceCollector::new(verbosity))`, calls `provider.force_flush()`, then returns `exporter.get_finished_spans()`. Use a tight-budget pipeline for exclusion tests (budget 100 tokens, 3 items of 40 tokens each so 2 are excluded).

4. Write test `source_name_is_cupel`: calls `run_pipeline_and_capture(StageOnly)`, asserts at least 1 span was captured, asserts every span has `instrumentation_library.name == "cupel"` (or equivalent in opentelemetry_sdk 0.27 `SpanData`). This fails with 0 spans from the stub.

5. Write tests `hierarchy_root_and_five_stage_spans`, `stage_only_no_events`, `stage_and_exclusions_emits_exclusion_events`, `full_emits_included_item_events_on_place` — each annotated with `#[serial]`. All assert on span count or event presence; all fail (0 spans from stub).

6. Verify: `cd crates/cupel-otel && cargo build` exits 0 (no compile errors); `cargo test --all-targets 2>&1 | grep -E "FAILED|passed"` shows tests run and fail at the assertion level, not at compile time.

## Must-Haves

- [ ] `crates/cupel-otel/Cargo.toml` exists with `opentelemetry = "0.27"` in `[dependencies]` and `opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` in `[dev-dependencies]`
- [ ] `cupel = { version = "1.1", path = "../cupel" }` in `[dependencies]` (both version and path required for future `cargo package`)
- [ ] `crates/cupel-otel/src/lib.rs` compiles: `CupelVerbosity` enum + `CupelOtelTraceCollector` struct + `TraceCollector` impl with no-op bodies
- [ ] `crates/cupel-otel/tests/integration.rs` compiles: 5 tests with `#[serial]`, `InMemorySpanExporter` setup, real cupel pipeline
- [ ] `cargo build` in `crates/cupel-otel/` exits 0
- [ ] `cargo test --all-targets` in `crates/cupel-otel/` shows tests run and **fail** (not compile errors) — failure message comes from assertion, not from a linker or type error
- [ ] `cargo clippy --all-targets -- -D warnings` in `crates/cupel-otel/` exits 0 (stub is clean)

## Verification

- `cd crates/cupel-otel && cargo build` → exit 0
- `cd crates/cupel-otel && cargo test --all-targets 2>&1 | grep -c FAILED` → 5 (all integration tests fail)
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0
- `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty (core cupel unchanged)

## Observability Impact

- Signals added/changed: None at runtime — stub emits nothing; test harness captures 0 spans
- How a future agent inspects this: `cd crates/cupel-otel && cargo test -- --nocapture` shows assertion failure messages naming the expected span count vs actual 0
- Failure state exposed: If T02 incorrectly wires spans, the specific failing assertion identifies which tier or span property is wrong

## Inputs

- `crates/cupel-testing/Cargo.toml` — template for crate metadata shape (edition, rust-version, include list pattern)
- `crates/cupel/src/diagnostics/trace_collector.rs` — `TraceCollector` trait signature to implement (is_enabled, record_stage_event, record_item_event, on_pipeline_completed)
- S02-RESEARCH.md appendix — API cheatsheet for `InMemorySpanExporterBuilder` and `SdkTracerProvider::builder()` setup
- `crates/cupel/tests/on_pipeline_completed.rs` — shows how to build a minimal test pipeline (`Pipeline::builder().scorer(...).slicer(...).placer(...).build()`)

## Expected Output

- `crates/cupel-otel/Cargo.toml` — complete package manifest with pinned opentelemetry 0.27 deps
- `crates/cupel-otel/src/lib.rs` — `CupelVerbosity`, `CupelOtelTraceCollector`, no-op `TraceCollector` impl
- `crates/cupel-otel/tests/integration.rs` — 5 `#[serial]` integration tests in failing state
