# Kata State

**Active Milestone:** M008 — Rust OpenTelemetry Bridge
**Active Slice:** S03 — Crate packaging, spec addendum, and R058 validation
**Active Task:** none (S03 not yet planned)
**Phase:** S02 done; S03 is next

## Recent Decisions

- D171: `ExclusionReason` variant name extracted via `match` returning `&'static str`, not `Debug` formatting
- D170: `cupel-otel` root span carries only `cupel.budget.max_tokens` and `cupel.verbosity` (spec-only — not .NET extras)
- D169: Root span requires explicit `.end()` call via `root_cx.span().end()` — not drop-based
- D168: S02 verification strategy — integration-level with 5 failing integration tests using `InMemorySpanExporter`
- D165: `StageTraceSnapshot.excluded` carries stage-scoped excluded items (same as .NET GetExclusionsForStage)

## Blockers

- None

## Milestone Progress (M008)

- [x] S01: Add on_pipeline_completed hook to core cupel TraceCollector ✓
- [x] S02: Implement CupelOtelTraceCollector (all 3 verbosity tiers) ✓
- [ ] S03: Crate packaging, spec addendum, and R058 validation

## Next Action

Begin S03: run `cargo package --dry-run` for `cupel-otel`; add Rust-specific section to `spec/src/integrations/opentelemetry.md`; update `CHANGELOG.md`; validate R058 in `.kata/REQUIREMENTS.md`. Plan S03 tasks from `.kata/milestones/M008/M008-ROADMAP.md`.
