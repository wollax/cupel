---
estimated_steps: 6
estimated_files: 6
---

# T02: Fix algorithm spec files — slicers, placers, scorers

**Slice:** S02 — Spec Editorial Debt
**Milestone:** M002

## Description

Apply six targeted editorial changes to the slicer, placer, and scorer spec files: greedy zero-token tiebreak clarification, knapsack floor/truncation equivalence note, UShapedPlacer pinned edge case table row correction, CompositeScorer pseudocode completion, ScaledScorer nesting depth warning, and KindScorer case-insensitivity source clarification.

## Steps

1. **`spec/src/slicers/greedy.md`** — In the Value Density section (immediately after the zero-token sentence "Items with `tokens = 0` have density `MAX_FLOAT`..."), add a clarifying note: "Among zero-token items, all share the same density `MAX_FLOAT`. The sort tiebreak is index only — score values are irrelevant for zero-token items." Also add a matching Conformance Note bullet: "Among zero-token items, tiebreaking is by original index only; score values MUST NOT affect the relative order of zero-token items."

2. **`spec/src/slicers/knapsack.md`** — In the Discretization > Score Scaling subsection, after the line `integerValue = max(0, floor(score * 10000))`, add a note: "**Implementation note:** For non-negative scores (which all Cupel scores are, since scorers return values in [0.0, 1.0]), `floor` and truncation-toward-zero (C-style integer truncation) produce identical results. Either operation is conformant."

3. **`spec/src/placers/u-shaped.md`** — In the Edge Cases table, find the row:
   ```
   | Pinned items (score 1.0) | Placed at edges alongside other high-scored items |
   ```
   Replace the cell content with:
   ```
   | Pinned items (score 1.0) | Not special-cased by UShapedPlacer; pinned items arrive with score 1.0 from the pipeline and naturally rank highly, sorting to edges. |
   ```

4. **`spec/src/scorers/composite.md`** — In the `CONSTRUCT-COMPOSITE` pseudocode, find the line:
   ```
       // Store scorers and normalizedWeights
   ```
   Replace it with two explicit assignment lines:
   ```
       self.scorers           <- [entries[i].scorer for i in 0..length(entries)]
       self.normalizedWeights <- normalizedWeights
   ```

5. **`spec/src/scorers/scaled.md`** — In the Performance note section (the paragraph beginning "**Performance note:** Each call to `SCALED-SCORE` invokes..."), append the following sentence after the existing text: "When `ScaledScorer` instances are deeply nested — a `ScaledScorer` wrapping another `ScaledScorer` — the cost compounds to O(*N*^(*depth*+1)) per scoring pass. Callers with multiple scale-normalization requirements should prefer a flat [`CompositeScorer`](composite.md) structure over nested `ScaledScorer` instances."

6. **`spec/src/scorers/kind.md`** — In the Algorithm pseudocode, replace the inline comment:
   ```
       if weights contains item.kind:    // case-insensitive lookup
   ```
   with:
   ```
       if weights contains item.kind:    // ContextKind equality is case-insensitive (see enumerations.md)
   ```
   Then add a brief prose note below the pseudocode block (or integrate into the existing paragraph): "Case-insensitivity is a property of `ContextKind` equality as defined in [Enumerations](../data-model/enumerations.md#contextkind), not an ad-hoc behavior of the weight dictionary."

## Must-Haves

- [ ] `greedy.md` Value Density section contains explicit note that zero-token tiebreak is index-only and score is irrelevant
- [ ] `greedy.md` Conformance Notes contains a bullet about zero-token tiebreaking by index only
- [ ] `knapsack.md` Score Scaling subsection contains the floor/truncation-toward-zero equivalence note
- [ ] `u-shaped.md` Edge Cases table "Pinned items" row corrected to clarify no special UShapedPlacer logic
- [ ] `composite.md` `CONSTRUCT-COMPOSITE` pseudocode ends with two explicit `self.scorers` and `self.normalizedWeights` assignment lines (comment removed)
- [ ] `scaled.md` Performance note warns about nested ScaledScorer O(N^(depth+1)) cost
- [ ] `kind.md` pseudocode comment updated to reference ContextKind equality definition; prose note added

## Verification

- `grep -n "Placed at edges alongside other high-scored items" spec/src/placers/u-shaped.md` → 0
- `grep -n "// Store scorers and normalizedWeights" spec/src/scorers/composite.md` → 0
- `grep -n "self.scorers" spec/src/scorers/composite.md` → at least 1 match
- `grep -n "self.normalizedWeights" spec/src/scorers/composite.md` → at least 1 match
- `grep -n "depth\|nested\|compound" spec/src/scorers/scaled.md` → at least 1 match (nesting warning)
- `grep -n "index only\|index-only" spec/src/slicers/greedy.md` → at least 1 match (zero-token tiebreak note)
- `grep -n "enumerations.md\|ContextKind equality" spec/src/scorers/kind.md` → at least 1 match (case-insensitivity clarification)

## Observability Impact

- Signals added/changed: None (spec-only changes)
- How a future agent inspects this: Read target sections in each file and verify the text matches the expected content
- Failure state exposed: None

## Inputs

- `spec/src/slicers/greedy.md` — current content (Value Density section is the target)
- `spec/src/slicers/knapsack.md` — current content (Score Scaling subsection is the target)
- `spec/src/placers/u-shaped.md` — current content (Edge Cases table row is the target)
- `spec/src/scorers/composite.md` — current content (CONSTRUCT-COMPOSITE pseudocode closing lines are the target)
- `spec/src/scorers/scaled.md` — current content (Performance note is the target)
- `spec/src/scorers/kind.md` — current content (pseudocode comment and surrounding prose are the target)
- Research pitfall: do NOT modify the u-shaped.md Conformance Notes or the paragraph "Pinned items arrive with score 1.0 (see Stage 6: Place)..." — that paragraph is correct and should be preserved; only the Edge Cases table row changes

## Expected Output

- `spec/src/slicers/greedy.md` — zero-token tiebreak note in Value Density + Conformance Notes
- `spec/src/slicers/knapsack.md` — floor/truncation equivalence note in Score Scaling
- `spec/src/placers/u-shaped.md` — corrected Pinned items Edge Cases table row
- `spec/src/scorers/composite.md` — two explicit assignment lines replacing the comment
- `spec/src/scorers/scaled.md` — nesting depth performance warning appended
- `spec/src/scorers/kind.md` — case-insensitivity source clarification in pseudocode comment and prose
