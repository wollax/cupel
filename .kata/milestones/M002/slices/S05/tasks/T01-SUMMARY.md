---
id: T01
parent: S05
milestone: M002
provides:
  - spec/src/testing/vocabulary.md skeleton with Overview, Pre-decisions, Chain Plumbing, and 13-pattern Vocabulary summary table
  - spec/src/SUMMARY.md updated with # Testing section containing vocabulary.md link
key_files:
  - spec/src/testing/vocabulary.md
  - spec/src/SUMMARY.md
key_decisions:
  - PD-1 locked: predicate type is IncludedItem/ExcludedItem, not raw ContextItem
  - PD-2 locked: BudgetUtilization denominator is budget.MaxTokens (per D045/DI-3)
  - PD-3 locked: exact operator no epsilon (>= lower-bound, <= upper-bound) per D064
  - PD-4 locked: Should() entry point, SelectionReportAssertionChain, SelectionReportAssertionException
patterns_established:
  - vocabulary.md structure: Overview → Pre-decisions → Chain Plumbing → Vocabulary (T02/T03 fill per-pattern specs)
observability_surfaces:
  - none (spec-only, D039)
duration: 20m
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Create vocabulary.md skeleton, lock pre-decisions, write chain plumbing and intro sections

**Created `spec/src/testing/vocabulary.md` with all four foundational sections and updated `spec/src/SUMMARY.md` with a new # Testing section.**

## What Happened

Created `spec/src/testing/` directory and wrote `spec/src/testing/vocabulary.md` containing:

- **Overview**: defines what Cupel.Testing is (language-agnostic named assertion patterns over SelectionReport), what it is not (no FluentAssertions, no snapshots per D041, not an implementation), and why it is a prerequisite for R021 in M003.
- **Pre-decisions**: four locked design choices (PD-1 through PD-4) that all 13 per-pattern specs depend on. Locking these first prevents inconsistency across T02/T03: predicate type (IncludedItem/ExcludedItem), BudgetUtilization denominator (MaxTokens per D045/DI-3), floating-point comparison (exact operator, no epsilon per D064), and chain plumbing entry point/failure mechanism.
- **Chain Plumbing**: full `SelectionReportAssertionChain` type shape with all 13 method signatures, error message contract, and implementation cost note (~100 lines).
- **Vocabulary skeleton**: 13-row summary table with group, method name, and one-line description for all patterns. Per-pattern full specs are delegated to T02 (patterns 1–7) and T03 (patterns 8–13).

Updated `spec/src/SUMMARY.md` to insert a `# Testing` section (after `# Conformance`, before `# Appendix`) containing `[Cupel.Testing Vocabulary](testing/vocabulary.md)`.

## Verification

```
$ ls spec/src/testing/vocabulary.md
spec/src/testing/vocabulary.md                          ✓

$ grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"
PASS                                                    ✓

$ grep -ci "\bTBD\b" spec/src/testing/vocabulary.md
0                                                       ✓

$ grep "^## " spec/src/testing/vocabulary.md
## Overview
## Pre-decisions
## Chain Plumbing
## Vocabulary                                           ✓ all four sections present
```

Slice-level verification (partial pass — T01 scope only):
- Pattern count check (`grep -c "^### "`) → 0 (expected; `###` patterns are written in T02/T03)
- No TBD → 0 ✓
- SUMMARY.md updated → PASS ✓
- Full regression checks (cargo test, dotnet test) deferred to T03

## Diagnostics

Spec-only changes; no runtime observability surfaces introduced. Inspect via:
- `cat spec/src/testing/vocabulary.md` — full document
- `grep "^## " spec/src/testing/vocabulary.md` — section structure
- `grep -q "testing/vocabulary" spec/src/SUMMARY.md` — nav linkage

## Deviations

None. Followed the task plan exactly. SUMMARY.md insertion point matches the plan (after # Conformance, before # Appendix).

## Known Issues

None. T02 and T03 will fill in the per-pattern specifications; the skeleton table in the Vocabulary section has all 13 patterns listed with their groups and one-line descriptions.

## Files Created/Modified

- `spec/src/testing/vocabulary.md` — new file; Overview, Pre-decisions, Chain Plumbing, and Vocabulary skeleton sections
- `spec/src/SUMMARY.md` — added `# Testing` section with `[Cupel.Testing Vocabulary](testing/vocabulary.md)` entry
