---
id: T03
parent: S01
milestone: M002
provides:
  - future-features-ideas.md — 462-line explorer-mode brainstorm across three S06 spec areas (DecayScorer, OTel verbosity, Budget Simulation) with 7+8+7 raw proposals covering Rust TimeProvider trait design, Window/falloff debate, negative age handling, timestamp coverage metrics, zero half-life edge case, exact OTel attribute names and cardinality table, and multi-budget DryRun evaluation
  - future-features-report.md — 524-line challenger report with per-proposal verdicts, D042/D043 applied as locked, three "S06 must specify" sections (6+6+6 items) plus "S06 should consider" subsections and an explicit verdict summary table
key_files:
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
key_decisions:
  - Rust TimeProvider trait shape resolved — minimal trait (Send + Sync, fn now() -> DateTime<Utc>) with SystemTimeProvider ZST; Box<dyn TimeProvider + Send + Sync> at DecayScorer construction; D042 mandatory injection locked
  - Multi-budget DryRun rejected — optimization saves microseconds at the cost of permanent API coupling; FindMinBudgetFor calls single-budget DryRun ~10-15 times
  - FindMinBudgetFor return type decided — int? / Option<i32>; null/None is a valid business result (not found within searchCeiling), not an exception
  - TimestampCoverage() confirmed as fourth analytics extension method alongside BudgetUtilization, KindDiversity, ExcludedItemCount
  - OTel activity hierarchy resolved — root "cupel.pipeline" + 5 child "cupel.stage.{name}" Activities; no duration_ms attribute (redundant with Activity built-in timing); exclusions as Events not child Activities
  - DryRun determinism invariant identified as a spec gap — must be stated explicitly in S06 budget simulation chapter
patterns_established:
  - Explorer/challenger debate format continued: ideas.md (uncensored, multi-section) → report.md (challenged, "S06 must specify" / "S06 should consider" / "implementation (S06 decides)" classification per feature)
  - Locked decisions (D042, D043) applied by noting the constraint and redirecting to what S06 must specify within that constraint — identical pattern to T01's D040 handling
  - New spec gap identification pattern: cross-feature X. proposals in ideas.md capture gaps discovered during exploration that cut across feature boundaries (DryRun determinism, TimestampCoverage as fourth analytics method)
observability_surfaces:
  - Inspection: cat .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
  - Inspect S06 mandate lists: grep "S06 must specify" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
  - Inspect downstream inputs: grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
  - Inspect verdict table: grep -A1 "Verdict Summary Table" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
duration: ~45min
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Future features angles pair

**Explorer/challenger debate for three S06 spec chapters committed to `.planning/brainstorms/2026-03-21T09-00-brainstorm/`; 18 S06 mandate items produced across DecayScorer, OTel, and Budget Simulation spec chapters.**

## What Happened

Read `crates/cupel/src/scorer/recency.rs` to ground the DecayScorer framing (rank-based
vs. time-magnitude contrast), the March 15 highvalue-report (Decisions 3, 4, 5 baseline),
and `.kata/DECISIONS.md` (D042, D043 locked).

**Explorer pass (`future-features-ideas.md`):** Three labeled sections with 7+ proposals
each. DecayScorer section covered four Rust TimeProvider candidates (minimal trait,
Arc<dyn>, generic T, function pointer), Window binary vs linear falloff, negative age
handling (4 options), timestamp coverage as runtime warning vs. extension method, zero
half-life construction behavior, and nullTimestampScore placement. OTel section produced
exact candidate attribute names for all three verbosity tiers, challenged duration_ms
redundancy, resolved Events vs. child Activities for exclusions, and produced a cardinality
table. Budget simulation section analyzed the multi-budget DryRun optimization case in
depth (shared stage analysis), resolved GetMarginalItems diff direction (Primary \ Margin),
worked through FindMinBudgetFor lower bound with pinned-item caveat, and challenged all
four return-type options for the not-found case.

**Challenger pass (`future-features-report.md`):** Per-proposal verdict for all proposals.
D042 and D043 applied as locked — no re-debate, redirection to what S06 specifies within
those constraints. Multi-budget DryRun rejected (microsecond savings, permanent API
coupling cost). `duration_ms` attribute rejected (OTEL best practice: use Activity
built-in timing). Exclusions as Events confirmed (Activities are for work units with
measurable duration; exclusions are state snapshots). `FindMinBudgetFor` return type
settled on `int?` / `Option<i32>`. Three downstream inputs (DI-1 through DI-3) produced
for S06 planning.

## Verification

```
ls .planning/brainstorms/2026-03-21T09-00-brainstorm/
# → shows future-features-ideas.md and future-features-report.md ✓

wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md
# → 462 (≥ 80 required) ✓

wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
# → 524 (≥ 100 required) ✓

grep -c "S06 must specify" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md
# → 3 (≥ 3 required) ✓
```

All must-haves confirmed:
- [x] `ideas.md` has three labeled sections with ≥5 proposals each (7, 8, 7)
- [x] `ideas.md` covers Rust TimeProvider trait design as a distinct angle (Proposal 1.1)
- [x] `ideas.md` addresses the dry_run multi-budget question explicitly (Proposal 3.1)
- [x] `report.md` applies D042 and D043 as locked without re-debating them
- [x] `report.md` has "S06 must specify" and "S06 should consider" subsections for each of the three features
- [x] Neither file conflates spec concerns with implementation details (implementation items labeled "Implementation (S06 decides)")

## Diagnostics

- `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — full challenger report with verdicts
- `grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — 3 downstream inputs for S06
- `grep "S06 must specify" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — the three mandate lists
- `grep "Verdict:" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — all per-proposal verdicts

## Deviations

One small deviation from the task plan: the plan specified ≥5 proposals per section but
the ideas file naturally produced 7 (DecayScorer), 8 (OTel), and 7 (Budget Simulation).
Cross-feature proposals (X.1, X.2, X.3) were added as a separate section in the ideas
file capturing gaps that cut across all three features. These were folded into the report's
feature sections rather than appearing as a separate cross-feature section.

## Known Issues

None. T04 handles the commit.

## Files Created/Modified

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` — 462-line explorer document with three labeled sections covering Rust TimeProvider, OTel attribute names, and budget simulation edge cases
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — 524-line challenger report with "S06 must specify" / "S06 should consider" / "implementation (S06 decides)" classification, 3 downstream inputs, and 19-row verdict summary table
