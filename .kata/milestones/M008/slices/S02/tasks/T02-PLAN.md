---
estimated_steps: 8
estimated_files: 1
---

# T02: Implement CupelOtelTraceCollector::on_pipeline_completed

**Slice:** S02 — Implement CupelOtelTraceCollector (all 3 verbosity tiers)
**Milestone:** M008

## Description

Replaces the no-op `on_pipeline_completed` stub from T01 with the full behavioral implementation. This is the only file changed: `crates/cupel-otel/src/lib.rs`. When done, all 5 integration tests from T01 pass.

The implementation follows the research cheatsheet exactly:
1. Get `BoxedTracer` from `global::tracer("cupel")`.
2. Create root `cupel.pipeline` span; set 2 spec-required attributes; wrap in `Context::current_with_span`.
3. For each `StageTraceSnapshot` in order: create `cupel.stage.{name}` child span (via `start_with_context`), set 3 spec attributes, emit exclusion events (if `StageAndExclusions`+), emit included-item events (if `Full` + Place stage), call `.end()` explicitly.
4. End the root span by calling `.end()` before dropping the context.

Key constraints from D164/D165/spec:
- Root span attributes: only `cupel.budget.max_tokens` (i64) and `cupel.verbosity` (string)
- Stage span attributes: `cupel.stage.name`, `cupel.stage.item_count_in` (i64), `cupel.stage.item_count_out` (i64)
- `StageAndExclusions`: `cupel.exclusion.count` on stage span + `cupel.exclusion` events from `snapshot.excluded` (not by re-scanning `report`)
- `Full`: `cupel.item.included` events from `report.included` on the Place stage only
- `ExclusionReason` variant name: `match` helper returning `&'static str`, not `Debug` output
- `is_enabled()` returns `true` unconditionally — noop global tracer handles the disabled path
- No content, no metadata in any attribute — structural fields only

## Steps

1. Add imports at the top of `src/lib.rs`: `opentelemetry::{global, trace::{Span, Tracer, TraceContextExt}, Context, KeyValue}`, `cupel::{TraceCollector, SelectionReport, ContextBudget, StageTraceSnapshot, ExclusionReason, PipelineStage}`.

2. Add a private free function `exclusion_reason_name(r: &ExclusionReason) -> &'static str` with a `match` covering all known `ExclusionReason` variants (10 named variants + `_Unknown` and `_ => "Unknown"` fallback). Use `..` struct patterns for data-carrying variants (e.g. `ExclusionReason::BudgetExceeded { .. } => "BudgetExceeded"`).

3. Add a private free function `stage_name(stage: PipelineStage) -> &'static str` returning `"classify"`, `"score"`, `"deduplicate"`, `"slice"`, `"place"` for each variant — avoids format! at runtime and satisfies clippy. Use `_ => "unknown"` fallback (non_exhaustive).

4. Replace the no-op `on_pipeline_completed` with the real implementation:
   - Guard: if `stage_snapshots.is_empty() { return; }`
   - `let tracer = global::tracer(Self::SOURCE_NAME);`
   - `let mut root = tracer.start("cupel.pipeline");`
   - `root.set_attribute(KeyValue::new("cupel.budget.max_tokens", budget.max_tokens()));`
   - `root.set_attribute(KeyValue::new("cupel.verbosity", verbosity_name(self.verbosity)));`
   - `let root_cx = Context::current_with_span(root);`

5. After setting root attributes, hold a reference to end the root span explicitly. Best pattern: use `with_context` block or keep `root` as a variable separate from the context. Since `Context::current_with_span` takes ownership of the span, use `root_cx.span()` method to call `.end()` after all stage spans complete. **Important**: Rust `Span` does NOT auto-end on drop in opentelemetry 0.27 — if `.end()` is not called, the span never appears in the exporter. Call `root_cx.span().end()` after the stage loop. Then iterate `stage_snapshots`:
   - `let name = stage_name(snapshot.stage);`
   - `let mut span = tracer.start_with_context(format!("cupel.stage.{name}"), &root_cx);`
   - Set 3 attributes: `cupel.stage.name` (name as string), `cupel.stage.item_count_in` (snapshot.item_count_in as i64), `cupel.stage.item_count_out` (snapshot.item_count_out as i64)

6. Exclusion events (inside stage iteration, after attributes):
   ```rust
   if self.verbosity >= CupelVerbosity::StageAndExclusions && !snapshot.excluded.is_empty() {
       span.set_attribute(KeyValue::new("cupel.exclusion.count", snapshot.excluded.len() as i64));
       for excluded in &snapshot.excluded {
           span.add_event("cupel.exclusion", vec![
               KeyValue::new("cupel.exclusion.reason", exclusion_reason_name(&excluded.reason)),
               KeyValue::new("cupel.exclusion.item_kind", excluded.item.kind().to_string()),
               KeyValue::new("cupel.exclusion.item_tokens", excluded.item.tokens()),
           ]);
       }
   }
   ```

7. Included-item events (after exclusion block, still inside stage iteration):
   ```rust
   if self.verbosity == CupelVerbosity::Full && snapshot.stage == PipelineStage::Place {
       for included in &report.included {
           span.add_event("cupel.item.included", vec![
               KeyValue::new("cupel.item.kind", included.item.kind().to_string()),
               KeyValue::new("cupel.item.tokens", included.item.tokens()),
               KeyValue::new("cupel.item.score", included.score),
           ]);
       }
   }
   ```
   After events: `span.end();`

8. Add a private helper `verbosity_name(v: CupelVerbosity) -> &'static str` returning `"StageOnly"`, `"StageAndExclusions"`, or `"Full"`. Add `PartialOrd`/`Ord` derives to `CupelVerbosity` (discriminant order: StageOnly=0, StageAndExclusions=1, Full=2) to enable `>=` comparisons.

## Must-Haves

- [ ] `on_pipeline_completed` creates a root `cupel.pipeline` span with `cupel.budget.max_tokens` and `cupel.verbosity` attributes
- [ ] 5 stage child spans named `cupel.stage.classify`, `cupel.stage.score`, etc. are created via `start_with_context` with the root context — they appear as children in the exported spans
- [ ] Each stage span carries `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out` (all present, correct types)
- [ ] All 5 stage spans have `.end()` called explicitly before the next iteration
- [ ] Root span has `.end()` called explicitly after all stage spans (NOT relying on drop — opentelemetry 0.27 requires explicit `.end()` for spans to appear in the exporter)
- [ ] `StageOnly` tier: `exporter.get_finished_spans()` → 6 spans total, 0 events on any span
- [ ] `StageAndExclusions` tier with excluded items: `cupel.exclusion` events on the stage span where `snapshot.excluded` is non-empty; each event has `cupel.exclusion.reason`, `cupel.exclusion.item_kind`, `cupel.exclusion.item_tokens`; `cupel.exclusion.count` attribute is present on that stage span
- [ ] `Full` tier: `cupel.item.included` events on the `cupel.stage.place` span with `cupel.item.kind`, `cupel.item.tokens`, `cupel.item.score`
- [ ] `exclusion_reason_name` never panics — `_ => "Unknown"` catch-all covers future variants
- [ ] `cargo test --all-targets` in `crates/cupel-otel/` → all 5 integration tests pass
- [ ] `cargo clippy --all-targets -- -D warnings` → exit 0
- [ ] `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty

## Verification

- `cd crates/cupel-otel && cargo test --all-targets` → all 5 tests pass, 0 failures
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0
- `cd crates/cupel-otel && cargo test -- --nocapture 2>&1 | grep -E "test .* ok"` → 5 passing test names
- `cd crates/cupel && cargo test --all-targets` → 170+ passed, 0 regressions
- `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty

## Observability Impact

- Signals added/changed: `CupelOtelTraceCollector` now emits real OTel spans via `global::tracer("cupel")`; any consumer with a tracer provider configured receives spans
- How a future agent inspects this: `cd crates/cupel-otel && cargo test -- --nocapture` shows which specific assertions pass/fail; assertion messages name the missing attribute key or event name
- Failure state exposed: If a span attribute is missing, the test assertion prints the expected key and the span's actual attributes; if span hierarchy is wrong, `span.parent_span_id` comparison fails with expected vs actual IDs

## Inputs

- `crates/cupel-otel/src/lib.rs` (from T01) — stub `CupelOtelTraceCollector` with no-op `on_pipeline_completed`
- `crates/cupel-otel/tests/integration.rs` (from T01) — 5 failing integration tests defining exact assertions to satisfy
- S02-RESEARCH.md Appendix (API Cheatsheet) — exact `opentelemetry` 0.27 API: `global::tracer`, `tracer.start`, `tracer.start_with_context`, `Context::current_with_span`, `span.set_attribute(KeyValue::new(...))`, `span.add_event(name, vec![...])`, `span.end()`
- S01 Forward Intelligence — confirms `StageTraceSnapshot.excluded` is stage-scoped; `report.included` for Full-tier included-item events; synthetic `SelectionReport` is safe to use (events field empty, count_requirement_shortfalls empty)

## Expected Output

- `crates/cupel-otel/src/lib.rs` — fully implemented `CupelOtelTraceCollector::on_pipeline_completed`, `exclusion_reason_name`, `stage_name`, `verbosity_name` helpers; `CupelVerbosity` with `PartialOrd`/`Ord`
- All 5 integration tests in `crates/cupel-otel/tests/integration.rs` pass
- `cargo clippy --all-targets -- -D warnings` clean in both `crates/cupel-otel/` and `crates/cupel/`
