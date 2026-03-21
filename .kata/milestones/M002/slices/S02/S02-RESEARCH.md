# S02: Spec Editorial Debt — Research

**Date:** 2026-03-21
**Slice:** M002/S02

## Summary

S02 is a pure editorial pass over the Cupel spec. It closes all 20 open `spec`- and `phase24`-prefixed issues in `.planning/issues/open/`. No code changes, no conformance vector updates — every issue resolves to a targeted prose or pseudocode edit in `spec/src/`. The 8 core R041 items (event ordering, item_count sentinel, observer callback normative label, greedy zero-token note, knapsack floor/truncation note, UShapedPlacer pinned edge row, composite pseudocode assignment, ScaledScorer nesting warning) map one-to-one to specific lines in the spec. The remaining 12 issues are structural/formatting clean-ups in the same diagnostic chapter files.

The only non-trivial judgment call is the `content` field normative inconsistency in `context-item.md` (MUST in field table vs SHOULD in Constraints section). All other edits are unambiguous additions or removals with a clear target location.

No existing conformance vectors assert event ordering within a stage — the diagnostic test vectors check `expected.diagnostics.summary`, `expected.diagnostics.included`, and `expected.diagnostics.excluded`, not the `events` list order. Adding the event ordering rule is therefore pure spec addition with no vector side-effects.

## Recommendation

Apply all 20 issues in a single editing pass, grouped by file. Execute in this order to avoid repeated file switches: `diagnostics/events.md` → `diagnostics/trace-collector.md` → `diagnostics/selection-report.md` → `diagnostics/exclusion-reasons.md` → `diagnostics.md` → `slicers/greedy.md` → `slicers/knapsack.md` → `placers/u-shaped.md` → `scorers/composite.md` → `scorers/scaled.md` → `scorers/kind.md` → `data-model/context-item.md` → `conformance/format.md` → conformance TOML vector. After all edits, delete all 20 issue files from `.planning/issues/open/` (plus optionally the workflow security issue).

For the `content` normative inconsistency: the correct fix is to demote the field-table cell from normative-looking prose to a plain description (e.g., "The textual content of the item; must not be null or empty"), leaving the Constraints section as the canonical normative source using SHOULD. This avoids a spec tightening that would force implementations to error on empty content when the .NET implementation currently only enforces non-null.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Locating issue bodies | Already read above | No discovery needed |
| Finding spec file locations | `spec/src/` directory already mapped | All target files identified |
| Checking conformance vector format | `spec/conformance/required/pipeline/*.toml` | Confirms no vector changes needed |

## Existing Code and Patterns

- `spec/src/diagnostics/events.md` — contains `PipelineStage` table, `TraceEvent` field table, Conformance Notes. Needs: (1) event ordering rule in Conformance Notes ("item-level events for a stage MUST precede the stage-level event"); (2) `item_count` field description note that value `1` on item-level events is a sentinel with no aggregate meaning.
- `spec/src/diagnostics/trace-collector.md` — four sections: Contract, NullTraceCollector, DiagnosticTraceCollector, TraceDetailLevel, Observer Callback, Conformance Notes. Needs: (1) add "MAY" label to Observer Callback section heading or first sentence; (2) move Conformance Notes section before Observer Callback; (3) replace null-path guarantee prose with cross-reference to `diagnostics.md`.
- `spec/src/diagnostics/selection-report.md` — sections: "How to Obtain", SelectionReport Fields, IncludedItem, ExcludedItem, Conformance Notes. Needs: (1) move fields table before "How to Obtain"; (2) replace inline Deduplicated rationale block with back-reference to `exclusion-reasons.md`; (3) convert inline-italics rejected alternative in Conformance Notes to bold "Rejected alternative:" standalone paragraph.
- `spec/src/diagnostics/exclusion-reasons.md` — needs JSON examples for the 4 multi-field reserved variants: `ScoredTooLow`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `Filtered`.
- `spec/src/diagnostics.md` — summary table has "Defined in" column header; rename to "Spec page" or "Chapter".
- `spec/src/slicers/greedy.md` — zero-token items: spec notes density is MAX_FLOAT but doesn't clarify that among zero-token items, sort tiebreak is index only (score is irrelevant). Add explicit note in Value Density section or Conformance Notes.
- `spec/src/slicers/knapsack.md` — Step 2 uses `floor(score * 10000)`; needs note in Discretization > Score Scaling subsection that `floor` and `truncation-toward-zero` are equivalent for non-negative scores, so either is conformant.
- `spec/src/placers/u-shaped.md` — Edge Cases table row for "Pinned items (score 1.0)" currently says "Placed at edges alongside other high-scored items." This misleadingly implies UShapedPlacer has special pinned logic. Correct to: "Not special-cased by UShapedPlacer; pinned items arrive with score 1.0 from the pipeline and naturally rank highly, sorting to edges."
- `spec/src/scorers/composite.md` — CONSTRUCT-COMPOSITE pseudocode ends with `// Store scorers and normalizedWeights` comment. Replace with two explicit assignment lines to make the pseudocode complete.
- `spec/src/scorers/scaled.md` — Performance note currently warns about O(N²) per full pass but says nothing about nesting depth. Add a warning: deeply nested `ScaledScorer` wrapping another `ScaledScorer` compounds the cost to O(N^(depth+1)); callers should prefer a flat `CompositeScorer` structure.
- `spec/src/scorers/kind.md` — Algorithm pseudocode comment says "case-insensitive lookup" implying the dictionary is configured with case-insensitive keys. Clarify: case-insensitivity is a property of `ContextKind` equality (defined in `enumerations.md`), not an ad-hoc dictionary feature.
- `spec/src/data-model/context-item.md` — field table cell for `content` says "Must be non-null and non-empty" (informal MUST) while Constraint 1 says "MUST be a non-null, non-empty string. Implementations SHOULD reject construction..." — inconsistent normative levels. Fix: change the field table cell to plain description, e.g., "The textual content of the item. Non-null and non-empty."; leave Constraint 1 as the normative source.
- `spec/src/conformance/format.md` — Slicing Vectors > Set Comparison note exists but only references the slicer section. Add a clarifying sentence: "This applies to all slicers including QuotaSlice — ordering is always the placer's responsibility, not the slicer's."
- `spec/conformance/required/pipeline/greedy-chronological.toml` — comment for jan's density says `density = 0.0/200 = 0.0` without clarifying this is the normal path (only zero tokens triggers MAX_FLOAT). Update comment to make the normal path explicit.
- `spec/src/diagnostics/events.md` — "Sort is omitted" rationale block + adjacent "Rejected alternative" block cover the same ground. Remove the "Rejected alternative" block; keep the "Sort is omitted" rationale.

## Constraints

- **No code changes in M002.** All edits are to `spec/src/**/*.md` and `spec/conformance/**/*.toml` only.
- **Conformance drift guard applies to TOML changes.** If any conformance TOML in `spec/conformance/` is modified, the same change must be applied to `crates/cupel/conformance/` simultaneously (D007 pattern).
- **The greedy-chronological.toml density comment fix** is a comment change only — `expected_output` is unchanged, so both copies must be updated but no test logic changes.
- **No new spec chapters** are introduced in S02. S02 is editorial only; new chapters come in S04, S05, S06.
- **Do not modify `.github/workflows/spec.yml`** for the checksum issue — that is a CI security concern, not a spec editorial issue. It can be batched with future CI work or left open.

## Common Pitfalls

- **Editing `trace-collector.md` conformance notes placement** — after moving the Conformance Notes section before the Observer Callback section, verify the resulting file structure makes logical sense: Contract → NullTraceCollector → DiagnosticTraceCollector → TraceDetailLevel → Conformance Notes → Observer Callback (optional/non-normative).
- **ScaledScorer nesting warning** — the `2026-03-14-unbounded-scaled-nesting-depth.md` issue references `.NET` source file `ScorerEntry.cs`. For S02, only `spec/src/scorers/scaled.md` is changed (no code). The .NET source doc update is out of scope (M002 is design-only, D039).
- **phase24-event-ordering-within-stage-unspecified** — the correct ordering rule is: item-level events for a stage MUST precede the corresponding stage-level event (items are recorded as they are processed; the stage event is emitted after all items finish). This matches the natural implementation flow. The Conformance Notes in `events.md` already say "emitted after the stage completes" for stage-level events — the item-level ordering rule is an addendum to that existing sentence.
- **Removing duplicate Sort rationale block** — the `events.md` file has both a "Sort is omitted" rationale paragraph and an immediately adjacent "Rejected alternative:" block. Remove only the "Rejected alternative:" block (which repeats the rationale); keep the primary "Sort is omitted" paragraph.
- **greedy-chronological.toml drift guard** — this file lives in `spec/conformance/`. The parallel copy is `crates/cupel/conformance/required/pipeline/greedy-chronological.toml`. Both must be updated.

## Open Risks

- **Event ordering rule affects future conformance vectors.** No existing diagnostic vectors assert event ordering, so no changes are required now. However, when S02 adds the normative "item-level events MUST precede stage-level events" rule, any future conformance vectors that test the `events` list must be authored to match. Note this explicitly in the spec change so implementors know.
- **context-item.md normative level fix** — the chosen fix (demote the field table to non-normative description) must not accidentally weaken the invariant. Constraint 1 must remain as the authoritative source and be clearly normative (SHOULD). Verify the Constraints section still reads as the primary normative definition after the field table is simplified.
- **Observer Callback section labeling** — the issue asks to add "MAY" language; the existing spec already says "Implementations may support..." (lowercase may, which is non-normative per RFC 2119). The fix should either capitalize to MAY or add an explicit "(Optional)" heading qualifier to make the non-normative status unambiguous. Do not accidentally elevate to MUST or SHOULD.

## Issue-to-File Map (complete)

| Issue file | Target spec file | Change type |
|---|---|---|
| `spec-composite-pseudocode-storage.md` | `scorers/composite.md` | Pseudocode addition |
| `spec-context-item-normative-inconsistency.md` | `data-model/context-item.md` | Normative level alignment |
| `spec-greedy-zero-token-ordering.md` | `slicers/greedy.md` | Clarifying note |
| `spec-kindscorer-case-insensitivity-clarification.md` | `scorers/kind.md` | Source clarification |
| `spec-knapsack-floor-vs-truncation.md` | `slicers/knapsack.md` | Equivalence note |
| `spec-pipeline-density-comment-clarity.md` | `spec/conformance/…/greedy-chronological.toml` (both copies) | Comment update |
| `spec-slicer-set-comparison-clarification.md` | `conformance/format.md` | Clarifying sentence |
| `spec-ushaped-pinned-edge-case.md` | `placers/u-shaped.md` | Table row correction |
| `spec-workflow-checksum-verification.md` | `.github/workflows/spec.yml` | **Out of S02 scope** — CI security, not spec editorial |
| `phase24-conformance-notes-placement.md` | `diagnostics/trace-collector.md` | Section reorder |
| `phase24-event-ordering-within-stage-unspecified.md` | `diagnostics/events.md` | New conformance note |
| `phase24-excluded-item-rationale-repeats-exclusion-reasons.md` | `diagnostics/selection-report.md` | Cross-reference replacement |
| `phase24-how-to-obtain-placement.md` | `diagnostics/selection-report.md` | Section reorder |
| `phase24-item-count-sentinel-ambiguity.md` | `diagnostics/events.md` | Sentinel note |
| `phase24-null-path-prose-duplicated.md` | `diagnostics/trace-collector.md` | Cross-reference replacement |
| `phase24-observer-callback-normative-status.md` | `diagnostics/trace-collector.md` | MAY label addition |
| `phase24-pipeline-stage-sort-omission-redundancy.md` | `diagnostics/events.md` | Remove duplicate block |
| `phase24-rejected-alternative-formatting-inconsistency.md` | `diagnostics/selection-report.md` | Formatting fix |
| `phase24-reserved-variants-no-json-example.md` | `diagnostics/exclusion-reasons.md` | Add JSON examples |
| `phase24-summary-table-column-header.md` | `diagnostics.md` | Column header rename |
| `2026-03-14-unbounded-scaled-nesting-depth.md` | `scorers/scaled.md` | Performance warning addition |

(21 issue files → 20 closed from `.planning/issues/open/`; spec-workflow-checksum-verification deferred)

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| mdBook markdown | none required | n/a — plain markdown editing |

## Sources

- All issue bodies read directly from `.planning/issues/open/` (20 files)
- All target spec files read from `spec/src/` (10 files)
- Conformance vector format verified from `spec/conformance/required/pipeline/diagnostics-budget-exceeded.toml` — confirms no vector assertions on event ordering
- `spec/conformance/required/pipeline/greedy-chronological.toml` — confirms density comment location for the TOML comment fix
