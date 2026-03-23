---
id: S01
parent: M002
milestone: M002
provides:
  - count-quota-ideas.md + count-quota-report.md — 28-proposal explorer/challenger pair framing the 5 Decision-6 open questions for S03
  - testing-vocabulary-ideas.md + testing-vocabulary-report.md — 25-proposal explorer/challenger pair producing 15 vocabulary candidates for S05
  - future-features-ideas.md + future-features-report.md — 22-proposal explorer/challenger pair producing 18 S06 mandate items across DecayScorer, OTel, and Budget Simulation
  - post-v1-2-ideas.md + post-v1-2-report.md — 5 deferred item re-evaluations + 7 new post-v1.2 ideas with verdicts; 13-item M003+ backlog candidates table
  - SUMMARY.md — session synthesis with S03/S05/S06 downstream inputs, deferred-items status, and new backlog candidates
requires: []
affects:
  - S03 (consumes count-quota-report.md downstream inputs DI-1 through DI-6)
  - S05 (consumes testing-vocabulary-report.md vocabulary candidates table + downstream inputs)
  - S06 (consumes future-features-report.md "S06 must specify" mandate lists)
key_files:
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md
key_decisions:
  - Tag non-exclusivity (DI-2) is the hardest count-quota open question — semantics are a public contract; cannot change post-ship without a breaking version bump
  - CountRequireUnmet must be a SelectionReport-level field, not a per-item ExclusionReason — there is no item to attach it to when a minimum count is unmet
  - Multi-budget DryRun API variant rejected — microsecond optimization at cost of permanent API coupling; FindMinBudgetFor calls single-budget DryRun ~10-15 times
  - BudgetUtilization and KindDiversity belong in Wollax.Cupel core (not Wollax.Cupel.Analytics) — avoids OTel-depends-on-Analytics dependency chain; supersedes T02 recommendation
  - Rust TimeProvider trait: minimal (Send+Sync, fn now() → DateTime<Utc>), Box<dyn> at construction, SystemTimeProvider ZST
  - Fork diagnostic (PolicySensitivityReport) is M003-ready — dry_run is now live in both languages
  - FindMinBudgetFor return type: int?/Option<i32>; null/None is a valid business result (budget not found within ceiling), not an exception
  - DryRun determinism invariant is a spec gap — must be stated explicitly in S06 budget simulation chapter
patterns_established:
  - Explorer/challenger debate format: ideas.md (uncensored, ≥15-25 proposals) → report.md (challenged, scoped, per-proposal verdict table + downstream inputs section)
  - Locked decisions applied by noting constraint and redirecting to what downstream slice must specify — not reopening the debate
  - "High-scoring" language rejected wherever it appears; replaced with explicit parameterized forms (PlaceTopNScoredAtEdges(n))
  - Ordering-dependent assertions documented with explicit Placer dependency caveat; "edge = position 0 or count-1" defined
  - Deferred-items re-evaluation format: "What changed since March 15" framing → verdict from {M003 ready / M003+ defer / Drop / Reassigned}
  - Placement revision in later pairs overrides earlier recommendations when architectural reasoning is stronger
observability_surfaces:
  - cat .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md — full session synthesis
  - grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md — 6 S03 downstream inputs
  - grep "Vocabulary candidates for S05" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md — 15-candidate table
  - grep "S06 must specify" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md — 18 mandate items
  - grep "New Backlog Candidates" -A 40 .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md — 13-item M003+ table
drill_down_paths:
  - .kata/milestones/M002/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M002/slices/S01/tasks/T02-SUMMARY.md
  - .kata/milestones/M002/slices/S01/tasks/T03-SUMMARY.md
  - .kata/milestones/M002/slices/S01/tasks/T04-SUMMARY.md
duration: 4 sessions (~3.5h total across T01-T04)
verification_result: passed
completed_at: 2026-03-21
---

# S01: Post-v1.2 Brainstorm Sprint

**Four explorer/challenger brainstorm pairs (9 files, 1,600+ lines) committed to `.planning/brainstorms/2026-03-21T09-00-brainstorm/`; 5 deferred items re-evaluated; 13 M003+ backlog candidates catalogued; 113 Rust + 583 .NET tests pass.**

## What Happened

T01 grounded in three source artifacts (March 15 highvalue-report Decision 6, `spec/src/slicers/quota.md` DISTRIBUTE-BUDGET pseudocode, `crates/cupel/src/slicer/quota.rs`) before writing. The count-quota explorer pass generated 28 raw proposals across six sections — one per Decision-6 open question plus a sixth post-v1.2 section on exclusion diagnostic variants. The challenger pass debated all 28 with challenge/response/verdict structure, applied D040 explicitly (Section 4, Idea 4B redirected), and produced 6 downstream inputs for S03 (DI-1 through DI-6). DI-2 (tag non-exclusivity) was flagged as the hardest: it is a public contract that cannot change post-ship without a breaking version bump.

T02 anchored against exact field semantics from `spec/src/diagnostics/selection-report.md` (Included = final placed order; Excluded = score-desc with stable insertion-order tiebreak) and Decision 4 from the March 15 highvalue-report. The explorer pass generated 25 named assertion patterns across 7 categories. The challenger applied precision requirements to every proposal — flagging and replacing all "high-scoring" language, documenting Placer dependencies for all ordering-dependent assertions, requiring denominator definitions and floating-point tolerance for all utilization assertions. Result: 15-candidate vocabulary table for S05, all with ready/needs-work status. Extension-method placement verdicts: BudgetUtilization and KindDiversity → core analytics; ExcludedItemCount → Cupel.Testing only.

T03 read `crates/cupel/src/scorer/recency.rs` (rank-based contrast for DecayScorer framing) and March 15 Decisions 3/4/5. Explorer pass: 22 proposals across three feature sections (DecayScorer, OTel verbosity, Budget Simulation) plus three cross-feature proposals. Key proposals: Rust TimeProvider trait candidates (minimal trait won), multi-budget DryRun optimization (rejected: microseconds vs permanent API coupling), OTel exclusions as Events not child Activities, FindMinBudgetFor return type (int?/Option<i32> settled). Challenger produced three "S06 must specify" lists (6+6+6 items) and identified DryRun determinism invariant as a spec gap.

T04 re-evaluated five deferred items with "What changed since March 15" framing: fork diagnostic advanced to M003-ready (dry_run now live in both languages was the sole prerequisite); BudgetUtilization/KindDiversity placement revised to Wollax.Cupel core (OTel dependency chain argument overrides T02's analytics-package recommendation); IntentSpec and ProfiledPlacer confirmed M003+ defer (no new signal); snapshot testing M003+ defer (requires tiebreak spec rule + GreedySlice default change first). Seven new post-v1.2 ideas generated and debated; 13-item "New backlog candidates" table produced. SUMMARY.md written following March 15 template with all five required sections. Both test suites verified clean; all 9 files committed.

## Verification

```
ls .planning/brainstorms/2026-03-21T09-00-brainstorm/ | wc -l
# → 9  ✅

grep -c "Downstream Inputs" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md
# → 1  ✅

grep -c "New Backlog Candidates" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md
# → 1  ✅

cargo test --manifest-path crates/cupel/Cargo.toml
# → 113 passed, 0 failed  ✅

dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
# → 583 passed, 0 failed  ✅

git log --oneline -3
# → feat(S01/T04): post-v1-2 ideas pair + SUMMARY.md; S01 complete
# → docs(S01): add brainstorm 2026-03-21T09-00
# → ...  ✅
```

## Requirements Advanced

- R045 — Primary owning requirement. Brainstorm session committed with all 9 files; 5 deferred items re-evaluated; fresh post-v1.2 ideas catalogued; SUMMARY.md provides downstream inputs for S03/S05/S06.

## Requirements Validated

- R045 — Validated. All success criteria met: brainstorm directory committed, SUMMARY.md contains all required sections, deferred items re-evaluated, new backlog candidates enumerated, test suites pass.

## New Requirements Surfaced

- **Fork diagnostic (PolicySensitivityReport)** — Confirmed M003-ready. Should be added as an active M003 requirement when M003 planning begins. (~80 lines of orchestration over dry_run.)
- **TimestampCoverage() extension method** — Fourth analytics method alongside BudgetUtilization, KindDiversity, ExcludedItemCount. Belongs in Wollax.Cupel core.
- **SelectionReport equality** — Deep equality for test assertions (ContextItem equality required first). M003+.
- **KnapsackSlice OOM error structure** — expose CupelError.TableTooLarge fields (candidates, capacity, cells) as structured properties, not just message parse.
- **MetadataKeyScorer** — Generic scorer that reads arbitrary metadata keys; companion to R042 MetadataTrustScorer.
- **DryRun determinism invariant** — Should be added as explicit normative text in the budget simulation spec chapter (S06 must specify).

## Requirements Invalidated or Re-scoped

- none

## Deviations

- **`rtk dotnet test` incompatibility:** `rtk dotnet test` exits 1 with "Zero tests ran" when run against the TUnit-based test project. Raw `dotnet test --project` exits 0 with 583 tests passing. Pre-existing environmental issue with the `rtk` wrapper; not a regression.
- **T01 produced 6 downstream inputs (DI-1 through DI-6) rather than the planned ≥5.** DI-6 (SelectionReport backward-compatibility precondition) was added as a prerequisite for DI-3.
- **T02/T04 placement revision:** T04 challenger overrides T02's recommendation to place BudgetUtilization and KindDiversity in `Wollax.Cupel.Analytics`. The OTel dependency chain argument (avoid OTel-depends-on-Analytics) warrants the revision. SUMMARY.md notes the override in S05 DI-5.
- **T03 produced cross-feature proposals** (X.1, X.2, X.3) as a separate section in ideas.md — capturing gaps cutting across all three features. Folded into report's feature sections rather than appearing as a separate section.

## Known Limitations

- All outputs are design inputs and recommendations, not locked decisions. S03, S05, and S06 may revise positions taken here when working through the specifics.
- Tag non-exclusivity (DI-2) remains unresolved — it is the hardest count-quota open question and is explicitly deferred to S03's explorer/challenger debate.
- Snapshot testing path identified (tiebreak spec rule + GreedySlice default) but requires spec changes outside M002 scope.

## Follow-ups

- **S03** must consume `count-quota-report.md` DI-1 through DI-6 as starting framing for the count-quota design record.
- **S05** must consume `testing-vocabulary-report.md` 15-candidate table as vocabulary candidates; DI-5 notes the revised placement for BudgetUtilization/KindDiversity.
- **S06** must consume `future-features-report.md` "S06 must specify" mandate lists for all three spec chapters; DI-1 provides the Rust TimeProvider trait shape.
- When M003 planning begins, fork diagnostic (PolicySensitivityReport) should be added as an active requirement — it is now M003-ready.

## Files Created/Modified

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md` — 238-line explorer pass; 28 raw proposals across 6 sections including post-v1.2 exclusion diagnostic angle
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — 376-line challenger report; DI-1 through DI-6 downstream inputs for S03; D040 applied
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md` — 247-line explorer pass; 25 named assertion patterns across 7 categories; extension-methods re-evaluation; Rust parity note
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — 412-line challenger report; 15-candidate vocabulary table; per-pattern precision analysis; extension-methods placement verdicts; D041 applied
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` — 462-line explorer pass; three feature sections with 7+8+7 proposals plus cross-feature section
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — 524-line challenger report; "S06 must specify" / "S06 should consider" / "implementation" classification; 3 downstream inputs; D042/D043 applied
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md` — explorer pass; 5 deferred re-evaluations + 7 new ideas
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md` — challenger report; deferred item verdicts; 13-item new backlog candidates table
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` — session synthesis; pairs table; S03/S05/S06 downstream inputs; deferred-items status; new backlog candidates; cross-cutting themes

## Forward Intelligence

### What the next slice should know

- **S03:** DI-2 (tag non-exclusivity) is the highest-risk open question. The count-quota report explicitly flags it as a public contract — run a full explorer/challenger debate focused specifically on this before settling the algorithm. The two-phase algorithm (count-satisfy first, then DISTRIBUTE-BUDGET on remainder) is the recommended base architecture (DI-1).
- **S05:** The 15 vocabulary candidates in `testing-vocabulary-report.md` include "needs-work" annotations for several patterns — specifically `PlaceTopNScoredAtEdges(n)` (tie-score handling) and `ExcludeItemWithBudgetDetails` (dual-form API). These need explicit resolution before S05 produces the final spec.
- **S06:** The DryRun determinism invariant (same inputs → same outputs) must appear as normative text in the budget simulation chapter. It was identified as a spec gap in T03 but belongs in S06's chapter, not a separate issue.
- **M003 planning:** Fork diagnostic (PolicySensitivityReport) is M003-ready; BudgetUtilization + KindDiversity + TimestampCoverage belong in Wollax.Cupel core.

### What's fragile

- **`rtk dotnet test` with TUnit** — the `rtk` wrapper transforms arguments in a way that confuses TUnit's CLI runner, reporting "Zero tests ran" even when all tests pass. Always use raw `dotnet test --project` for the TUnit project.
- **BudgetUtilization denominator** — the report recommends `budget.MaxTokens` as the denominator (not `TargetTokens`). If S05 uses a different denominator, the assertion semantics will be inconsistent with what T02 analyzed. Flag this in S05's task plan.

### Authoritative diagnostics

- `grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — authoritative S03 starting framing (6 design inputs)
- `grep "Vocabulary candidates for S05" -A 50 .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — authoritative S05 candidate table
- `grep "S06 must specify" -A 10 .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — authoritative S06 mandate lists

### What assumptions changed

- **T02 analytics placement** — Originally assumed BudgetUtilization/KindDiversity belonged in a separate Wollax.Cupel.Analytics package. T04 revised this: they belong in Wollax.Cupel core to avoid OTel → Analytics dependency chain.
- **DryRun multi-budget** — T03 considered a multi-budget DryRun optimization as plausible in the explorer pass; the challenger rejected it decisively. S06 should not re-examine this unless new evidence emerges.
