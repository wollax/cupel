---
id: T01
parent: S01
milestone: M002
provides:
  - count-quota-ideas.md — 238-line explorer-mode brainstorm across all 5 Decision-6 open questions plus post-v1.2 exclusion diagnostic angle
  - count-quota-report.md — 376-line challenger report with 28 verdict entries, D040 applied, and 6 precisely-framed downstream inputs for S03
key_files:
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md
key_decisions:
  - Tag non-exclusivity (DI-2) identified as hardest open question — semantics are a public contract that cannot change after count-quota ships without a breaking version bump
  - Phantom ExclusionReason items for CountRequireUnmet rejected — breaks Excluded list invariant; TraceEvent or SelectionReport.quota_violations are the correct representations
  - KnapsackSlice + CountQuotaSlice combination: 5E (build-time rejection) recommended as safe default for v1; 5A or 5D as upgrade paths
  - Two-phase algorithm (count-satisfy first, then DISTRIBUTE-BUDGET on remainder) accepted as the base algorithm architecture
patterns_established:
  - Explorer/challenger debate format: ideas.md (uncensored) → report.md (challenged, scoped, verdict table)
  - Locked decisions (D040) applied by noting the locked constraint and redirecting to what S03 must specify within that constraint, not reopening the debate
observability_surfaces:
  - none (documentation-only task)
duration: 1 session
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Count-quota angles pair

**Explorer/challenger pair for count-quota design questions committed to `.planning/brainstorms/2026-03-21T09-00-brainstorm/`.**

## What Happened

Read three source artifacts before writing:
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decision 6 quoted directly as the framing preamble in `count-quota-ideas.md`
- `spec/src/slicers/quota.md` — `DISTRIBUTE-BUDGET` pseudocode understood; the two-phase algorithm (Idea 1C) was shaped to dovetail with the existing budget distribution structure
- `crates/cupel/src/slicer/quota.rs` — `QuotaEntry { require: f64, cap: f64 }` structure understood; the contrast between percentage units and count units informed why Idea 1B (extending QuotaEntry with count fields) is architecturally unsound

Also read:
- `.kata/DECISIONS.md` — D040 confirmed as locked; applied explicitly in Section 4 and wherever cross-kind conflict detection arose
- `.kata/milestones/M002/slices/S01/S01-RESEARCH.md` — "New Ideas to Explore" section informed the post-v1.2 exclusion diagnostic angle in Section 6

**Explorer pass** (`count-quota-ideas.md`): 28 raw proposals across 6 sections — one per Decision-6 open question (algorithm integration, tag non-exclusivity, pinned interaction, conflict detection, KnapsackSlice compatibility) plus a sixth section on post-v1.2 exclusion diagnostic variants. Explorer mode applied faithfully: D040 ideas appear uncensored.

**Challenger pass** (`count-quota-report.md`): All 28 proposals debated with challenge/response/verdict structure. D040 applied explicitly in Section 4 (Idea 4B redirected with "D040 locked"). Verdict table in summary. "Downstream inputs for S03" section contains 6 precisely-framed design questions (DI-1 through DI-6), with DI-2 (tag non-exclusivity) marked as hardest with explicit reasoning.

## Verification

```
ls .planning/brainstorms/2026-03-21T09-00-brainstorm/
# → count-quota-ideas.md  count-quota-report.md  ✓

wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md
# → 238  (≥ 80 ✓)

wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md
# → 376  (≥ 80 ✓)

grep -c "Downstream inputs for S03" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md
# → 1  (≥ 1 ✓)

grep -c "TBD" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md
# → 0  ✓

grep -c "TBD" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md
# → 0  ✓
```

All must-haves confirmed:
- [x] Brainstorm directory exists
- [x] `ideas.md` covers all five open questions from Decision 6 plus post-v1.2 exclusion diagnostic angle (Section 6)
- [x] `ideas.md` does not pre-filter by locked decisions (D040 ideas appear in Section 4 uncensored)
- [x] `report.md` applies D040 explicitly (Section 4, Idea 4B: "D040 locked" with redirect)
- [x] `report.md` contains "Downstream inputs for S03" section with 6 scoped design inputs (DI-1 through DI-6)
- [x] Neither file contains "TBD" without explanation

**Slice-level verification (partial — T01 is the first task):**
- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` — shows 2 files; full 9-file requirement is a slice-level check for T04
- `grep -c "Downstream inputs" SUMMARY.md` — SUMMARY.md not yet written (T04 responsibility)
- `cargo test` and `dotnet test` — not run; documentation-only task; no code changes

## Diagnostics

Inspect with:
- `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — full challenger report with verdicts
- `grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — list all downstream inputs for S03
- `grep "Verdict" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — all 28 verdicts
- `grep "D040 locked" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — confirm D040 application

## Deviations

The task plan specified ≥5 downstream inputs for S03. The challenger produced 6 (DI-1 through DI-6). DI-6 (SelectionReport backward-compatibility precondition) was added because the diagnostic mechanism choice (DI-3) cannot be resolved without it — treating it as a prerequisite rather than leaving an implicit dependency in DI-3.

## Known Issues

None.

## Files Created/Modified

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md` — 238-line explorer-mode brainstorm; 28 raw proposals across 6 sections; no pre-filtering for locked decisions
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — 376-line challenger report; 28 verdict entries; "Downstream inputs for S03" section with 6 design questions; DI-2 (tag non-exclusivity) identified as hardest
