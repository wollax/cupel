---
estimated_steps: 4
estimated_files: 2
---

# T02: Testing vocabulary angles pair

**Slice:** S01 — Post-v1.2 Brainstorm Sprint
**Milestone:** M002

## Description

Run the second explorer/challenger pair targeting Cupel.Testing assertion vocabulary candidates. S05 must define ≥10 named assertion patterns with precise specs (what each asserts, tolerance/edge cases, error message format). Without prior debate specifically about vocabulary precision, S05 risks designing patterns against intuition rather than against the exact `SelectionReport` semantics — which differ in subtle but important ways from what callers expect.

The critical field semantics to hold throughout this task:
- `SelectionReport.Excluded` is sorted **score-descending, insertion-order tiebreak** — ordering assertions against `Excluded` must account for this
- `SelectionReport.Included` is in **final placed order** — position-dependent assertions (e.g. `PlaceItemAtEdge`) must specify which edge relative to the placer's behavior
- `SelectionReport.TotalTokensConsidered` counts tokens of all items passed to the pipeline, not just selected items

The explorer generates vocabulary candidates broadly; the challenger enforces precision requirements and surfaces which patterns will require the most spec work in S05.

**Re-evaluation in scope:** The `SelectionReport` extension methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`) from March 15 Decision 4 were reshaped but not assigned to any M002 slice. This task should produce a verdict: do they belong in the Cupel.Testing vocabulary (S05), as standalone analytics methods, or in M003?

**Locked decisions (treat as given):**
- D041: No FluentAssertions dependency; no snapshot assertions. Do not re-debate either.

## Steps

1. Read the following sources before writing:
   - `spec/src/diagnostics/selection-report.md` — exact field semantics for `SelectionReport`, `IncludedItem`, `ExcludedItem`. Field names and ordering semantics are precise; assertions must be specified against these, not against intuition.
   - `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decision 2 (Cupel.Testing vocabulary baseline with example assertion patterns), Decision 4 (SelectionReport extension methods — `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`)
   - `.kata/DECISIONS.md` — D041 locked: no FA, no snapshots

2. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md` in **explorer mode**:
   - Do not self-censor. Generate ≥15 named assertion patterns.
   - Organize by assertion category: item-presence (what's in `Included`), item-absence (what's in `Excluded` with what reason), kind coverage (which `ContextKind` values appear), budget/utilization metrics, placement/ordering, count-based (min/max exclusions), diversity.
   - For each: give a name and one-sentence description of what it asserts.
   - Also include a section: "SelectionReport extension methods — re-evaluation." Should `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` be in the testing vocabulary, in core analytics, or in M003? Generate arguments for each placement.
   - Also propose at least one cross-language assertion parity note: if the vocabulary is designed for C# `SelectionReport`, what changes (if anything) for the Rust equivalent?

3. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` in **challenger mode**:
   - For each proposed pattern: challenge it on precision. Specific questions to apply:
     - Does this assertion depend on `Excluded` or `Included` ordering? If so, is the ordering guarantee sufficient?
     - Does "high-scoring" or "low-scoring" appear anywhere? If so, flag as underspecified — require a concrete definition (top N by score? above a threshold?).
     - Does the pattern have a tolerance need (e.g., floating-point utilization)? If so, specify the tolerance.
     - What is the exact error message format when it fails? (e.g., `"Expected item with Kind=Message in Included but found 0 items of that kind. Included had N items."`)
   - Apply D041: any snapshot-assertion proposal is out of scope with note "blocked until ordering stability for ties is guaranteed (D041)."
   - Extension-methods re-evaluation: produce a clear verdict for each of the three methods with rationale.
   - Close with "Vocabulary candidates for S05" section: list the ≥10 strongest candidates with per-candidate notes on what S05 must specify precisely. Flag patterns that are "ready to spec" vs "needs precision work."

4. Do not commit yet — that happens in T04.

## Must-Haves

- [ ] `ideas.md` proposes ≥15 named assertion patterns organized by category
- [ ] `ideas.md` includes a section re-evaluating the three `SelectionReport` extension methods from Decision 4
- [ ] `report.md` challenges every "high-scoring" or ordering-dependent term with a precision requirement
- [ ] `report.md` applies D041 explicitly (no snapshots, no FA) without re-debating those decisions
- [ ] `report.md` contains a "Vocabulary candidates for S05" section with ≥10 strong candidates
- [ ] `report.md` produces a verdict on where `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` belong

## Verification

- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` shows `testing-vocabulary-ideas.md` and `testing-vocabulary-report.md`
- `wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md` ≥ 80
- `wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` ≥ 100
- `grep -c "Vocabulary candidates for S05" .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` ≥ 1

## Observability Impact

- Signals added/changed: None (documentation only)
- How a future agent inspects this: `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md`; grep for "Vocabulary candidates for S05" to find the summary section
- Failure state exposed: If `report.md` lacks the vocabulary candidates section or contains un-challenged "high-scoring" language, S05 will produce an ambiguous vocabulary that requires API-breaking revisions

## Inputs

- `spec/src/diagnostics/selection-report.md` — authoritative field semantics for `SelectionReport`
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decisions 2 and 4 for vocabulary baseline and extension-methods context
- `.kata/DECISIONS.md` — D041 locked
- `S01-RESEARCH.md` — "Open Risks" section item 2 (ordering-dependent assertions) and "Deferred Ideas to Re-evaluate" item 2 (extension methods)

## Expected Output

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md` — ≥15 raw assertion pattern proposals with extension-methods re-evaluation section
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — precision-challenged report with ≥10 vocabulary candidates for S05, per-candidate precision notes, and extension-methods placement verdict
