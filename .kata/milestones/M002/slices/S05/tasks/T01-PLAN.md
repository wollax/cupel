---
estimated_steps: 5
estimated_files: 2
---

# T01: Create vocabulary.md skeleton, lock pre-decisions, write chain plumbing and intro sections

**Slice:** S05 — Cupel.Testing Vocabulary Design
**Milestone:** M002

## Description

Create `spec/src/testing/vocabulary.md` with the foundational sections: Overview (what Cupel.Testing is/isn't), Pre-decisions (locking the four shared design choices that every per-pattern spec depends on), Chain Plumbing (entry point, chain type, failure mechanism), and a Vocabulary skeleton table listing all 13 patterns. Update `spec/src/SUMMARY.md` with the new Testing section.

This task establishes the document structure and locked choices that T02 and T03 will fill in with per-pattern specs. Writing shared decisions first prevents inconsistency (e.g. different patterns using different predicate types or different denominators).

## Steps

1. Create the `spec/src/testing/` directory (it does not exist yet).
2. Write `spec/src/testing/vocabulary.md` with the following sections:
   - **Overview**: what the Cupel.Testing vocabulary is (a language-agnostic set of named assertion patterns over `SelectionReport`); what it is not (no implementation, no FluentAssertions, no snapshot assertions per D041 — note D041 and its rationale); note that the vocabulary is the prerequisite for R021 implementation in M003.
   - **Pre-decisions**: four locked choices that apply uniformly across all patterns:
     1. *Predicate type*: all predicate-bearing methods accept `IncludedItem` (for Inclusion-group methods) or `ExcludedItem` (for Exclusion-group methods), not raw `ContextItem`. Convenience overloads over `ContextItem` fields (Kind, Content) may be offered by implementations but are not part of the vocabulary spec.
     2. *BudgetUtilization denominator*: `budget.MaxTokens` (the hard token ceiling). Not `TargetTokens` (which is a Slice-stage-internal soft target, not a public capacity metric). Rationale: MaxTokens is the single authoritative ceiling callers control at construction time.
     3. *Floating-point threshold comparisons*: exact operator, no epsilon. `>=` for lower-bound thresholds (e.g. HaveBudgetUtilizationAbove), `<=` for upper-bound. The comparison operator is stated explicitly in each pattern spec. Floating-point edge cases (e.g. computed ratio = 0.80000000001 for a 0.80 threshold) are test authoring responsibility — callers should choose thresholds that avoid floating-point boundary cases.
     4. *Chain plumbing*: `SelectionReport.Should()` is the entry point; returns a `SelectionReportAssertionChain`; each assertion method returns `this` for chaining; on failure throws `SelectionReportAssertionException` (not `InvalidOperationException`) with a structured message containing: assertion name, what was expected, what was actually found.
   - **Chain Plumbing**: define the `SelectionReportAssertionChain` type shape — the type wraps a `SelectionReport`, exposes the 13 assertion methods, and exposes the error message contract. Each assertion method signature is listed (implementations fill in the body). Note: ~100 lines of chain plumbing code is the expected implementation cost; the vocabulary does not dictate the internal structure.
   - **Vocabulary** (skeleton): an introduction paragraph, then a summary table of all 13 patterns with their group, name, and one-line description. The full per-pattern spec follows in T02 and T03.
3. Update `spec/src/SUMMARY.md`: add a `# Testing` top-level section after the `# Conformance` block (before `# Appendix`) containing `[Cupel.Testing Vocabulary](testing/vocabulary.md)`.
4. Verify the file exists and SUMMARY.md is updated.
5. Confirm no TBD fields in the sections written so far.

## Must-Haves

- [ ] `spec/src/testing/vocabulary.md` created
- [ ] Overview section present: what Cupel.Testing is, D041 prohibition on snapshots/FA noted
- [ ] Pre-decisions section present: all four locked choices stated with rationale
- [ ] Chain Plumbing section present: `SelectionReportAssertionChain` entry point, method return type, `SelectionReportAssertionException` failure mechanism
- [ ] Vocabulary skeleton: summary table with all 13 pattern names and one-line descriptions
- [ ] `spec/src/SUMMARY.md` updated with `# Testing` section containing `vocabulary.md` link
- [ ] No TBD fields anywhere in the sections written

## Verification

```bash
# File exists
ls spec/src/testing/vocabulary.md

# SUMMARY.md updated
grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"

# No TBD in what's written
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md   # → 0

# Structure sanity: top-level sections present
grep "^## " spec/src/testing/vocabulary.md
# expect: Overview, Pre-decisions, Chain Plumbing, Vocabulary
```

## Observability Impact

- Signals added/changed: None (spec-only, no runtime changes)
- How a future agent inspects this: `cat spec/src/testing/vocabulary.md` for document content; `grep -q "testing/vocabulary" spec/src/SUMMARY.md` for navigation linkage
- Failure state exposed: If any must-have is missing, grep checks in Verification section of S05-PLAN.md will show which check fails

## Inputs

- `spec/src/diagnostics/selection-report.md` — `SelectionReport` field semantics needed to understand what patterns can assert; `IncludedItem` and `ExcludedItem` type shapes for predicate type decision
- `spec/src/scorers/metadata-trust.md` — chapter style template to follow for sectioning and formatting
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — DI-1 (chain plumbing entry point), DI-2 (predicate type), DI-3 (denominator), DI-4 (PlaceTopNScoredAtEdges), DI-5 (extension methods)
- `spec/src/SUMMARY.md` — existing structure to understand where to insert the new section
- D041 (no FA, no snapshots), D045 (MaxTokens denominator), S05-RESEARCH.md pre-decision section

## Expected Output

- `spec/src/testing/vocabulary.md` — skeleton with Overview, Pre-decisions, Chain Plumbing, and 13-row Vocabulary summary table; ~80-120 lines
- `spec/src/SUMMARY.md` — updated with `# Testing` section containing `[Cupel.Testing Vocabulary](testing/vocabulary.md)` entry
