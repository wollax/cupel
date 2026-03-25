---
id: T02
parent: S02
milestone: M008
provides:
  - Full CupelOtelTraceCollector::on_pipeline_completed implementation emitting real OTel spans
  - Root cupel.pipeline span with cupel.budget.max_tokens (i64) and cupel.verbosity (string) attributes
  - Five cupel.stage.* child spans via start_with_context with root context, each with stage.name/item_count_in/item_count_out attributes
  - StageAndExclusions tier: cupel.exclusion events per excluded item with reason/item_kind/item_tokens; cupel.exclusion.count attribute on stage span
  - Full tier: cupel.item.included events on the Place stage span with kind/tokens/score attributes
  - PartialOrd/Ord on CupelVerbosity enabling >= comparisons for tier-gating logic
  - Private helpers: verbosity_name, stage_name, exclusion_reason_name (all returning &'static str)
key_files:
  - crates/cupel-otel/src/lib.rs
key_decisions:
  - Root span ended via root_cx.span().end() — opentelemetry 0.27 requires explicit .end() call; spans not ended never appear in the exporter
  - CupelVerbosity derives PartialOrd/Ord (discriminant order StageOnly < StageAndExclusions < Full) for >= tier comparisons rather than match
  - Stage child spans created with tracer.start_with_context(format!("cupel.stage.{name}"), &root_cx) — parent wired via context, not explicit parent ID
  - SOURCE_NAME constant used directly (not via Self::SOURCE_NAME) in on_pipeline_completed — avoids method call in non-method context
patterns_established:
  - OTel span hierarchy via Context::current_with_span — root context established once, all stage spans use start_with_context with root_cx
  - Explicit .end() on every span before drop — never rely on Drop for span finalization in opentelemetry 0.27
observability_surfaces:
  - cd crates/cupel-otel && cargo test -- --nocapture shows pass/fail for all 5 integration tests
  - exporter.get_finished_spans() returns Vec<SpanData>; filter by span.name for specific span; span.attributes for attribute assertions; span.events for event assertions
  - Test assertion messages name the expected key and actual attributes on failure
duration: 15min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Implement CupelOtelTraceCollector::on_pipeline_completed

**Real OTel span emission: root cupel.pipeline + 5 cupel.stage.* children with attributes and tier-gated events, all 5 integration tests pass**

## What Happened

Replaced the no-op `on_pipeline_completed` stub with the full implementation in `crates/cupel-otel/src/lib.rs`. The implementation:

1. Gets a `BoxedTracer` from `global::tracer(SOURCE_NAME)`.
2. Creates the root `cupel.pipeline` span, sets `cupel.budget.max_tokens` and `cupel.verbosity` attributes, wraps in `Context::current_with_span`.
3. Iterates `stage_snapshots`: creates `cupel.stage.{name}` child spans via `start_with_context` with the root context, sets 3 stage attributes, emits exclusion events at `StageAndExclusions`+ tier, emits included-item events at `Full` tier on the Place stage, calls `span.end()` explicitly.
4. Calls `root_cx.span().end()` after the stage loop — critical because opentelemetry 0.27 does not end spans on drop.

Added `PartialOrd`/`Ord` derives to `CupelVerbosity` to enable the `>= CupelVerbosity::StageAndExclusions` tier comparison. Added `verbosity_name`, `stage_name`, and `exclusion_reason_name` private helpers. The `exclusion_reason_name` function was already present in the stub (moved from a public to private helper section); `stage_name` and `verbosity_name` are new.

## Verification

- `cd crates/cupel-otel && cargo test --all-targets` → 5/5 integration tests pass, 0 failures
- `cd crates/cupel-otel && cargo clippy --all-targets -- -D warnings` → exit 0, 0 warnings
- `cargo tree --manifest-path crates/cupel/Cargo.toml | grep opentelemetry` → empty (no OTel dep in core)
- `cd crates/cupel && cargo test --all-targets` → 170 tests pass, 0 failures (no regressions)

Tests passing:
- `source_name_is_cupel` — all spans have instrumentation_scope.name() == "cupel"
- `hierarchy_root_and_five_stage_spans` — 6 spans total, root cupel.pipeline + 5 cupel.stage.* children with correct parent_span_id
- `stage_only_no_events` — 6 spans, 0 events on any span
- `stage_and_exclusions_emits_exclusion_events` — cupel.exclusion events with reason/item_kind/item_tokens on tight-budget pipeline
- `full_emits_included_item_events_on_place` — cupel.item.included events with kind/tokens/score on place stage span

## Diagnostics

- `cd crates/cupel-otel && cargo test -- --nocapture` shows test names and assertion failures
- `exporter.get_finished_spans()` returns all spans; filter by `span.name` to find specific spans
- Assertion failures print the expected key and `span.attributes` contents for fast diagnosis

## Deviations

None. Implementation followed the task plan exactly.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel-otel/src/lib.rs` — Full CupelOtelTraceCollector implementation: on_pipeline_completed with OTel span emission, PartialOrd/Ord on CupelVerbosity, verbosity_name/stage_name helpers
