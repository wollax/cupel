# Kata State

**Active Milestone:** M004 — v1.4 Diagnostics & Simulation Parity
**Active Slice:** None — planning complete, ready to start S01
**Active Task:** None
**Phase:** Ready to plan S01
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Plan S01 (SelectionReport structural equality) — decompose into tasks, write S01-PLAN.md and task plans, then execute.
**Last Updated:** 2026-03-23 (M004 roadmap written)

## M004 Progress

| Slice | Title | Status |
|-------|-------|--------|
| S01 | SelectionReport structural equality | ⏳ not started |
| S02 | PolicySensitivityReport — fork diagnostic | ⏳ not started |
| S03 | IQuotaPolicy abstraction + QuotaUtilization | ⏳ not started |
| S04 | Snapshot testing in Cupel.Testing | ⏳ not started |
| S05 | Rust budget simulation parity | ⏳ not started |

## Recent Decisions

- D103: SelectionReport equality uses exact f64 comparison (no epsilon)
- D104: PolicySensitivityReport returns labeled reports + structured diff
- D105: QuotaUtilization uses IQuotaPolicy abstraction
- D106: Snapshot testing uses JSON format with CUPEL_UPDATE_SNAPSHOTS=1 env var
- D107: Rust snapshot testing out of scope (use insta crate)

## Blockers

- None
