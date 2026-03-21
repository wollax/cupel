---
id: S02
parent: M002
milestone: M002
provides:
  - All 20 spec/phase24 editorial issues closed (issue files deleted from .planning/issues/open/)
  - 11 diagnostics spec changes (events ordering, item_count sentinel, section reorder, MAY labeling, null-path cross-reference, JSON examples, column rename)
  - 6 algorithm spec changes (zero-token tiebreak, floor/truncation equivalence, UShapedPlacer pinned row, composite pseudocode, scaled nesting warning, kind case-insensitivity)
  - 3 remaining editorial changes (context-item normative alignment, format.md QuotaSlice note, TOML density comment)
  - Drift guard satisfied: both greedy-chronological.toml copies identical
requires: []
affects:
  - S03
  - S04
  - S05
  - S06
key_files:
  - spec/src/diagnostics/events.md
  - spec/src/diagnostics/trace-collector.md
  - spec/src/diagnostics/selection-report.md
  - spec/src/diagnostics/exclusion-reasons.md
  - spec/src/diagnostics.md
  - spec/src/slicers/greedy.md
  - spec/src/slicers/knapsack.md
  - spec/src/placers/u-shaped.md
  - spec/src/scorers/composite.md
  - spec/src/scorers/scaled.md
  - spec/src/scorers/kind.md
  - spec/src/data-model/context-item.md
  - spec/src/conformance/format.md
  - spec/conformance/required/pipeline/greedy-chronological.toml
  - crates/cupel/conformance/required/pipeline/greedy-chronological.toml
key_decisions:
  - none
patterns_established:
  - none
observability_surfaces:
  - none
drill_down_paths:
  - .kata/milestones/M002/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M002/slices/S02/tasks/T02-SUMMARY.md
  - .kata/milestones/M002/slices/S02/tasks/T03-SUMMARY.md
duration: ~1 hour (3 tasks)
verification_result: passed
completed_at: 2026-03-21
---

# S02: Spec Editorial Debt

**Closed all 20 spec/phase24 editorial issues: 20 issue files deleted, 13 spec files updated with precise ordering rules, normative alignment, algorithm notes, and reserved variant examples; both test suites green.**

## What Happened

Three tasks executed in sequence across 13 spec/doc files and two TOML conformance vectors:

**T01 (Diagnostics spec files — 11 changes):** Fixed the bulk of the phase24 editorial debt concentrated in the diagnostics chapter. Key changes: added item-level event ordering MUST rule to `events.md` Conformance Notes; added `item_count` sentinel note to the field table; removed the duplicate "Rejected alternative: Including Sort for completeness" block. In `trace-collector.md`, moved Conformance Notes before Observer Callback and labeled the callback section "(Optional — MAY)"; replaced the null-path guarantee paragraph with a cross-reference to `diagnostics.md#null-path-guarantee`. In `selection-report.md`, reordered sections (SelectionReport Fields before How to Obtain), replaced inline ExcludedItem rationale with a back-reference to `exclusion-reasons.md`, and converted an inline italic "Rejected alternative" to a bold standalone paragraph. Added JSON examples for all 4 reserved `ExclusionReason` variants in `exclusion-reasons.md`. Renamed the "Defined in" column header to "Spec page" in the `diagnostics.md` summary table.

**T02 (Algorithm spec files — 6 changes):** Applied targeted single-issue fixes to slicer, placer, and scorer files. In `greedy.md`, added a note that among zero-token items the sort tiebreak is index only and score values are irrelevant; added a matching Conformance Notes bullet with a MUST NOT. In `knapsack.md`, added an implementation note that `floor` and C-style truncation-toward-zero are equivalent for non-negative scores. In `u-shaped.md`, corrected the "Pinned items (score 1.0)" Edge Cases row — UShapedPlacer has no special pinned logic; they naturally rank highly from score 1.0. In `composite.md`, replaced `// Store scorers and normalizedWeights` with two explicit `self.scorers` and `self.normalizedWeights` assignment lines. In `scaled.md`, appended a nesting depth warning (O(N^(depth+1)) compound cost; prefer flat CompositeScorer). In `kind.md`, changed the pseudocode comment to reference `ContextKind` equality as defined in `enumerations.md` and added a prose note below the block.

**T03 (Data-model, conformance, TOML, issue deletions):** Demoted the `content` field table cell in `context-item.md` from informal MUST to plain description (Constraint 1 normative source unchanged). Added the QuotaSlice clarifying sentence to the Set Comparison note in `format.md`. Updated the `jan` density comment in both copies of `greedy-chronological.toml` to append `(non-zero token, normal score path)` — D007 drift guard satisfied. Deleted all 20 resolved issue files from `.planning/issues/open/`; kept `2026-03-14-spec-workflow-checksum-verification.md` (deferred — CI security concern, out of S02 scope).

## Verification

All slice-level checks passed:

- `ls .planning/issues/open/ | grep -E '^2026-03-1[45]-(spec-|phase24-)' | wc -l` → 1 (the one remaining file is the intentionally deferred `spec-workflow-checksum-verification.md`; all 20 listed issue files deleted) ✓
- `grep -n "Defined in" spec/src/diagnostics.md` → 0 matches ✓
- `grep -n "// Store scorers and normalizedWeights" spec/src/scorers/composite.md` → 0 matches ✓
- `grep -n "Placed at edges alongside other high-scored items" spec/src/placers/u-shaped.md` → 0 matches ✓
- `diff spec/conformance/required/pipeline/greedy-chronological.toml crates/cupel/conformance/required/pipeline/greedy-chronological.toml` → no output ✓
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 35 passed (+ doctest), 1 ignored, exit 0 ✓
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed, exit 0 ✓

## Requirements Advanced

- R041 — Spec quality debt closure: all 20 tracked editorial issues closed; all 13 spec files updated with correct ordering rules, normative alignment, algorithm clarifications, and reserved variant examples.

## Requirements Validated

- R041 — All ~8-10 originally scoped issues (expanded to 20 actual issues) are resolved. Issue files deleted. No ambiguous ordering guarantees, misleading algorithm descriptions, or unspecified normative status remain in the targeted sections. Both test suites pass, confirming no regressions from spec-only edits.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

The slice plan described `2026-03-14-spec-workflow-checksum-verification.md` as having "a different prefix pattern" that wouldn't match the `^2026-03-1[45]-(spec-|phase24-)` grep. This is incorrect — it does match. However, the actual requirement (keep the deferred file, delete the 20 listed files) is fully satisfied. The wc count of 1 reflects this one retained file, which is correct behavior.

The `rtk dotnet test` wrapper incorrectly reported exit 1 during T03 due to TUnit secondary runner output; raw `dotnet test` confirmed exit 0.

## Known Limitations

- `spec-workflow-checksum-verification.md` remains open — the CI checksum verification issue is a legitimate concern but requires separate security design work outside S02 scope.

## Follow-ups

- none (all deferred items are tracked in `.planning/issues/open/` or in later M002 slices)

## Files Created/Modified

- `spec/src/diagnostics/events.md` — item_count sentinel note; item-level ordering MUST rule; Sort rejected-alternative block removed
- `spec/src/diagnostics/trace-collector.md` — section reorder; Observer Callback labeled (Optional — MAY); null-path cross-reference
- `spec/src/diagnostics/selection-report.md` — SelectionReport Fields before How to Obtain; ExcludedItem cross-reference; bold Rejected alternative in Conformance Notes
- `spec/src/diagnostics/exclusion-reasons.md` — JSON examples for 4 reserved variants
- `spec/src/diagnostics.md` — Summary table "Spec page" column header
- `spec/src/slicers/greedy.md` — zero-token tiebreak note in Value Density + Conformance Notes MUST NOT bullet
- `spec/src/slicers/knapsack.md` — floor/truncation equivalence note in Score Scaling subsection
- `spec/src/placers/u-shaped.md` — corrected Pinned items Edge Cases table row
- `spec/src/scorers/composite.md` — two explicit assignment lines replacing the comment
- `spec/src/scorers/scaled.md` — nesting depth performance warning appended
- `spec/src/scorers/kind.md` — case-insensitivity source clarification in pseudocode comment and prose
- `spec/src/data-model/context-item.md` — content field table cell demoted to plain description
- `spec/src/conformance/format.md` — QuotaSlice clarifying sentence added to Set Comparison
- `spec/conformance/required/pipeline/greedy-chronological.toml` — jan density comment updated
- `crates/cupel/conformance/required/pipeline/greedy-chronological.toml` — identical jan density comment update (drift guard)
- `.planning/issues/open/` — 20 resolved issue files deleted

## Forward Intelligence

### What the next slice should know
- The spec is now internally consistent on ordering guarantees, normative status, and algorithm descriptions. Downstream slices (S03–S06) can write new chapters without inheriting the editorial patterns that generated this debt — avoid informal MUST in table cells, label optional behaviors with MAY explicitly, and keep pseudocode assignment-complete.
- The `spec-workflow-checksum-verification.md` issue is still open — S03–S06 should not assume CI conformance vector drift detection is operating at maximum fidelity.

### What's fragile
- TOML drift guard — both conformance vector copies must stay identical; any future TOML edit must be applied to both `spec/conformance/` and `crates/cupel/conformance/` manually or the CI drift check will catch it.

### Authoritative diagnostics
- `ls .planning/issues/open/ | grep -E 'spec-|phase24-'` — single source of truth for remaining spec editorial debt; currently returns only the deferred checksum issue.

### What assumptions changed
- Slice plan assumed 20 issue files total with the checksum file having a different prefix — the actual count matched, but the checksum file shares the same prefix pattern. This doesn't affect correctness; the verification check must be read as "1 = the one intentionally deferred file."
