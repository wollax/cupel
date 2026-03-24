# Kata State

**Active Milestone:** M004 — v1.4 Diagnostics & Simulation Parity
**Active Slice:** S04 — Snapshot testing in Cupel.Testing
**Active Task:** None — S04 not yet started
**Phase:** Planning
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Plan S04 — decompose snapshot testing slice into tasks with must-haves.
**Last Updated:** 2026-03-23 (S03 complete — IQuotaPolicy + QuotaUtilization in both languages)

## M004 Progress

| Slice | Title | Status |
|-------|-------|--------|
| S01 | SelectionReport structural equality | ✅ complete |
| S02 | PolicySensitivityReport — fork diagnostic | ✅ complete |
| S03 | IQuotaPolicy abstraction + QuotaUtilization | ✅ complete |
| S04 | Snapshot testing in Cupel.Testing | ⏳ not started |
| S05 | Rust budget simulation parity | ⏳ not started |

## Recent Decisions

- D115: S03 verification strategy — contract + integration; tests exercise both quota types
- D116: QuotaConstraint uses f64 for both percentage and count modes
- D117: quota_utilization requires explicit ContextBudget parameter (percentage mode needs target_tokens)

## Blockers

- None
