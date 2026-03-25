# Kata State

**Active Milestone:** M008 — Rust OpenTelemetry Bridge
**Active Slice:** S01 — Add on_pipeline_completed hook to core cupel TraceCollector
**Active Task:** (not yet planned)
**Phase:** Planning

## Recent Decisions

- D165: `StageTraceSnapshot` carries `excluded: Vec<ExcludedItem>` scoped to that stage — OTel collector emits exclusion events from snapshot data, not by re-scanning the full report
- D164: `on_pipeline_completed` defaulted no-op on TraceCollector trait — Rust Span trait is not dyn-compatible; structured end-of-run handoff is the only viable pattern
- D163: Canonical OTel source name is `"cupel"` (not `"Wollax.Cupel"`) — Rust crate name convention
- D162: Direct `opentelemetry` crate API (not `tracing` bridge) — full control over span hierarchy; easier to test
- D161: `cupel-otel` is a separate crate — R032 extends to OTel; core stays zero-dep

## Blockers

- None

## Milestone Progress (M008)

- [ ] S01: Add on_pipeline_completed hook to core cupel TraceCollector
- [ ] S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers)
- [ ] S03: Crate packaging, spec addendum, and R058 validation

## Next Action

Plan S01: write S01-PLAN.md decomposing the `TraceCollector` hook addition and `StageTraceSnapshot` struct into tasks. S01 is the highest-risk slice (core crate change) and the prerequisite for S02.
