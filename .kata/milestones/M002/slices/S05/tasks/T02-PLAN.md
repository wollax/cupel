---
estimated_steps: 5
estimated_files: 1
---

# T02: Spec patterns 1–7: Inclusion and Exclusion groups

**Slice:** S05 — Cupel.Testing Vocabulary Design
**Milestone:** M002

## Description

Write the full per-pattern specs for the 7 patterns in the Inclusion group (3 patterns) and Exclusion group (4 patterns). These are the most commonly used patterns and form the foundation of the vocabulary. Each pattern gets a complete sub-section with: method signature, assertion semantics, predicate type, edge cases, tie-breaking (if applicable), and error message format.

This task extends `spec/src/testing/vocabulary.md` built in T01 — the Pre-decisions section is already locked, so all predicate type and error message format decisions flow from those choices.

## Steps

1. Read `spec/src/diagnostics/selection-report.md` (IncludedItem fields) and `spec/src/diagnostics/exclusion-reasons.md` (ExclusionReason variant table) before writing — pattern specs must reference exact field names.
2. Write the **Inclusion group** section in `vocabulary.md` with 3 patterns:
   - **`IncludeItemWithKind(ContextKind kind)`** — asserts `Included.Any(i => i.Item.Kind == kind)`. Predicate type: `IncludedItem`. No ordering dependency. Edge cases: empty `Included` → fails with 0 count. Error message format: `"IncludeItemWithKind({kind}) failed: Included contained 0 items with Kind={kind}. Included had {count} items with kinds: [{actualKinds}]."` Note: `ContextKind.Any` is not a valid sentinel for this assertion; pass the specific kind.
   - **`IncludeItemMatching(Func<IncludedItem, bool> predicate)`** — asserts `Included.Any(predicate)`. Predicate over `IncludedItem` (not raw `ContextItem`) so callers can inspect `score` and `reason`. Convenience overloads over `ContextItem` are implementation-defined. Edge cases: empty `Included` → fails with 0 items; predicate that never returns true → includes a summary of included items (up to 5) in the error. Error message format: `"IncludeItemMatching failed: no item in Included matched the predicate. Included had {count} items."` Optionally append first 5 items.
   - **`IncludeExactlyNItemsWithKind(ContextKind kind, int n)`** — asserts `Included.Count(i => i.Item.Kind == kind) == n`. N=0 is a valid spelling meaning no items of that kind are in Included (equivalent to the NotIncludeKind pattern). Error message format: `"IncludeExactlyNItemsWithKind({kind}, {n}) failed: expected {n} items with Kind={kind} in Included, but found {actual}. Included had {count} items total."`
3. Write the **Exclusion group** section with 4 patterns:
   - **`ExcludeItemWithReason(ExclusionReason reason)`** — asserts `Excluded.Any(e => e.Reason is <reason variant>)`. Variant discriminant match (not string equality). Must handle all variants including reserved ones — reserved variants (ScoredTooLow, QuotaCapExceeded, QuotaRequireDisplaced, Filtered) are valid arguments even if never emitted by built-in stages. Error message format: `"ExcludeItemWithReason({reason}) failed: no excluded item had reason {reason}. Excluded had {count} items with reasons: [{reasonList}]."`
   - **`ExcludeItemMatchingWithReason(Func<ContextItem, bool> predicate, ExclusionReason reason)`** — asserts `Excluded.Any(e => predicate(e.Item) && e.Reason is <reason variant>)`. Predicate over `ContextItem` here (callers filter by content/kind; the reason check is the second filter). Error message shows partial-match count to distinguish "predicate matched 0 items" from "predicate matched N items but none had the expected reason". Error message format: `"ExcludeItemMatchingWithReason(reason={reason}) failed: predicate matched {predicateMatchCount} excluded item(s) but none had reason {reason}. Matched items had reasons: [{actualReasons}]."`
   - **`ExcludeItemWithBudgetDetails(Func<ContextItem, bool> predicate, int expectedItemTokens, int expectedAvailableTokens)`** — asserts that there exists an excluded item matching `predicate` where `reason == BudgetExceeded`, `reason.item_tokens == expectedItemTokens`, and `reason.available_tokens == expectedAvailableTokens`. Exact integer equality for both token values. `available_tokens` is `effective_target − sum(sliced_item.tokens)` at the moment of exclusion (D025). Error message format: `"ExcludeItemWithBudgetDetails failed: expected BudgetExceeded with item_tokens={eIT}, available_tokens={eAT}, but found item_tokens={aIT}, available_tokens={aAT}."` Include a language-asymmetry note: "**Language note:** In .NET, `ExclusionReason` is a flat enum with no associated data. The `item_tokens` and `available_tokens` fields are not available on `ExcludedItem.Reason`. .NET implementations MAY omit this assertion or surface it differently (e.g. `HaveExcludedItemWithBudgetExceeded(predicate)` without the token detail parameters)."
   - **`HaveNoExclusionsForKind(ContextKind kind)`** — asserts `Excluded.All(e => e.Item.Kind != kind)`. Note: semantically distinct from `IncludeExactlyNItemsWithKind(kind, 0)` — this pattern tests that items of the given kind are not *excluded*, while the inclusion pattern tests that none are *included*. An item of a given kind could be absent from both lists if it was never a candidate. Error message format: `"HaveNoExclusionsForKind({kind}) failed: found {count} excluded item(s) with Kind={kind}. First: score={score}, reason={reason}."`
4. Review all 7 patterns for: no TBD fields, no undefined terms, consistent use of `IncludedItem`/`ExcludedItem`/`ContextItem` per the T01 pre-decisions, no "high-scoring" or similar vague qualifiers.
5. Run `grep -ci "\bTBD\b" spec/src/testing/vocabulary.md` to confirm 0.

## Must-Haves

- [ ] `IncludeItemWithKind` fully specified with error message format
- [ ] `IncludeItemMatching` fully specified: predicate over `IncludedItem`, error message format
- [ ] `IncludeExactlyNItemsWithKind` fully specified: N=0 valid, error message format
- [ ] `ExcludeItemWithReason` fully specified: variant discriminant match, covers reserved variants, error message format
- [ ] `ExcludeItemMatchingWithReason` fully specified: predicate over `ContextItem`, partial-match count in error, error message format
- [ ] `ExcludeItemWithBudgetDetails` fully specified: exact integer equality, language-asymmetry note (.NET flat enum), error message format
- [ ] `HaveNoExclusionsForKind` fully specified: semantically distinct from `IncludeExactlyNItemsWithKind(kind, 0)`, error message format
- [ ] No TBD in any of the 7 pattern specs
- [ ] All predicate type choices consistent with T01 pre-decisions

## Verification

```bash
# 7 patterns written
grep -c "^### " spec/src/testing/vocabulary.md   # → 7

# No TBD
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md   # → 0

# Language note for ExcludeItemWithBudgetDetails present
grep -q "Language note" spec/src/testing/vocabulary.md && echo "PASS"

# Error message format present (at least 7 occurrences)
grep -c "Error message format\|Error message:" spec/src/testing/vocabulary.md   # → ≥ 7
```

## Observability Impact

- Signals added/changed: None (spec-only)
- How a future agent inspects this: `grep "^### " spec/src/testing/vocabulary.md` to list all pattern headings; `grep -A 20 "ExcludeItemWithBudgetDetails" spec/src/testing/vocabulary.md` to review the language note
- Failure state exposed: grep checks above show which patterns are missing or incomplete

## Inputs

- `spec/src/testing/vocabulary.md` — skeleton from T01 with Overview, Pre-decisions, Chain Plumbing sections
- `spec/src/diagnostics/selection-report.md` — `IncludedItem` (item, score, reason), `ExcludedItem` (item, score, reason) field shapes
- `spec/src/diagnostics/exclusion-reasons.md` — full variant table: `BudgetExceeded` (item_tokens, available_tokens), `Deduplicated` (deduplicated_against), `NegativeTokens` (tokens), `PinnedOverride` (displaced_by), reserved variants
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — P01–P06, A01–A07 per-pattern precision analyses
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — .NET flat enum confirmation (for language note)
- `crates/cupel/src/diagnostics/mod.rs` — Rust data-carrying enum confirmation (for language note)
- D025 — `available_tokens = effective_target - sum(sliced_item.tokens)` at exclusion time

## Expected Output

- `spec/src/testing/vocabulary.md` — extended with Inclusion group (3 patterns) and Exclusion group (4 patterns), fully specified; ~150–200 additional lines
