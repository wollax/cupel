---
id: T02
parent: S02
milestone: M002
provides:
  - Editorial fixes across 6 algorithm spec files (slicers, placers, scorers)
key_files:
  - spec/src/slicers/greedy.md
  - spec/src/slicers/knapsack.md
  - spec/src/placers/u-shaped.md
  - spec/src/scorers/composite.md
  - spec/src/scorers/scaled.md
  - spec/src/scorers/kind.md
key_decisions:
  - none
patterns_established:
  - none
observability_surfaces:
  - none
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T02: Fix algorithm spec files — slicers, placers, scorers

**Applied 6 targeted editorial changes across slicer, placer, and scorer spec files.**

## What Happened

Applied all 6 planned editorial changes:

1. **`greedy.md`** — Added clarifying note in Value Density section that among zero-token items the sort tiebreak is index only and score values are irrelevant. Added matching Conformance Notes bullet: "Among zero-token items, tiebreaking is by original index only; score values MUST NOT affect the relative order of zero-token items."

2. **`knapsack.md`** — Added implementation note in Score Scaling subsection after the `integerValue = max(0, floor(score * 10000))` line clarifying that for non-negative scores (all Cupel scores), `floor` and C-style truncation-toward-zero produce identical results and either is conformant.

3. **`u-shaped.md`** — Corrected the Edge Cases table "Pinned items (score 1.0)" row from "Placed at edges alongside other high-scored items" to explicitly state that UShapedPlacer does not special-case pinned items; they naturally rank highly because they arrive with score 1.0 from the pipeline.

4. **`composite.md`** — Replaced the `// Store scorers and normalizedWeights` comment in `CONSTRUCT-COMPOSITE` pseudocode with two explicit assignment lines: `self.scorers <- [entries[i].scorer for i in 0..length(entries)]` and `self.normalizedWeights <- normalizedWeights`.

5. **`scaled.md`** — Appended nesting depth warning to Performance note: nested `ScaledScorer` instances compound cost to O(N^(depth+1)) per scoring pass; callers should prefer a flat `CompositeScorer` structure.

6. **`kind.md`** — Updated pseudocode comment from `// case-insensitive lookup` to `// ContextKind equality is case-insensitive (see enumerations.md)`. Added prose note below the pseudocode block: "Case-insensitivity is a property of `ContextKind` equality as defined in [Enumerations](../data-model/enumerations.md#contextkind), not an ad-hoc behavior of the weight dictionary."

## Verification

All task-level grep checks passed:
- `grep "Placed at edges alongside other high-scored items" u-shaped.md` → 0 matches ✓
- `grep "// Store scorers and normalizedWeights" composite.md` → 0 matches ✓
- `grep "self.scorers" composite.md` → 1 match ✓
- `grep "self.normalizedWeights" composite.md` → 1 match ✓
- `grep "depth\|nested\|compound" scaled.md` → 1 match ✓
- `grep "index only\|index-only" greedy.md` → 2 matches ✓
- `grep "enumerations.md\|ContextKind equality" kind.md` → 4 matches ✓

Test suites:
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed, 1 ignored ✓
- `dotnet test` → 583 passed, 0 failed ✓

## Diagnostics

None — spec-only changes, no runtime signals added.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `spec/src/slicers/greedy.md` — zero-token tiebreak note in Value Density + Conformance Notes bullet
- `spec/src/slicers/knapsack.md` — floor/truncation equivalence note in Score Scaling subsection
- `spec/src/placers/u-shaped.md` — corrected Pinned items Edge Cases table row
- `spec/src/scorers/composite.md` — two explicit assignment lines replacing the comment
- `spec/src/scorers/scaled.md` — nesting depth performance warning appended
- `spec/src/scorers/kind.md` — case-insensitivity source clarification in pseudocode comment and prose
