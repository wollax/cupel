# S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers)

**Goal:** Create the `cupel-otel` crate with `CupelOtelTraceCollector` (implementing `TraceCollector`) and `CupelVerbosity` enum, with all three verbosity tiers (StageOnly, StageAndExclusions, Full) emitting the correct spans, attributes, and events as specified in `spec/src/integrations/opentelemetry.md`.
**Demo:** Integration tests using the `opentelemetry-sdk` 0.27 in-memory exporter pass — root `cupel.pipeline` span + 5 `cupel.stage.*` child spans captured with correct attributes; exclusion events on the right stages at `StageAndExclusions`; included-item events on the Place stage at `Full`; `StageOnly` emits no events. `cargo test --all-targets` passes in `cupel-otel`; `cargo clippy --all-targets -- -D warnings` clean.

## Must-Haves

- `crates/cupel-otel/` crate compiles as a standalone Rust crate
- `CupelOtelTraceCollector` implements `TraceCollector`; `CupelVerbosity` enum has 3 variants (`StageOnly`, `StageAndExclusions`, `Full`)
- `CupelOtelTraceCollector::SOURCE_NAME` const is `"cupel"`
- Root `cupel.pipeline` span carries `cupel.budget.max_tokens` (i64) and `cupel.verbosity` (string) attributes
- Each of the 5 stage spans carries `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out` attributes; stage spans are children of the root span
- `StageOnly` emits no `cupel.exclusion` or `cupel.item.included` events
- `StageAndExclusions` emits `cupel.exclusion` events with `cupel.exclusion.reason`, `cupel.exclusion.item_kind`, `cupel.exclusion.item_tokens` + `cupel.exclusion.count` attribute on stage spans that have exclusions
- `Full` additionally emits `cupel.item.included` events on the Place stage span with `cupel.item.kind`, `cupel.item.tokens`, `cupel.item.score`
- All 5 spans are explicitly `.end()`ed
- Integration tests in `crates/cupel-otel/tests/integration.rs` using `InMemorySpanExporter` pass for all 3 verbosity tiers
- `cargo test --all-targets` passes in `crates/cupel-otel/`
- `cargo clippy --all-targets -- -D warnings` clean in `crates/cupel-otel/`
- Core `crates/cupel` has zero `opentelemetry` dependency (verified by `cargo tree`)

## Proof Level

- This slice proves: integration
- Real runtime required: yes (real OTel in-memory exporter; real spans captured and asserted)
- Human/UAT required: no

## Verification

- `cd crates/cupel-otel && cargo test --all-targets` → all tests pass, 0 failures
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0, 0 warnings
- `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty (core has no OTel dep)
- `cd crates/cupel && cargo test --all-targets` → 170+ passed, 0 regressions (S01 baseline not broken)
- Integration test `hierarchy_root_and_five_stage_spans` passes: 6 spans total, root `cupel.pipeline` + 5 `cupel.stage.*` children
- Integration test `stage_only_no_events` passes: no events on any span
- Integration test `stage_and_exclusions_emits_exclusion_events` passes: at least 1 `cupel.exclusion` event with all 3 required attributes
- Integration test `full_emits_included_item_events_on_place` passes: `cupel.item.included` events on the place span with kind/tokens/score

## Observability / Diagnostics

- Runtime signals: real OTel spans exported to `InMemorySpanExporter`; test asserts specific span names, attributes by key, event names and tags
- Inspection surfaces: `exporter.get_finished_spans()` returns `Vec<SpanData>`; filter by `span.name` to locate specific spans; `span.attributes` for attribute assertions; `span.events` for event assertions
- Failure visibility: test names encode the failing tier and assertion; `#[serial]` on all tests prevents global tracer provider interference; assertion failures print which attribute/event was missing
- Redaction constraints: no item content or raw metadata in any OTel attribute — only structural fields (kind, tokens, score, reason)

## Integration Closure

- Upstream surfaces consumed: `TraceCollector::on_pipeline_completed`, `StageTraceSnapshot`, `ExclusionReason`, `IncludedItem`, `PipelineStage`, `SelectionReport`, `ContextBudget` — all exported from `cupel` crate root
- New wiring introduced in this slice: `crates/cupel-otel/` crate; `CupelOtelTraceCollector::on_pipeline_completed` calls `global::tracer("cupel")` and emits spans; dependency on `opentelemetry = "0.27"` lives exclusively in `cupel-otel`
- What remains before the milestone is truly usable end-to-end: S03 — `cargo package --dry-run`, Rust section in spec, CHANGELOG, R058 validation

## Tasks

- [x] **T01: Scaffold cupel-otel crate and write failing integration tests** `est:45m`
  - Why: Establishes the crate structure, Cargo.toml with pinned deps, `src/lib.rs` stub types, and a test file with 5 integration tests in failing state — gives T02 and T03 unambiguous pass/fail targets
  - Files: `crates/cupel-otel/Cargo.toml`, `crates/cupel-otel/src/lib.rs`, `crates/cupel-otel/tests/integration.rs`
  - Do: Create `crates/cupel-otel/` directory structure; write `Cargo.toml` with `opentelemetry = "0.27"` in `[dependencies]` and `opentelemetry_sdk = { version = "0.27", features = ["testing", "trace"] }` + `serial_test = "0.9"` in `[dev-dependencies]` and `cupel = { version = "1.1", path = "../cupel" }` in `[dependencies]`; write `src/lib.rs` with `CupelVerbosity` enum and `CupelOtelTraceCollector` struct that implements the `TraceCollector` trait with no-op bodies (all methods compile but do nothing); write `tests/integration.rs` with 5 failing tests using `#[serial]` and `InMemorySpanExporterBuilder`: (1) `hierarchy_root_and_five_stage_spans`, (2) `stage_only_no_events`, (3) `stage_and_exclusions_emits_exclusion_events`, (4) `full_emits_included_item_events_on_place`, (5) `source_name_is_cupel` — each test sets up the in-memory provider, runs a real pipeline with the collector, and asserts span/event structure; these tests must compile but fail at runtime (0 spans captured from the stub)
  - Verify: `cd crates/cupel-otel && cargo build` exits 0; `cargo test --all-targets 2>&1 | grep -E "FAILED|test result"` shows tests compile and run but fail
  - Done when: `crates/cupel-otel/` crate compiles without errors; `cargo test --all-targets` shows 5 failing tests (not compile errors)

- [x] **T02: Implement CupelOtelTraceCollector::on_pipeline_completed** `est:1h`
  - Why: This is the entire behavioral implementation — the `on_pipeline_completed` override that creates the root span, threads context to 5 stage spans, sets all spec-required attributes, and ends spans explicitly; makes all 5 integration tests pass
  - Files: `crates/cupel-otel/src/lib.rs`
  - Do: Replace the no-op `on_pipeline_completed` with the real implementation: (1) call `global::tracer("cupel")` to get the `BoxedTracer`; (2) create root `cupel.pipeline` span via `tracer.start("cupel.pipeline")`; (3) set `cupel.budget.max_tokens` (as `KeyValue::new("cupel.budget.max_tokens", budget.max_tokens())`) and `cupel.verbosity` (verbosity enum name as `&'static str`) on the root span; (4) wrap root in `Context::current_with_span(root)` to establish parent context; (5) iterate `stage_snapshots` — for each, create `cupel.stage.{name}` span via `tracer.start_with_context(name, &root_cx)` where name uses `format!("{:?}", snapshot.stage).to_lowercase()`; (6) set `cupel.stage.name`, `cupel.stage.item_count_in` (as i64), `cupel.stage.item_count_out` (as i64) on each stage span; (7) if verbosity >= `StageAndExclusions` and `!snapshot.excluded.is_empty()`: set `cupel.exclusion.count` attribute + add `cupel.exclusion` event per excluded item with `exclusion_reason_name(&e.reason)`, `e.item.kind().to_string()`, `e.item.tokens() as i64`; (8) if verbosity == `Full` and stage is `PipelineStage::Place`: add `cupel.item.included` event per included item in `report.included` with kind, tokens, score; (9) call `.end()` on each stage span before next iteration; (10) drop `root_cx` (root span ends when context drops — verify this pattern works with the in-memory exporter or call root span's `.end()` explicitly before dropping); implement `exclusion_reason_name(r: &ExclusionReason) -> &'static str` as a `match` with all variants and `_ => "Unknown"`; implement `CupelVerbosity` with `PartialOrd`/`Ord` so `>=` comparisons work, or use explicit `match`; add `SOURCE_NAME: &'static str = "cupel"` const on the struct; `is_enabled()` returns `true` unconditionally (noop tracer handles the disabled path); no-op `record_stage_event` and `record_item_event`
  - Verify: `cd crates/cupel-otel && cargo test --all-targets` → all 5 integration tests pass; `cargo clippy --all-targets -- -D warnings` → exit 0
  - Done when: All 5 integration tests pass; no clippy warnings; `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty

## Files Likely Touched

- `crates/cupel-otel/Cargo.toml` (new)
- `crates/cupel-otel/src/lib.rs` (new)
- `crates/cupel-otel/tests/integration.rs` (new)
