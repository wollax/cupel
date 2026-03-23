# S05: Cupel.Testing Vocabulary Design — UAT

**Milestone:** M002
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: This slice is spec-only (D039). There is no runtime component to exercise. The artifact is `spec/src/testing/vocabulary.md`; correctness is verified by reading the document for internal consistency, precision, and completeness against the slice's Must-Have list. No server, no deployed service, no live integration is involved.

## Preconditions

- `spec/src/testing/vocabulary.md` exists (created in T01–T03)
- `spec/src/SUMMARY.md` has been updated with the Testing section (T01)
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes (T03 confirmed)
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes (T03 confirmed)

## Smoke Test

```bash
grep -c "^### " spec/src/testing/vocabulary.md    # must be ≥ 10
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md # must be 0
grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"
```

All three must pass without manual intervention.

## Test Cases

### 1. Pattern count meets minimum

1. Run: `grep -c "^### " spec/src/testing/vocabulary.md`
2. **Expected:** result ≥ 10 (slice target: 13 assertion patterns delivered)

### 2. No TBD fields remain

1. Run: `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md`
2. **Expected:** `0`

### 3. No undefined qualifiers

1. Run: `grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md`
2. **Expected:** `0`

### 4. Error message format present in every pattern

1. Run: `grep -c "Error message format" spec/src/testing/vocabulary.md`
2. **Expected:** result ≥ 10 (one per assertion pattern)

### 5. SUMMARY.md updated with Testing section

1. Run: `grep -q "testing/vocabulary" spec/src/SUMMARY.md && echo "PASS"`
2. **Expected:** `PASS`

### 6. BudgetUtilization denominator locked to MaxTokens

1. Open `spec/src/testing/vocabulary.md`
2. Find `HaveBudgetUtilizationAbove`
3. **Expected:** denominator is stated as `budget.MaxTokens`; explicit note that `TargetTokens` is NOT the denominator; rationale present (MaxTokens is the hard ceiling; TargetTokens is Slice-stage-internal)

### 7. PlaceItemAtEdge defines "edge" precisely

1. Open `spec/src/testing/vocabulary.md`
2. Find `PlaceItemAtEdge`
3. **Expected:** "edge" is defined as position 0 OR position `Included.Count − 1` exactly; no "same-score adjacency" or similar vague phrasing

### 8. PlaceTopNScoredAtEdges defines edge-position mapping

1. Open `spec/src/testing/vocabulary.md`
2. Find `PlaceTopNScoredAtEdges`
3. **Expected:** edge positions enumerated as 0, count−1, 1, count−2, … (alternating inward); tie-score handling specified (any tied-score item is valid at the N-th position)

### 9. Placer dependency caveat on ordering-sensitive assertions

1. Open `spec/src/testing/vocabulary.md`
2. Find `PlaceItemAtEdge` and `PlaceTopNScoredAtEdges`
3. **Expected:** both carry a Placer dependency caveat (assertions only valid when a placement-aware Placer is used)

### 10. ExcludeItemWithBudgetDetails language-asymmetry note

1. Open `spec/src/testing/vocabulary.md`
2. Find `ExcludeItemWithBudgetDetails`
3. **Expected:** explicit note that .NET `ExclusionReason` is a flat enum with no associated `item_tokens`/`available_tokens` fields; .NET implementations may omit or adapt this assertion

### 11. All predicate-bearing patterns use IncludedItem / ExcludedItem

1. Scan each pattern in the Inclusion and Exclusion groups
2. **Expected:** predicates operate on `IncludedItem` (for Inclusion patterns) or `ContextItem` / `ExcludedItem` (for Exclusion patterns) as specified in PD-1; no pattern accepts a raw `ContextItem` where `IncludedItem` is the right type

### 12. D041 snapshot prohibition noted

1. Open `spec/src/testing/vocabulary.md`
2. Find the Notes section
3. **Expected:** explicit statement that snapshot assertions are deferred because insertion-order tiebreak makes serialized ordering non-deterministic without full input control; reference to D041

### 13. Cargo + dotnet regression tests pass

1. Run: `cargo test --manifest-path crates/cupel/Cargo.toml`
2. Run: `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`
3. **Expected:** both suites pass with 0 failures (spec-only changes introduce no regressions)

## Edge Cases

### ExcludeItemWithReason accepts reserved variants

1. Open `spec/src/testing/vocabulary.md`
2. Find `ExcludeItemWithReason`
3. **Expected:** doc explicitly states all 8 `ExclusionReason` variants (including reserved ones like `Reserved2`, `Reserved3`) are valid arguments; no restriction to "common" variants

### HaveNoExclusionsForKind vacuous pass

1. Open `spec/src/testing/vocabulary.md`
2. Find `HaveNoExclusionsForKind`
3. **Expected:** doc states that an empty `Excluded` list causes a vacuous pass (there are no exclusions of any kind); this is noted as intentional

### IncludeExactlyNItemsWithKind with N=0

1. Open `spec/src/testing/vocabulary.md`
2. Find `IncludeExactlyNItemsWithKind`
3. **Expected:** N=0 is a valid invocation asserting that no items of the given kind appear in `Included`; semantic distinction from `HaveNoExclusionsForKind` is stated

### ExcludedItemsAreSortedByScoreDescending tiebreak caveat

1. Open `spec/src/testing/vocabulary.md`
2. Find `ExcludedItemsAreSortedByScoreDescending`
3. **Expected:** doc explicitly states that insertion-order tiebreak (D019) is real but not assertable from the report alone; the assertion only verifies score-descending order

## Failure Signals

- `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` returns non-zero — a pattern is incomplete
- Any pattern section is missing an "Error message format:" heading — pattern is underspecified
- `HaveBudgetUtilizationAbove` references `TargetTokens` as denominator — decision DI-3/D045 was not honoured
- `PlaceItemAtEdge` or `PlaceTopNScoredAtEdges` lacks a Placer dependency caveat — ordering assertions are misleading
- `spec/src/SUMMARY.md` has no `testing/vocabulary` link — chapter is unreachable from the mdBook nav

## Requirements Proved By This UAT

- **R043** (Cupel.Testing vocabulary design) — proved by: ≥10 named assertion patterns exist in `vocabulary.md`; each specifies what it asserts, tolerance/edge cases, tie-breaking behavior, and error message format; no undefined terms remain (`grep -ci "\bTBD\b"` → 0, "high-scoring" → 0); D041 snapshot prohibition honoured; `PlaceItemAtEdge` edge definition is explicit; `HaveBudgetUtilizationAbove` denominator locked to `MaxTokens`; all predicate-bearing methods use `IncludedItem`/`ExcludedItem`; language asymmetry for `.NET` noted; chapter reachable via `SUMMARY.md`; both test suites pass confirming no regressions

## Not Proven By This UAT

- **R021** (Cupel.Testing NuGet package implementation) — this UAT only proves the vocabulary spec design; the actual .NET library implementing these assertion patterns is deferred to M003
- Runtime behaviour of the assertion chains — no code was written; the vocabulary document is a spec contract, not an implementation
- Whether the `.NET ExclusionReason` flat-enum limitation is resolved — the language note documents the asymmetry but the implementation decision (omit, adapt, or extend the enum) is deferred to M003
- `ExcludedItemsAreSortedByScoreDescending` asserting insertion-order tiebreak — the spec explicitly states this is not provable from the report; no future UAT can prove this without extending the `SelectionReport` schema

## Notes for Tester

- The document uses `###` headings for both assertion pattern headings and structural sub-headings within the pre-decisions and chain plumbing sections. The total `grep -c "^### "` count (26) exceeds 13; this is expected. To list only the assertion pattern headings, use `grep "^### \`"` (backtick prefix marks assertion pattern headings).
- `ExcludeItemWithBudgetDetails` is intentionally Rust-centric. If reviewing for .NET balance, the language-asymmetry note is the correct place to evaluate completeness — the assertion is not a .NET gap, it is an explicitly documented limitation.
- The `HaveBudgetUtilizationAbove` denominator choice (`MaxTokens` vs `TargetTokens`) is locked by D045/DI-3. Do not accept a revision that changes this to `TargetTokens` without a corresponding DECISIONS.md update.
- `rtk dotnet test` exits non-zero with TUnit's test runner; use `dotnet test` directly to verify regression status.
