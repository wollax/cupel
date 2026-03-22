---
id: T02
parent: S05
milestone: M002
provides:
  - spec/src/testing/vocabulary.md extended with Inclusion group (3 patterns) and Exclusion group (4 patterns), fully specified
key_files:
  - spec/src/testing/vocabulary.md
key_decisions:
  - No new decisions; all 7 patterns flowed directly from PD-1 (IncludedItem/ExcludedItem predicate types), PD-2, PD-3, PD-4 locked in T01
patterns_established:
  - "IncludeItemWithKind: existence check over included; ContextKind.Any not valid sentinel"
  - "IncludeItemMatching: predicate over IncludedItem (full pair); convenience ContextItem overloads are implementation-defined"
  - "IncludeExactlyNItemsWithKind: N=0 valid (no items of kind in included); semantically distinct from HaveNoExclusionsForKind"
  - "ExcludeItemWithReason: variant discriminant match (not string equality); all 8 variants valid including reserved"
  - "ExcludeItemMatchingWithReason: predicate over ContextItem; partial-match count in error distinguishes two failure modes"
  - "ExcludeItemWithBudgetDetails: exact integer equality on item_tokens and available_tokens; .NET flat enum language-asymmetry note"
  - "HaveNoExclusionsForKind: All over excluded; vacuous pass on empty excluded; semantically distinct from IncludeExactlyNItemsWithKind(kind,0)"
observability_surfaces:
  - none (spec-only changes)
duration: ~30 min
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T02: Spec patterns 1–7: Inclusion and Exclusion groups

**Extended `spec/src/testing/vocabulary.md` with 7 fully-specified assertion patterns — 3 Inclusion-group and 4 Exclusion-group — each with assertion semantics, predicate type, edge cases, error message format, and cross-pattern semantic distinctions.**

## What Happened

Opened `spec/src/diagnostics/selection-report.md` (IncludedItem/ExcludedItem field shapes) and `spec/src/diagnostics/exclusion-reasons.md` (full 8-variant ExclusionReason table) before writing to ensure exact field names were used throughout.

Confirmed from `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` that the .NET type is a flat enum (no associated data), and from `crates/cupel/src/diagnostics/mod.rs` that the Rust type uses data-carrying enum variants — this asymmetry is the basis for the language note on `ExcludeItemWithBudgetDetails`.

Appended the **Inclusion group** section with three fully-specified patterns:
- `IncludeItemWithKind` — existence check with `ContextKind.Any` invalid-sentinel note and `[actualKinds]` error detail
- `IncludeItemMatching` — predicate over `IncludedItem` (PD-1); optional up-to-5-item error appendix
- `IncludeExactlyNItemsWithKind` — exact count; N=0 semantics explicit; distinction from HaveNoExclusionsForKind established here

Appended the **Exclusion group** section with four fully-specified patterns:
- `ExcludeItemWithReason` — variant discriminant match; all 8 variants (including reserved) are valid arguments
- `ExcludeItemMatchingWithReason` — predicate over `ContextItem`; error distinguishes "0 predicate matches" vs "N matches, wrong reason" via `predicateMatchCount`
- `ExcludeItemWithBudgetDetails` — exact integer equality on `item_tokens`/`available_tokens`; full language-asymmetry note for .NET flat enum
- `HaveNoExclusionsForKind` — `All` predicate; vacuous pass on empty excluded; semantic distinction from `IncludeExactlyNItemsWithKind(kind,0)` stated explicitly

## Verification

```
grep -c "^### " spec/src/testing/vocabulary.md          → 17 (10 from skeleton + 7 new patterns)
grep -ci "\bTBD\b" spec/src/testing/vocabulary.md       → 0
grep -c "Language note" spec/src/testing/vocabulary.md  → 1
grep -c "Error message format" spec/src/testing/vocabulary.md → 7
grep -q "testing/vocabulary" spec/src/SUMMARY.md        → PASS
grep -c "high-scoring\|high scoring" spec/src/testing/vocabulary.md → 0
cargo test --manifest-path crates/cupel/Cargo.toml      → 35 passed, 0 failed
dotnet test ...Wollax.Cupel.Tests.csproj                → 583 succeeded, 0 failed
```

## Diagnostics

Spec-only changes; no runtime observability surfaces introduced.

Inspect via:
- `grep "^### " spec/src/testing/vocabulary.md` — all pattern headings
- `grep -A 30 "ExcludeItemWithBudgetDetails" spec/src/testing/vocabulary.md` — language note for .NET flat enum
- `grep -A 5 "IncludeExactlyNItemsWithKind\|HaveNoExclusionsForKind" spec/src/testing/vocabulary.md` — semantic distinction note

## Deviations

The task plan's verification check `grep -c "^### "` → 7 did not account for the 10 pre-existing `###` headings in the T01 skeleton (PD-1/PD-2/PD-3/PD-4, What is/What is not, Type shape, Error message contract, Implementation cost, Pattern summary table). The actual count is 17 (10 + 7 new). All 7 new pattern headings are present and correct.

## Known Issues

None.

## Files Created/Modified

- `spec/src/testing/vocabulary.md` — extended with Inclusion group (patterns 1–3) and Exclusion group (patterns 4–7), ~190 additional lines
