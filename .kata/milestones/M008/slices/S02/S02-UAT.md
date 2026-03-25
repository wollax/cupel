# S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers) — UAT

**Milestone:** M008
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All observable behavior is in span structure, attribute names/values, and event presence — fully captured by the `InMemorySpanExporter` integration tests. No interactive UI, no deployed service, no human-experience surface. The in-memory exporter integration tests ARE the runtime verification.

## Preconditions

- Rust toolchain installed (`cargo --version` → 1.70+)
- `crates/cupel-otel/` exists with `Cargo.toml`, `src/lib.rs`, `tests/integration.rs`
- `crates/cupel/` baseline (S01 outputs: `on_pipeline_completed` hook + `StageTraceSnapshot`) is on the branch

## Smoke Test

```bash
cd crates/cupel-otel && cargo test --all-targets
```
Expected: `test result: ok. 5 passed; 0 failed`

## Test Cases

### 1. All 5 integration tests pass

1. `cd crates/cupel-otel`
2. `cargo test --all-targets`
3. **Expected:** `5 passed; 0 failed` — tests are `source_name_is_cupel`, `hierarchy_root_and_five_stage_spans`, `stage_only_no_events`, `stage_and_exclusions_emits_exclusion_events`, `full_emits_included_item_events_on_place`

### 2. Clippy clean

1. `cd crates/cupel-otel`
2. `cargo clippy --all-targets -- -D warnings`
3. **Expected:** exit 0, zero warnings

### 3. Core crate has no OTel dependency

1. `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry`
2. **Expected:** empty output (no lines printed)

### 4. Core cupel tests unaffected

1. `cd crates/cupel && cargo test --all-targets`
2. **Expected:** 170+ passed, 0 failed, 0 regressions from S01

### 5. StageOnly emits no events

1. Run: `cd crates/cupel-otel && cargo test stage_only_no_events -- --nocapture`
2. **Expected:** PASS — the test asserts 0 events on all 6 spans

### 6. StageAndExclusions emits exclusion events on tight-budget run

1. Run: `cd crates/cupel-otel && cargo test stage_and_exclusions_emits_exclusion_events -- --nocapture`
2. **Expected:** PASS — exclusion events have `cupel.exclusion.reason`, `cupel.exclusion.item_kind`, `cupel.exclusion.item_tokens` attributes

### 7. Full tier emits included-item events on Place span

1. Run: `cd crates/cupel-otel && cargo test full_emits_included_item_events_on_place -- --nocapture`
2. **Expected:** PASS — `cupel.item.included` events on Place span have `cupel.item.kind`, `cupel.item.tokens`, `cupel.item.score` attributes

## Edge Cases

### Span hierarchy: stage spans are children of root

1. Run: `cd crates/cupel-otel && cargo test hierarchy_root_and_five_stage_spans -- --nocapture`
2. **Expected:** PASS — test asserts 6 spans total, root named `cupel.pipeline`, 5 children named `cupel.stage.*` each with `parent_span_id == root.span_context.span_id()`

### Source name: instrumentation scope is "cupel"

1. Run: `cd crates/cupel-otel && cargo test source_name_is_cupel -- --nocapture`
2. **Expected:** PASS — all spans have `instrumentation_scope.name() == "cupel"`

## Failure Signals

- Any test shows `FAILED` → implementation regression; run `cargo test -- --nocapture` for assertion details
- `cargo tree | grep opentelemetry` produces output → OTel dep leaked into core (critical: must be zero)
- `cargo clippy` produces warnings or errors → lint regression
- `cd crates/cupel && cargo test --all-targets` shows failures → S01 wiring broken

## Requirements Proved By This UAT

- R058 (partially) — `cupel-otel` crate exists with `CupelOtelTraceCollector` implementing `TraceCollector`; all three verbosity tiers (StageOnly, StageAndExclusions, Full) verified via in-memory exporter; core `cupel` crate has zero `opentelemetry` production dependency; `cargo clippy` clean; 170+ core tests pass (no regression)

## Not Proven By This UAT

- `cargo package --dry-run` exit 0 for `cupel-otel` — deferred to S03
- Rust-specific section in `spec/src/integrations/opentelemetry.md` — deferred to S03
- `CHANGELOG.md` entry — deferred to S03
- R058 final validation in `REQUIREMENTS.md` — deferred to S03
- Integration with a real OTel backend (Jaeger, Honeycomb, etc.) — not in scope for this milestone; in-memory exporter proves structural correctness

## Notes for Tester

- All integration tests use `#[serial]` — they must run serially due to global OTel tracer provider state. Running with `--test-threads=1` is safe; parallel execution may produce flaky results.
- The tight-budget test helper (`run_pipeline_with_exclusions_and_capture`) uses a 60-token budget with 3 items of 40 tokens each — only 1 item fits, forcing 2 exclusions.
- The "source name is cupel" test checks `instrumentation_scope.name()` (method call, not field access).
