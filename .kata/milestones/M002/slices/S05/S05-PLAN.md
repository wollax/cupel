# S05: Cupel.Testing Vocabulary Design

**Goal:** Write `spec/src/testing/vocabulary.md` defining 13 named assertion patterns over `SelectionReport`, each with a precise spec (what it asserts, tolerance/edge cases, tie-breaking, error message format), and update `spec/src/SUMMARY.md` with the new Testing section.

**Demo:** `grep -c "^### " spec/src/testing/vocabulary.md` returns ≥ 10 (target 13); `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` returns 0; `grep -q "testing/vocabulary" spec/src/SUMMARY.md` passes; `cargo test` and `dotnet test` both pass.

## Must-Haves

- `spec/src/testing/vocabulary.md` exists with ≥ 10 (target 13) named assertion patterns
- Every pattern specifies: assertion semantics, predicate type, tolerance/edge cases, tie-breaking behavior, and error message format
- No pattern uses "high-scoring", "dominant", "recent", or any other undefined qualifier — all patterns use explicit parameters
- `PlaceItemAtEdge` defines "edge" = position 0 or position `included.Count − 1` exactly
- `PlaceTopNScoredAtEdges(n)` defines edge-position mapping (0, count−1, 1, count−2, …) and tie-score handling
- `HaveBudgetUtilizationAbove` denominator locked to `budget.MaxTokens` (per DI-3 + D045); not `TargetTokens`
- All predicate-bearing methods specify `IncludedItem` / `ExcludedItem` as the predicate type (not raw `ContextItem`)
- `ExcludeItemWithBudgetDetails` includes a language-asymmetry note (Rust data-carrying enum vs .NET flat enum)
- Placer-dependency caveat applied to every ordering-sensitive assertion
- D041 (no snapshots, no FluentAssertions) honoured — explicit note in chapter
- `spec/src/SUMMARY.md` updated with new "Testing" top-level section
- No TBD fields in any pattern: `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` → 0
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes (no regressions)
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes

## Proof Level

- This slice proves: **contract** — vocabulary document exists with all required precision decisions resolved; M003 implementation can build against it without re-debating semantics
- Real runtime required: no (spec-only, D039)
- Human/UAT required: yes — human review of vocabulary chapter for internal consistency before marking the slice done

## Verification

```bash
# 1. Pattern count (must be ≥ 10, target 13)
grep -c "^### " spec/src/testing/vocabulary.md

# 2. No undefined terms
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md   # → 0
grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md  # → 0

# 3. Error message format present per pattern
grep -c "Error message format\|**Error message" spec/src/testing/vocabulary.md  # → ≥ 10

# 4. SUMMARY.md updated with testing link
grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"

# 5. No regressions
cargo test --manifest-path crates/cupel/Cargo.toml
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
```

## Observability / Diagnostics

- Runtime signals: none (spec-only changes)
- Inspection surfaces: `spec/src/testing/vocabulary.md` (grep checks above); `spec/src/SUMMARY.md`
- Failure visibility: grep checks show exactly which must-haves are missing
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed:
  - `spec/src/diagnostics/selection-report.md` — `Included` (final placed order), `Excluded` (score-desc, insertion-order tiebreak), `total_candidates`, `total_tokens_considered` field semantics
  - `spec/src/diagnostics/exclusion-reasons.md` — exact variant names, associated fields (item_tokens, available_tokens, deduplicated_against)
  - `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — 15-candidate vocabulary table, per-pattern precision analysis, DI-1 through DI-5
  - `spec/src/scorers/metadata-trust.md` — chapter style template (overview, algorithm, conformance notes)
  - `crates/cupel/src/diagnostics/mod.rs` and `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — language asymmetry for ExcludeItemWithBudgetDetails
- New wiring introduced in this slice: `spec/src/testing/` directory + `vocabulary.md`; SUMMARY.md "Testing" section
- What remains before the milestone is truly usable end-to-end: S06 (DecayScorer/OTel/budget-simulation spec chapters); M003 implementation of Cupel.Testing NuGet package

## Tasks

- [x] **T01: Create vocabulary.md skeleton, lock pre-decisions, write chain plumbing and intro sections** `est:45m`
  - Why: All per-pattern specs depend on shared design choices (predicate type, denominator, tolerance, chain type). Locking these first prevents inconsistency across T02/T03 and establishes the document structure that T02/T03 fill in.
  - Files: `spec/src/testing/vocabulary.md` (new), `spec/src/SUMMARY.md`
  - Do: Create `spec/src/testing/vocabulary.md` with: (1) Overview section stating what Cupel.Testing is and is not (no FA, no snapshots per D041); (2) Pre-decisions section locking predicate type = `IncludedItem`/`ExcludedItem`, BudgetUtilization denominator = `budget.MaxTokens`, floating-point threshold comparison = exact `>=`/`<=` with no epsilon, chain plumbing entry point = `SelectionReport.Should()` returning `SelectionReportAssertionChain`; (3) Chain Plumbing section defining `SelectionReportAssertionChain` type shape, method return type (self for chaining), and failure mechanism (`SelectionReportAssertionException` with structured message); (4) Vocabulary section header with 13-pattern list table. Update `spec/src/SUMMARY.md` to add a "Testing" top-level section after Diagnostics with `[Cupel.Testing Vocabulary](testing/vocabulary.md)`.
  - Verify: `ls spec/src/testing/vocabulary.md` exists; `grep -q "testing/vocabulary" spec/src/SUMMARY.md`; `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` → 0 for sections written
  - Done when: vocabulary.md has Overview, Pre-decisions, Chain Plumbing, and skeleton Vocabulary section (13-row pattern table); SUMMARY.md updated; no TBD in written sections

- [x] **T02: Spec patterns 1–7: Inclusion and Exclusion groups** `est:60m`
  - Why: Specifies the 7 patterns covering item presence (3), item absence and exclusion reasons (4). These are the most commonly used patterns and must be written before ordering/budget patterns that may reference them.
  - Files: `spec/src/testing/vocabulary.md`
  - Do: Write the full spec for each of the 7 patterns in the Inclusion group and Exclusion group. For each pattern: (a) sub-heading with method signature, (b) what it asserts (precise), (c) predicate type decision (IncludedItem vs ExcludedItem vs ContextItem overloads), (d) edge cases / tolerance, (e) tie-breaking behavior (if applicable), (f) error message format (exact template). Patterns to write: (1) `IncludeItemWithKind(ContextKind)` — P01, `Included.Any(i => i.Item.Kind == kind)`; (2) `IncludeItemMatching(Func<IncludedItem, bool>)` — P03, `Included.Any(predicate)`; (3) `IncludeExactlyNItemsWithKind(ContextKind, int n)` — P02, exact count, N=0 valid spelling; (4) `ExcludeItemWithReason(ExclusionReason)` — A01, variant discriminant match not string match, covers reserved variants; (5) `ExcludeItemMatchingWithReason(Func<ContextItem, bool>, ExclusionReason)` — A02, combined predicate+reason, error shows partial-match count; (6) `ExcludeItemWithBudgetDetails(Func<ContextItem, bool>, int expectedItemTokens, int expectedAvailableTokens)` — A03 with language note: full spec against Rust data-carrying enum; explicit note that .NET `ExclusionReason` is a flat enum with no item_tokens/available_tokens fields and this assertion may be omitted or adapted in .NET implementations; (7) `HaveNoExclusionsForKind(ContextKind)` — `Excluded.All(e => e.Item.Kind != kind)`.
  - Verify: `grep -c "^### " spec/src/testing/vocabulary.md` → 7 after this task; `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` → 0; each pattern section contains an "Error message format:" line
  - Done when: 7 patterns fully specified with no undefined terms, each with precise assertion, edge cases, and error message format

- [x] **T03: Spec patterns 8–13: Aggregate, Budget, Coverage, Ordering + final audit and regression** `est:60m`
  - Why: Completes the vocabulary with the remaining 6 patterns (count aggregates, budget utilization, kind coverage, and placement assertions), performs a document-wide precision audit to ensure no TBD/undefined terms remain, and runs the test suites to confirm no regressions.
  - Files: `spec/src/testing/vocabulary.md`
  - Do: Write the full spec for patterns 8–13: (8) `HaveAtLeastNExclusions(int n)` — `Excluded.Count >= n`, note N=0 is `HaveNoExclusions` alias; (9) `HaveBudgetUtilizationAbove(double threshold, ContextBudget)` — `sum(included[i].tokens) / budget.MaxTokens >= threshold`, exact `>=` no epsilon, document denominator choice with rationale (MaxTokens is hard ceiling; TargetTokens is Slice-stage-internal, not a public capacity metric), error message shows includedTokens and budgetMax; (10) `HaveKindCoverageCount(int n)` — `Included.Select(i => i.Item.Kind).Distinct().Count() >= n`; (11) `ExcludedItemsAreSortedByScoreDescending()` — conformance assertion, asserts score-descending property only (insertion-order tiebreak unobservable from report per D019), document caveat explicitly; (12) `PlaceItemAtEdge(Func<IncludedItem, bool>)` — `predicate(Included[0]) || predicate(Included[^1])`, Placer dependency caveat, error shows actual index if item is in Included but not at edge; (13) `PlaceTopNScoredAtEdges(int n)` — top-N by score descending, edge positions enumerated as (0, count−1, 1, count−2, …), tie-score handling: any item with the tied score is valid at that edge position (not a specific one), Placer dependency caveat. After patterns, add final Notes section: D041 prohibition on snapshots (rationale: insertion-order tiebreak makes serialized ordering non-deterministic without full input control), `TotalTokensConsidered` is a candidate-set metric (not a utilization metric) — not included as a utilization assertion. Run precision audit: `grep -ci "\bTBD\b"` → 0, `grep -c "high-scoring"` → 0. Run test suites.
  - Verify: `grep -c "^### " spec/src/testing/vocabulary.md` → 13; `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` → 0; `grep -c "high-scoring" spec/src/testing/vocabulary.md` → 0; `cargo test --manifest-path crates/cupel/Cargo.toml` passes; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes
  - Done when: All 13 patterns specified, document-wide audit clean, both test suites pass

## Files Likely Touched

- `spec/src/testing/vocabulary.md` (new)
- `spec/src/SUMMARY.md`
