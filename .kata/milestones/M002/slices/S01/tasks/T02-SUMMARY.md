---
id: T02
parent: S01
milestone: M002
provides:
  - testing-vocabulary-ideas.md — 247-line explorer-mode brainstorm of 25 named assertion patterns organized across 7 categories (item-presence, item-absence, kind-coverage, budget/utilization, placement/ordering, count-based, diversity) plus SelectionReport extension-methods re-evaluation section and cross-language parity note
  - testing-vocabulary-report.md — 412-line challenger report with per-pattern precision analysis, D041 compliance check, extension-methods placement verdicts, and a 15-candidate "Vocabulary candidates for S05" table with ready/needs-work status per candidate
key_files:
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md
  - .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
key_decisions:
  - BudgetUtilization(budget) and KindDiversity() belong in core analytics (Wollax.Cupel or Wollax.Cupel.Analytics), not testing-only — both have production utility (runtime logging, adaptive budget logic)
  - ExcludedItemCount(predicate) belongs in Cupel.Testing only — production callers use report.Excluded.Count(pred) directly; no value in wrapping it
  - PlaceHighestScoredAtEdges rejected as underspecified — replace with PlaceTopNScoredAtEdges(int n) with explicit N and edge-position mapping
  - Predicate-based assertion methods should accept IncludedItem/ExcludedItem (not raw ContextItem) so callers can filter on score and reason, not just item fields
  - Budget utilization denominator recommendation: budget.MaxTokens (the hard ceiling), not TargetTokens (internal to Slice stage)
  - ExcludedItemsAreSortedByScoreDescending() asserts score-desc only; insertion-order tiebreak is not assertable from the report alone
patterns_established:
  - Explorer/challenger debate format applied: ideas.md (uncensored, 25 patterns) → report.md (precision-challenged, per-pattern verdict table)
  - "High-scoring" language flagged and rejected wherever it appeared (O05); replaced with explicit parameterized form (PlaceTopNScoredAtEdges)
  - Ordering-dependent assertions (O01–O06) documented with explicit Placer dependency; "edge = position 0 or count-1" defined precisely
  - Floating-point tolerance requirement surfaced for all utilization assertions (B01–B03); default tolerance 1e-9 recommended
  - D041 applied without re-debate — no FluentAssertions, no snapshots; all proposals comply
observability_surfaces:
  - Inspection: cat .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
  - Vocabulary table: grep "Vocabulary candidates for S05" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
  - Extension-methods verdict: grep "Placement Verdict" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
  - Downstream inputs for S05: grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
duration: single session
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T02: Testing vocabulary angles pair

**Explorer/challenger debate for Cupel.Testing assertion vocabulary committed to `.planning/brainstorms/2026-03-21T09-00-brainstorm/`; 15-candidate vocabulary table produced for S05 with ready/needs-work status per pattern.**

## What Happened

Read `spec/src/diagnostics/selection-report.md` (authoritative field semantics), `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` (Decision 2 and Decision 4 baseline), and `.kata/DECISIONS.md` (D041 locked). Key semantics anchored throughout:
- `Included` = final placed order (Placer-determined, not score order)
- `Excluded` = score-descending, stable insertion-order tiebreak
- `TotalTokensConsidered` = sum of tokens across ALL items (included + excluded)

**Explorer pass (`testing-vocabulary-ideas.md`):** Generated 25 named assertion patterns across 7 categories. Included a dedicated section re-evaluating the three `SelectionReport` extension methods from Decision 4 with arguments for each placement option (testing-only, core analytics, M003 deferred). Added a cross-language parity note covering Rust adaptation requirements (predicate types, chain idiom, ContextKind variant matching — all surface-level only, no semantic changes).

**Challenger pass (`testing-vocabulary-report.md`):** Applied precision requirements to every proposal. Key challenges surfaced:
- All "high-scoring" language flagged — `PlaceHighestScoredAtEdges` rejected and replaced with `PlaceTopNScoredAtEdges(n)` with explicit N
- All ordering-dependent assertions (O01–O06) documented with explicit Placer dependency caveat
- Utilization assertions (B01–B03) require denominator definition and floating-point tolerance
- `ExcludeItemWithBudgetDetails` needs dual-form API (exact vs. range tolerance)
- `A07 ExcludedItemsAreSortedByScoreDescending` — insertion-order tiebreak is not assertable from the report alone; score-desc only

D041 applied: no snapshot assertions, no FluentAssertions. All proposals comply — no items were blocked.

**Extension-methods verdict:**
- `BudgetUtilization(budget)` → core analytics (production utility: logging, adaptive logic)
- `KindDiversity()` → core analytics (same rationale)
- `ExcludedItemCount(predicate)` → Cupel.Testing only (production callers use LINQ directly)

## Verification

```
ls .planning/brainstorms/2026-03-21T09-00-brainstorm/
# → count-quota-ideas.md, count-quota-report.md, testing-vocabulary-ideas.md, testing-vocabulary-report.md

wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md
# → 247 (≥80 ✅)

wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
# → 412 (≥100 ✅)

grep -c "Vocabulary candidates for S05" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md
# → 1 (≥1 ✅)
```

Must-have checklist:
- [x] `ideas.md` proposes ≥15 named assertion patterns organized by category (25 proposed)
- [x] `ideas.md` includes section re-evaluating BudgetUtilization, KindDiversity, ExcludedItemCount
- [x] `report.md` challenges every "high-scoring" and ordering-dependent term with a precision requirement
- [x] `report.md` applies D041 explicitly (no snapshots, no FA) without re-debating those decisions
- [x] `report.md` contains "Vocabulary candidates for S05" section with 15 strong candidates
- [x] `report.md` produces a verdict on where BudgetUtilization, KindDiversity, ExcludedItemCount belong

## Diagnostics

- `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — full challenger report
- `grep "^### DI-" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — 5 downstream inputs for S05
- `grep "Verdict" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — per-pattern verdicts
- `grep "Needs work\|Ready" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — pattern readiness

## Deviations

None. Task plan followed exactly. No commit in this task — T04 commits all brainstorm files per slice plan.

## Known Issues

None. `PlaceTopNScoredAtEdges` tie-score handling is flagged as "needs work" for S05 but is not a blocker for this task.

## Files Created/Modified

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md` — 247-line explorer pass with 25 named assertion patterns, extension-methods re-evaluation, and Rust cross-language parity note
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — 412-line challenger report with per-pattern precision analysis, extension-methods placement verdicts, and 15-candidate vocabulary table for S05
