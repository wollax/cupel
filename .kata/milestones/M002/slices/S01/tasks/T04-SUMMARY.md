---
id: T04
parent: S01
milestone: M002
provides:
  - post-v1-2-ideas.md — explorer-mode re-evaluation of 5 deferred items + 7 new post-v1.2-specific ideas
  - post-v1-2-report.md — challenger verdicts for all deferred items and new ideas; "New backlog candidates" table with 13 M003+ items
  - SUMMARY.md — session synthesis with 5 required sections: session metadata, pairs table, downstream inputs (S03/S05/S06), deferred-items status, new backlog candidates
key_files:
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md
key_decisions:
  - Fork diagnostic (PolicySensitivityReport) is M003-ready — dry_run now live in both languages was the sole prerequisite
  - BudgetUtilization and KindDiversity belong in Wollax.Cupel core (not a separate analytics package) — avoids OTel-depends-on-Analytics dependency chain; overrides T02's Wollax.Cupel.Analytics recommendation
  - CountRequireUnmet is a report-level field (not a per-item ExclusionReason) — no item to attach it to; CountCapExceeded is the correct per-item variant
  - IntentSpec as preset selector and ProfiledPlacer both confirmed M003+ (no new signal from v1.2)
  - Snapshot testing path identified but still M003+ — requires tiebreak spec rule + GreedySlice default change first
patterns_established:
  - Deferred-items re-evaluation format: "What changed since March 15" framing per item, ending with verdict from {M003 ready / M003+ defer / Drop / Reassigned}
  - SUMMARY.md serves as session synthesis only — no new content; all downstream-input sections derived from pair reports
  - Placement revision in later pairs overrides earlier pair recommendations when architectural reasoning is stronger
observability_surfaces:
  - cat .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md — full session synthesis with downstream inputs and backlog candidates
  - cat .planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md — fourth-pair challenger verdicts
  - grep "New Backlog Candidates" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md — 13-item M003+ backlog table
duration: ~45min
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T04: Post-v1.2 ideas pair + deferred re-evaluation + SUMMARY.md + regression verify

**Committed all 9 brainstorm files; 5 deferred items re-evaluated; fork diagnostic advanced to M003-ready; 13 new M003+ backlog candidates cataloged; 113 Rust + 583 .NET tests pass.**

## What Happened

**Step 1 — post-v1-2-ideas.md (explorer mode):**
Section A re-evaluated all five deferred items with "What changed since March 15" framing:
- Fork diagnostic (A1): `dry_run` is now the key enabler. `PolicySensitivityReport` is thin orchestration (~80 lines) over the existing API. M003-ready.
- Extension methods (A2): T02's verdict confirmed and clarified — `BudgetUtilization` + `KindDiversity` belong in `Wollax.Cupel` core (not a separate analytics package), avoiding OTel dependency chain issues.
- IntentSpec (A3): No new signal. Still 20 lines of caller code. Still deferred.
- ProfiledPlacer (A4): Three original blockers unchanged. Confirmed still deferred.
- Snapshot testing (A5): Path identified — spec tiebreak rule (sort by id ascending) + default in `GreedySlice`. But both are spec changes outside M002 scope.

Section B generated 7 new post-v1.2-specific ideas: QuotaUtilization metric (B1), timestamp coverage (B2, confirmed from T03 DI-1), count-quota ExclusionReason variants (B3), multi-budget dry_run re-examination (B4, confirmed rejected), SelectionReport equality (B5), KnapsackSlice OOM error structure (B6), MetadataKeyScorer (B7).

**Step 2 — post-v1-2-report.md (challenger mode):**
Verdicts for all deferred items: fork diagnostic → M003 ready; extension methods → Reassigned (S05 + core); IntentSpec → M003+ defer; ProfiledPlacer → M003+ defer; snapshot testing → M003+ defer (path identified). New idea verdicts and 13-item "New backlog candidates" table produced.

Key architectural clarification: `CountRequireUnmet` rejected as `ExclusionReason` (no item to attach it to) — should be a `SelectionReport`-level field. `CountCapExceeded` and `CountRequireCandidatesExhausted` accepted as per-item variants.

**Step 3 — SUMMARY.md:**
Session synthesis following March 15 template format. Five required sections all present: session metadata and pairs table, downstream inputs (S03/S05/S06) derived from pair reports, deferred-items status table, new backlog candidates table, cross-cutting themes. No new content introduced — pure synthesis.

Downstream inputs are cross-referenced from T01-T04 pair reports. Placement revision from post-v1-2-report (BudgetUtilization in Wollax.Cupel core) is noted in S05's DI-5.

**Step 4 — Regression verification:**
- `cargo test --manifest-path crates/cupel/Cargo.toml`: 113 passed, 0 failed ✅
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`: 583 passed, 0 failed ✅

**Step 5 — Commit:**
`git commit -m "docs(S01): add brainstorm 2026-03-21T09-00"` — 3 new files (621 insertions). All 9 brainstorm files now committed across T01-T04.

## Verification

- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/ | wc -l` → 9 ✅
- `grep -c "Downstream Inputs" SUMMARY.md` → 1 ✅
- `grep -c "New Backlog Candidates" SUMMARY.md` → 1 ✅
- `grep -c "Deferred Items" SUMMARY.md` → 1 ✅
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed ✅
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed ✅
- `git log --oneline -1` → `6787ed1 docs(S01): add brainstorm 2026-03-21T09-00` ✅

## Diagnostics

- `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` — full session synthesis with S03/S05/S06 downstream inputs and M003+ backlog candidates
- `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md` — deferred-item verdicts and scope-creep guard
- `grep "Deferred Items Status" -A 30 .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` — five deferred items with March 15 → post-v1.2 verdicts

## Deviations

**rtk dotnet test incompatibility:** `rtk dotnet test` exits 1 with "Zero tests ran" when run against the TUnit-based test project. The TUnit CLI uses a different argument format that `rtk` transforms in a way that confuses the test runner. The raw `dotnet test --project` command exits 0 with 583 tests passing. This is a pre-existing environmental issue with the `rtk` wrapper and is not a regression introduced by this task.

**Placement revision (analytics in Wollax.Cupel core):** The T04 challenger report overrides T02's recommendation to place `BudgetUtilization` and `KindDiversity` in `Wollax.Cupel.Analytics`. The stronger argument (avoid OTel → analytics dependency chain) warrants the revision. Updated in SUMMARY.md's S05 DI-5.

## Known Issues

None. This is a documentation-only task. All must-haves verified.

## Files Created/Modified

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md` — explorer-mode pair document: 5 deferred re-evaluations (Section A) + 7 new post-v1.2 ideas (Section B)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md` — challenger report: verdicts for deferred items (M003/M003+/Reassigned), verdicts for new ideas, 13-item new backlog candidates table
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` — session synthesis: pairs table, S03/S05/S06 downstream inputs, deferred items status, new backlog candidates, 5 cross-cutting themes
- `.kata/milestones/M002/slices/S01/S01-PLAN.md` — T04 marked [x]
- `.kata/STATE.md` — S01 marked complete; active task cleared; next action points to S02
