# S05: Cupel.Testing Vocabulary Design — Research

**Date:** 2026-03-21
**Requirement:** R043 — Cupel.Testing vocabulary design

## Summary

S05 produces `spec/src/testing/vocabulary.md` — a language-agnostic vocabulary document defining ≥10 named assertion patterns over `SelectionReport`. The brainstorm (S01) did the exploration work: `testing-vocabulary-report.md` delivers a 15-candidate table with per-pattern precision analysis, distinguishing "Ready to spec" (10 patterns) from "Needs precision work" (5 patterns). The vocabulary chapter must resolve each "needs-work" gap — primarily the `BudgetUtilization` denominator, `PlaceTopNScoredAtEdges` tie-score handling, and `ExcludeItemWithBudgetDetails` dual-form API — then write the chapter.

There is one non-obvious constraint: the `.NET` `ExclusionReason` is a **flat enum** (no associated data), while the Rust `ExclusionReason` is a **data-carrying enum** with struct variants. Any pattern that asserts associated fields (e.g. `item_tokens`, `available_tokens` from `BudgetExceeded`) is only fully expressible in Rust at the type level. The vocabulary document is language-agnostic, but it must acknowledge this asymmetry where it matters.

The chapter's primary deliverables are: precise natural-language specs per pattern, a chain-plumbing entry point definition, the predicate type decision (`IncludedItem`/`ExcludedItem` not raw `ContextItem`), the `BudgetUtilization` denominator selection, and the Placer-dependency caveat applied consistently to all ordering-sensitive assertions.

## Recommendation

Write the vocabulary chapter with these choices locked in:
1. **Predicate type**: All predicate-bearing methods accept `IncludedItem` / `ExcludedItem` (not raw `ContextItem`), enabling access to `Score` and `Reason`. Convenience overloads over `ContextItem` fields (Kind, Content) can be offered alongside.
2. **`BudgetUtilization` denominator**: `budget.MaxTokens` (the hard ceiling). This is the consistent recommendation from the S01 challenger report (DI-3) and matches D045 placement in core.
3. **Threshold comparisons**: Use `>=` for lower-bound thresholds (HaveTokenUtilizationAbove), `<=` for upper-bound (Below), without epsilon. The spec should state the comparison operator explicitly. Edge-case floating-point values are a test authoring responsibility.
4. **`PlaceTopNScoredAtEdges(n)`**: Enumerate edge positions as (0, count-1, 1, count-2, ...). Tie-score handling: if multiple items share the score at position N, the assertion passes if the actual items at the N edge positions are any N items with that score — not a specific one.
5. **`ExcludeItemWithBudgetDetails`**: Language-agnostic spec writes it against the Rust-style data-carrying variant. Include a language note: "In .NET, `ExclusionReason` is a flat enum; `item_tokens` and `available_tokens` are not available on `ExcludedItem`. Implementations may omit this assertion or surface it differently."
6. **Pattern count**: Target 13 well-specified patterns (the 10 from S05-CONTEXT.md scope + 3 well-specified additions from the 15-candidate table). No padding with vague patterns.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| SelectionReport field semantics | `spec/src/diagnostics/selection-report.md` | Authoritative source for Included order, Excluded sort, total_candidates derivation rule |
| ExclusionReason variant names + fields | `spec/src/diagnostics/exclusion-reasons.md` | Exact variant names and associated fields used in assertion specs |
| Naming conventions for assertion methods | S01 `testing-vocabulary-report.md` (15-candidate table) | Per-pattern precision analysis already done; start from these, don't re-derive |
| "Edge" definition | O03 analysis in testing-vocabulary-report.md | "Edge" = position 0 or position count-1 — already resolved |
| Denominator for BudgetUtilization | DI-3 in testing-vocabulary-report.md + D045 | MaxTokens is the canonical denominator |

## Existing Code and Patterns

- `spec/src/diagnostics/selection-report.md` — `Included` is final placed order; `Excluded` is score-descending with stable insertion-order tiebreak. `total_candidates = len(included) + len(excluded)`. These are the invariants all assertion specs must be written against.
- `spec/src/diagnostics/exclusion-reasons.md` — `ExclusionReason` variant table: `BudgetExceeded` (item_tokens, available_tokens), `Deduplicated` (deduplicated_against), `NegativeTokens` (tokens), `PinnedOverride` (displaced_by), `ScoredTooLow` / `QuotaCapExceeded` / `QuotaRequireDisplaced` / `Filtered` (reserved). Reserved variants must be accounted for — `ExcludeItemWithReason` must work against reserved variants too.
- `spec/src/scorers/metadata-trust.md` — follow its chapter style as a template: overview, algorithm pseudocode in labeled `text` fenced blocks, conformance notes section. The vocabulary chapter should follow the same sectioning style.
- `crates/cupel/src/diagnostics/mod.rs` — Rust `ExclusionReason` is a data-carrying enum with struct variants. `ExcludedItem` carries `item: ContextItem`, `score: f64`, `reason: ExclusionReason`.
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — .NET `ExclusionReason` is a **flat enum** (no associated data). `ExcludedItem` has `Item`, `Score`, `Reason`, and `DeduplicatedAgainst: ContextItem?` (only populated for `Deduplicated` reason). No `item_tokens` or `available_tokens` fields on the .NET type.
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `.NET` `SelectionReport` is a `sealed record` with `Events`, `Included`, `Excluded`, `TotalCandidates`, `TotalTokensConsidered`. Adding new properties is safe for property-access callers but breaks positional deconstruction — documented as explicitly unsupported (D057).
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — Authoritative 15-candidate table with per-pattern precision analysis. The "Vocabulary candidates for S05" section + "Downstream Inputs for S05" sections are the primary planning artifact.

## Constraints

- **No implementation code** — D039 is locked. Vocabulary is spec-only.
- **No FluentAssertions dependency, no snapshot assertions** — D041 is locked.
- **No "high-scoring" language** — Any pattern identifying top items must take an explicit `topN: int` parameter. S05-CONTEXT.md bans vague qualifiers entirely.
- **Error message format constraint** — On failure, show only the relevant field, not the full report. Must include: assertion name, what was expected, what was actually found. (S05-CONTEXT.md, Constraints section)
- **Edge definition is locked** — `PlaceItemAtEdge`: "at edge" = position 0 or position `(included.Count - 1)`. (S05-CONTEXT.md, Precision requirements)
- **Language-agnostic vocabulary** — The chapter describes semantics, not .NET or Rust implementation. Language-specific notes go in callout boxes, not the main spec prose.
- **No BudgetUtilization denominator ambiguity** — Must choose and document one denominator (MaxTokens recommended). The working assumption in S05-CONTEXT.md says `budget.targetTokens`; the S01 challenger report (DI-3) and D045 say `MaxTokens`. S05 must resolve this — recommendation is MaxTokens.
- **`spec/src/testing/` directory does not exist yet** — must create it when writing the file.
- **Chapter must be linked from `spec/src/SUMMARY.md`** — a new "Testing" top-level section with `[Cupel.Testing Vocabulary](testing/vocabulary.md)`.

## Common Pitfalls

- **Predicate over `ContextItem` vs `IncludedItem`** — Using `ContextItem` as the predicate type loses access to `Score` and `Reason`. Every predicate-bearing method must accept `IncludedItem` or `ExcludedItem`. The S01 challenger explicitly flags this (DI-2 and P03 analysis).
- **`.NET ExclusionReason` has no associated data** — `ExcludeItemWithBudgetDetails` (A03 in challenger report) works cleanly in Rust (data-carrying enum) but not in .NET (flat enum with no `item_tokens`/`available_tokens`). The vocabulary must note the language asymmetry rather than silently speccing against only one implementation.
- **"NotIncludeKind" vs "HaveNoExclusionsForKind" naming** — S05-CONTEXT.md uses `HaveNoExclusionsForKind` (no items of kind appear in `Excluded`), which is semantically different from the challenger report's `NotIncludeKind` (K04: no items of kind appear in `Included`). Both patterns are needed; ensure naming and semantics are distinct.
- **`PlaceTopNScoredAtEdges` tie-score edge case** — Ties at the Nth position must be handled explicitly: any item with the tied score is a valid occupant of that edge position. The S01 forward intelligence calls this out as a "needs-work" item.
- **`ExcludedItemsAreSortedByScoreDescending` tiebreak** — The insertion-order tiebreak for equal scores is guaranteed by implementation (D019) but is not assertable from the report alone. The assertion should only verify the score-descending property; the tiebreak caveat must be in the spec.
- **`HaveTokenUtilizationAbove` denominator** — `TotalTokensConsidered` is the sum of all candidate tokens (included + excluded), not just included. It is NOT a utilization metric — it is a candidate-set metric. `BudgetUtilization` should be `sum(included[i].tokens) / budget.MaxTokens`. Do not conflate these.
- **N=0 for `IncludeExactlyNItemsWithKind`** — N=0 is semantically equivalent to `HaveNoKindInIncluded`. The spec must note whether `IncludeExactlyNItemsWithKind(kind, 0)` is a valid spelling or whether a dedicated negation pattern is canonical.

## Open Risks

- **BudgetUtilization denominator conflict between S05-CONTEXT.md and S01**: S05-CONTEXT.md working assumption says `budget.targetTokens`; S01 challenger (DI-3) and D045 say `budget.MaxTokens`. The planning task should resolve this at the start. If the choice is unclear, note it in the task plan and default to `MaxTokens` with a clear rationale (it is the hard ceiling; `TargetTokens` is a soft Slice-stage target not intended as a public capacity metric).
- **`.NET ExclusionReason` flat enum is a permanent asymmetry**: The Rust `ExclusionReason` is richly data-carrying; the .NET version is not. If `ExcludeItemWithBudgetDetails` is included in the vocabulary, it can only be fully implemented in Rust as specified. The chapter must acknowledge this or narrow the assertion to only test fields available in both languages.
- **Placer dependency documentation**: Ordering-sensitive assertions (`PlaceItemAtEdge`, `PlaceTopNScoredAtEdges`, `PlaceItemsBefore`) depend on the Placer's contract. The chapter must consistently apply the caveat: "only meaningful when the Placer's ordering contract is known."
- **`spec/src/SUMMARY.md` needs a new "Testing" section** — no existing structure for this. The section header and its placement in the SUMMARY.md file must be decided (recommend: after Diagnostics, before Conformance).

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| mdBook spec authoring | — | none found (standard markdown; no specialist skill needed) |

## Sources

- 15-candidate vocabulary table with per-pattern analysis (source: `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md`, "Vocabulary candidates for S05" section)
- S05 downstream inputs DI-1 through DI-5 (source: same file, "Downstream Inputs for S05" section)
- SelectionReport field semantics and invariants (source: `spec/src/diagnostics/selection-report.md`)
- ExclusionReason variant table and associated fields (source: `spec/src/diagnostics/exclusion-reasons.md`)
- `.NET ExclusionReason` flat enum (source: `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs`)
- Rust data-carrying `ExclusionReason` (source: `crates/cupel/src/diagnostics/mod.rs`)
- D039 (no implementation), D041 (no FA / no snapshots), D045 (BudgetUtilization in core), D053/D057 (SelectionReport sealed record) (source: `.kata/DECISIONS.md`)
- S05 precision requirements and scope (source: `.kata/milestones/M002/slices/S05/S05-CONTEXT.md`)
