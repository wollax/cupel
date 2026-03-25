# Kata State

**Active Milestone:** M008 — Rust OpenTelemetry Bridge
**Active Slice:** S02 — Implement CupelOtelTraceCollector (all 3 verbosity tiers)
**Active Task:** T01 (not yet planned)
**Phase:** Planning (S02)

## Recent Decisions

- D167: Synthetic SelectionReport for `on_pipeline_completed` uses union of snapshot excluded items — avoids re-scanning; `events = vec![]`, `count_requirement_shortfalls = vec![]`
- D166: S01 verification strategy — integration-level with failing-first `SpyCollector` test; T01 writes failing test, T02 makes it pass
- D165: `StageTraceSnapshot` carries `excluded: Vec<ExcludedItem>` scoped to that stage
- D164: `on_pipeline_completed` defaulted no-op on TraceCollector trait
- D163: Canonical OTel source name is `"cupel"`

## Blockers

- None

## Milestone Progress (M008)

- [x] S01: Add on_pipeline_completed hook to core cupel TraceCollector ✓
- [ ] S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers)
- [ ] S03: Crate packaging, spec addendum, and R058 validation

## Next Action

Begin S02: Research the `opentelemetry` Rust crate API (version, span creation, attributes, in-memory exporter), then plan and implement `CupelOtelTraceCollector` in `crates/cupel-otel/`. Key entry point: `on_pipeline_completed` override builds `cupel.pipeline` root span + 5 `cupel.stage.*` child spans with correct attributes for all 3 verbosity tiers (StageOnly, StageAndExclusions, Full).
