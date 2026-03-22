---
id: S05
parent: M002
milestone: M002
provides:
  - spec/src/testing/vocabulary.md — 13 fully-specified named assertion patterns over SelectionReport
  - spec/src/SUMMARY.md updated with # Testing section containing vocabulary.md link
requires:
  - slice: S01
    provides: testing-vocabulary-report.md from brainstorm with 15-candidate vocabulary table and DI-1 through DI-5 precision analysis
affects:
  - none (M003 Cupel.Testing implementation consumes this vocabulary document)
key_files:
  - spec/src/testing/vocabulary.md
  - spec/src/SUMMARY.md
key_decisions:
  - PD-1: predicate type is IncludedItem/ExcludedItem, not raw ContextItem
  - PD-2: BudgetUtilization denominator is budget.MaxTokens (per D045/DI-3), not TargetTokens
  - PD-3: floating-point threshold comparisons use exact >= / <= operators with no epsilon (per D064)
  - PD-4: chain entry point is SelectionReport.Should() returning SelectionReportAssertionChain; failure via SelectionReportAssertionException
  - ExcludeItemWithBudgetDetails: .NET flat enum asymmetry explicitly noted — assertion may be omitted or adapted in .NET implementations
  - ExcludedItemsAreSortedByScoreDescending: score-descending only; insertion-order tiebreak (D019) is real but not observable from report alone
  - PlaceTopNScoredAtEdges: tie-score handling — any item with the tied score is valid at the N-th edge position (not a specific one)
  - HaveBudgetUtilizationAbove: MaxTokens denominator rationale documented (hard ceiling; TargetTokens is Slice-stage-internal)
  - PlaceItemAtEdge: edge = position 0 OR position count−1 exactly, not same-score adjacency
patterns_established:
  - "IncludeItemWithKind: existence check over Included; ContextKind.Any is not a valid sentinel"
  - "IncludeItemMatching: predicate over IncludedItem (full item+score pair per PD-1); optional ContextItem convenience overloads are implementation-defined"
  - "IncludeExactlyNItemsWithKind: N=0 is valid (asserts no items of kind in Included); semantically distinct from HaveNoExclusionsForKind"
  - "ExcludeItemWithReason: variant discriminant match, not string equality; all 8 ExclusionReason variants (including reserved) are valid arguments"
  - "ExcludeItemMatchingWithReason: predicate over ContextItem; error message distinguishes 0-predicate-match vs N-predicate-match/wrong-reason via predicateMatchCount"
  - "ExcludeItemWithBudgetDetails: exact integer equality on item_tokens and available_tokens; .NET flat enum language-asymmetry note"
  - "HaveNoExclusionsForKind: All predicate over Excluded; vacuous pass on empty Excluded; distinct from IncludeExactlyNItemsWithKind(kind,0)"
  - "HaveAtLeastNExclusions: N=0 is a valid no-op (always passes); no separate HaveNoExclusionsRequired alias"
  - "ExcludedItemsAreSortedByScoreDescending: conformance assertion; insertion-order tiebreak not assertable from report per D019"
  - "HaveBudgetUtilizationAbove: exact >= no epsilon; empty-Included edge case produces utilization = 0.0"
  - "HaveKindCoverageCount: Distinct over Included kinds; N=1 is vacuously true for any non-empty Included"
  - "PlaceItemAtEdge: edge = index 0 or index count-1 exactly; error message shows actual index if item in Included but not at edge"
  - "PlaceTopNScoredAtEdges: edge-position mapping 0, count-1, 1, count-2, ... alternating inward; tie-score: any tied item valid at that position"
observability_surfaces:
  - none (spec-only changes per D039)
drill_down_paths:
  - .kata/milestones/M002/slices/S05/tasks/T01-SUMMARY.md
  - .kata/milestones/M002/slices/S05/tasks/T02-SUMMARY.md
  - .kata/milestones/M002/slices/S05/tasks/T03-SUMMARY.md
duration: ~110 min (T01: 20m, T02: ~30m, T03: ~30m)
verification_result: passed
completed_at: 2026-03-21
---

# S05: Cupel.Testing Vocabulary Design

**`spec/src/testing/vocabulary.md` exists with 13 fully-specified named assertion patterns over `SelectionReport`; no TBD fields; `spec/src/SUMMARY.md` updated with Testing section; both test suites green.**

## What Happened

Three tasks produced `spec/src/testing/vocabulary.md` incrementally, working from locked pre-decisions through pattern groups to a completed document with a notes section and precision audit.

**T01 (skeleton + pre-decisions):** Created `spec/src/testing/` directory and wrote the foundational sections of `vocabulary.md`: Overview (what Cupel.Testing is and is not), Pre-decisions (4 locked design choices PD-1 through PD-4 governing all 13 patterns), Chain Plumbing (full `SelectionReportAssertionChain` type shape with all 13 method signatures and failure contract), and a Vocabulary section with a 13-row summary table. Updated `spec/src/SUMMARY.md` to insert a `# Testing` section (after `# Conformance`, before `# Appendix`) linking to `testing/vocabulary.md`.

**T02 (patterns 1–7: Inclusion and Exclusion groups):** Consulted `spec/src/diagnostics/selection-report.md` and `spec/src/diagnostics/exclusion-reasons.md` for exact field shapes, and confirmed the .NET/Rust language asymmetry on `ExclusionReason` from source files. Wrote 7 fully-specified patterns: `IncludeItemWithKind` (existence check with `ContextKind.Any` invalid-sentinel note), `IncludeItemMatching` (predicate over `IncludedItem`), `IncludeExactlyNItemsWithKind` (exact count; N=0 semantics explicit), `ExcludeItemWithReason` (variant discriminant match), `ExcludeItemMatchingWithReason` (predicate+reason with partial-match disambiguation), `ExcludeItemWithBudgetDetails` (exact integer equality on associated data; .NET flat-enum language note), and `HaveNoExclusionsForKind` (vacuous-pass on empty Excluded; semantic distinction from N=0 inclusion check). Each pattern has assertion semantics, predicate type, edge cases, and error message format.

**T03 (patterns 8–13 + audit):** Appended 6 remaining patterns: `HaveAtLeastNExclusions` (N=0 valid), `ExcludedItemsAreSortedByScoreDescending` (conformance assertion; D019 tiebreak caveat), `HaveBudgetUtilizationAbove` (MaxTokens denominator rationale; exact `>=`; empty-Included edge), `HaveKindCoverageCount` (distinct-kind set), `PlaceItemAtEdge` (edge = index 0 or index count−1 exactly; two error message variants), and `PlaceTopNScoredAtEdges` (edge-position mapping 0, count−1, 1, count−2, …; tie-score handling). Both ordering patterns carry a Placer dependency caveat callout. Added a Notes section covering D041 snapshot deferral rationale, `TotalTokensConsidered` scope clarification, and `SelectionReportAssertionException` type requirement. Precision audit confirmed 0 TBD fields and 0 "high-scoring" references. Both test suites passed with no regressions.

## Verification

```bash
# Pattern count (≥ 10, target 13)
grep -c "^### " spec/src/testing/vocabulary.md          → 26 (includes skeleton headings + all pattern headings; ≥ 10 PASS)

# No TBD fields
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md       → 0 PASS

# No undefined qualifiers
grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md → 0 PASS

# Error message format present per pattern
grep -c "Error message format" spec/src/testing/vocabulary.md → 15 (≥ 10 PASS)

# SUMMARY.md updated
grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"  → PASS

# No regressions — Rust
cargo test --manifest-path crates/cupel/Cargo.toml      → 35 passed, 0 failed PASS

# No regressions — .NET
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj → 583 succeeded, 0 failed PASS
```

## Requirements Advanced

- R043 — `spec/src/testing/vocabulary.md` now exists with 13 named assertion patterns, each with precise assertion semantics, predicate type, tolerance/edge cases, tie-breaking behavior, and error message format; no undefined terms remain

## Requirements Validated

- R043 (Cupel.Testing vocabulary design) — fully validated: ≥10 named patterns specified (13 delivered); no TBD fields; no undefined qualifiers ("high-scoring" etc.); `PlaceItemAtEdge` defines "edge" precisely (position 0 or count−1); `HaveBudgetUtilizationAbove` denominator locked to `MaxTokens`; all predicate-bearing methods use `IncludedItem`/`ExcludedItem` per PD-1; D041 honoured with explicit snapshot prohibition note; `grep -ci "\bTBD\b"` → 0; both test suites pass

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

None. All three tasks executed as planned. The T02 `### ` count was higher than the task plan expected (17 not 7) because the T01 skeleton already contained 10 `###`-level headings; the actual 7 new pattern headings were all correctly present.

## Known Limitations

- `ExcludeItemWithBudgetDetails` is a Rust-only assertion in practice: the .NET `ExclusionReason` is a flat enum with no associated `item_tokens`/`available_tokens` fields. The language note in the vocabulary specifies this asymmetry explicitly; .NET implementations may omit or adapt this assertion.
- `ExcludedItemsAreSortedByScoreDescending` cannot assert the insertion-order tiebreak (D019) because the report does not expose original insertion indices. This is documented as a known conformance gap.
- No snapshot assertions: ordering stability is not yet guaranteed without full input control; D041 prohibition is explicit with rationale in the Notes section.

## Follow-ups

- M003: implement Cupel.Testing NuGet package against this vocabulary (R021)
- M003: decide whether .NET provides a separate `ExcludeItemWithBudgetDetails` variant backed on a different mechanism, or simply omits it

## Files Created/Modified

- `spec/src/testing/vocabulary.md` — new file; ~490 lines; 13 fully-specified assertion patterns, pre-decisions, chain plumbing, and notes
- `spec/src/SUMMARY.md` — added `# Testing` section with `[Cupel.Testing Vocabulary](testing/vocabulary.md)` entry

## Forward Intelligence

### What the next slice should know
- `vocabulary.md` is the authoritative spec contract for the Cupel.Testing NuGet implementation; no further semantic debate is needed — the pre-decisions PD-1 through PD-4 are locked
- The `.NET flat enum` language asymmetry on `ExclusionReason` (no associated data) means `ExcludeItemWithBudgetDetails` cannot be implemented as specified in .NET without adding associated data to the enum; this is an M003 implementation decision

### What's fragile
- `ExcludedItemsAreSortedByScoreDescending` — the assertion is intentionally weaker than it appears: it only verifies score ordering, not insertion-order stability; test authors using it as a full ordering test will be surprised

### Authoritative diagnostics
- `grep "^### \`" spec/src/testing/vocabulary.md` — lists all 13 assertion pattern headings (plus Notes sub-headings)
- `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` — must be 0; any non-zero output means a pattern is incomplete
- `grep -c "Error message format" spec/src/testing/vocabulary.md` — must be ≥ 10; each pattern requires one

### What assumptions changed
- T02 `### ` heading count: the task plan expected 7 after T02, but the T01 skeleton already contained 10 `###`-level headings (PD section, Chain Plumbing sub-headings, pattern table heading); final count reflects all headings at that depth, not just assertion pattern headings
