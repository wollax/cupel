# S02: Spec Editorial Debt

**Goal:** Close all 20 open spec/phase24 editorial issues by making targeted prose, pseudocode, and table edits across 10 spec markdown files and one TOML conformance vector; delete all resolved issue files from `.planning/issues/open/`.

**Demo:** All 20 issue files removed from `.planning/issues/open/` (the `spec-workflow-checksum-verification.md` file deferred — kept open per research); the spec correctly specifies event ordering, item_count sentinel semantics, observer callback normative status, greedy zero-token tiebreak, knapsack floor/truncation equivalence, UShapedPlacer pinned row, CompositeScorer pseudocode storage, ScaledScorer nesting warning, KindScorer case-insensitivity source, context-item normative alignment, conformance format QuotaSlice note, and TOML density comment clarity; `cargo test` and `dotnet test` pass.

## Must-Haves

- `spec/src/diagnostics/events.md` — event ordering note added to Conformance Notes; `item_count` sentinel note added; duplicate "Rejected alternative" Sort block removed
- `spec/src/diagnostics/trace-collector.md` — Conformance Notes moved before Observer Callback; Observer Callback labeled "MAY (Optional)"; null-path guarantee prose replaced with cross-reference to `diagnostics.md`
- `spec/src/diagnostics/selection-report.md` — SelectionReport Fields table moved before "How to Obtain"; `Deduplicated` rationale in ExcludedItem replaced with back-reference to `exclusion-reasons.md`; inline-italic rejected alternative in Conformance Notes converted to bold standalone paragraph
- `spec/src/diagnostics/exclusion-reasons.md` — JSON examples added for the 4 reserved variants: `ScoredTooLow`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `Filtered`
- `spec/src/diagnostics.md` — "Defined in" column header renamed to "Spec page"
- `spec/src/slicers/greedy.md` — note added that among zero-token items score is irrelevant and sort tiebreak is index only
- `spec/src/slicers/knapsack.md` — note added that `floor` and truncation-toward-zero are equivalent for non-negative scores
- `spec/src/placers/u-shaped.md` — "Pinned items" edge case row corrected to clarify UShapedPlacer has no special pinned logic
- `spec/src/scorers/composite.md` — `CONSTRUCT-COMPOSITE` pseudocode `// Store scorers and normalizedWeights` comment replaced with two explicit assignment lines
- `spec/src/scorers/scaled.md` — nesting depth warning added (O(N^(depth+1)) compound cost; prefer flat CompositeScorer)
- `spec/src/scorers/kind.md` — pseudocode comment clarified: case-insensitivity is a ContextKind equality property, not ad-hoc dictionary behavior
- `spec/src/data-model/context-item.md` — `content` field table cell demoted to plain description; Constraint 1 remains the normative source
- `spec/src/conformance/format.md` — clarifying sentence added to Set Comparison note: applies to all slicers including QuotaSlice
- Both copies of `greedy-chronological.toml` — `jan` density comment updated to clarify `0.0` is the normal non-zero-token path
- 20 issue files deleted from `.planning/issues/open/` (all `spec-*` and `phase24-*` files; `spec-workflow-checksum-verification.md` deferred — kept open)
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes

## Proof Level

- This slice proves: contract (spec chapter completeness and internal consistency)
- Real runtime required: no (spec/doc edits only; tests confirm no regressions)
- Human/UAT required: yes (human review of spec clarity is part of the milestone DoD; automated checks confirm the mechanics only)

## Verification

- `ls .planning/issues/open/ | grep -E '^2026-03-1[45]-(spec-|phase24-)' | wc -l` → 0 (all closed; `spec-workflow-checksum-verification.md` is the one intentionally deferred and has a different prefix pattern — confirm it remains)
- `grep -n "Defined in" spec/src/diagnostics.md` → no match (column renamed to "Spec page")
- `grep -n "// Store scorers and normalizedWeights" spec/src/scorers/composite.md` → no match (replaced with assignment lines)
- `grep -n "Placed at edges alongside other high-scored items" spec/src/placers/u-shaped.md` → no match (row corrected)
- `cargo test --manifest-path crates/cupel/Cargo.toml` → exit 0
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → exit 0
- Diff of both `greedy-chronological.toml` files confirms they are identical (drift guard)

## Observability / Diagnostics

- Runtime signals: none (no runtime components modified)
- Inspection surfaces: `ls .planning/issues/open/ | grep -E 'spec-|phase24-'` — should return empty after T03
- Failure visibility: file diff against expected text in each spec section; `cargo test` / `dotnet test` exit codes
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `.planning/issues/open/` (20 issue files); `spec/src/` (10 markdown files); `spec/conformance/required/pipeline/greedy-chronological.toml`; `crates/cupel/conformance/required/pipeline/greedy-chronological.toml`
- New wiring introduced in this slice: none (editorial only)
- What remains before the milestone is truly usable end-to-end: S03 (count-quota design), S04 (metadata convention), S05 (testing vocabulary), S06 (future features chapters)

## Tasks

- [x] **T01: Fix diagnostics spec files** `est:45m`
  - Why: Five diagnostics files carry the bulk of the phase24 editorial issues (event ordering, item_count sentinel, duplicate Sort block, observer callback labeling, Conformance Notes placement, null-path prose duplication, selection-report section reorder, cross-reference replacements, rejected-alternative formatting, JSON examples for reserved variants, summary table column header)
  - Files: `spec/src/diagnostics/events.md`, `spec/src/diagnostics/trace-collector.md`, `spec/src/diagnostics/selection-report.md`, `spec/src/diagnostics/exclusion-reasons.md`, `spec/src/diagnostics.md`
  - Do: Apply all 11 editorial changes across the 5 files in file order. Key constraints: (1) in `events.md` remove only the "Rejected alternative" Sort block, keep the "Sort is omitted" paragraph; add event ordering note to Conformance Notes as an addendum to the existing "emitted after the stage completes" sentence; add item_count sentinel note to the `item_count` field description. (2) in `trace-collector.md` move Conformance Notes section before Observer Callback; add "(Optional — MAY)" qualifier to Observer Callback heading; replace the null-path guarantee paragraph body with a cross-reference to `diagnostics.md`. (3) in `selection-report.md` move the SelectionReport Fields table + header before the "How to Obtain" section; replace the inline `Deduplicated`/`ExcludedItem` rationale block referencing exclusion-reasons with a back-reference sentence; convert the inline-italic "Rejected alternative" at end of Conformance Notes to a bold standalone paragraph. (4) in `exclusion-reasons.md` add JSON examples after the table for `ScoredTooLow`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `Filtered`. (5) in `diagnostics.md` rename "Defined in" column header to "Spec page".
  - Verify: `grep -n "Rejected alternative:" spec/src/diagnostics/events.md` → zero results for the Sort rejected-alternative block; `grep -n "Defined in" spec/src/diagnostics.md` → 0; each modified file opens cleanly in a text editor with correct section order
  - Done when: All 11 changes applied; each file's structure matches the target described in the research; no stale text remains from the issues

- [x] **T02: Fix algorithm spec files — slicers, placers, scorers** `est:30m`
  - Why: Six algorithm files have targeted single-issue fixes — zero-token tiebreak note, floor/truncation equivalence, UShapedPlacer pinned row, CompositeScorer pseudocode completion, ScaledScorer nesting warning, KindScorer case-insensitivity clarification
  - Files: `spec/src/slicers/greedy.md`, `spec/src/slicers/knapsack.md`, `spec/src/placers/u-shaped.md`, `spec/src/scorers/composite.md`, `spec/src/scorers/scaled.md`, `spec/src/scorers/kind.md`
  - Do: (1) `greedy.md` — in Value Density section (or Conformance Notes), add a note: among zero-token items, all have density MAX_FLOAT; the sort tiebreak is index only; the score value is irrelevant. (2) `knapsack.md` — in Discretization > Score Scaling, add a note after the `floor(score * 10000)` line that `floor` and truncation-toward-zero (C-style integer truncation) are equivalent for non-negative scores (which all scores are), so either operation is conformant. (3) `u-shaped.md` — replace the "Pinned items (score 1.0)" edge case table row cell "Placed at edges alongside other high-scored items." with the corrected text: "Not special-cased by UShapedPlacer; pinned items arrive with score 1.0 from the pipeline and naturally rank highly, sorting to edges." (4) `composite.md` — replace the `// Store scorers and normalizedWeights` comment at end of CONSTRUCT-COMPOSITE with two explicit assignment lines: `self.scorers <- [entries[i].scorer for i in 0..length(entries)]` and `self.normalizedWeights <- normalizedWeights`. (5) `scaled.md` — add a warning in the Performance note: deeply nested ScaledScorer wrapping another ScaledScorer compounds the cost to O(N^(depth+1)); callers should prefer a flat CompositeScorer structure. (6) `kind.md` — in the pseudocode comment `// case-insensitive lookup`, replace with a note that case-insensitivity is a property of ContextKind equality (as defined in enumerations.md), not an ad-hoc dictionary feature; add a cross-reference.
  - Verify: `grep -n "Placed at edges alongside other high-scored items" spec/src/placers/u-shaped.md` → 0; `grep -n "// Store scorers and normalizedWeights" spec/src/scorers/composite.md` → 0; both greedy.md Conformance Notes and Value Density section contain the zero-token tiebreak note
  - Done when: All 6 algorithm file changes applied with correct wording per research

- [x] **T03: Fix data-model, conformance format, TOML; close issues; verify** `est:30m`
  - Why: Three remaining editorial targets (context-item normative alignment, format.md set-comparison clarification, TOML density comment), then delete all resolved issue files and run the test suites to confirm no regressions
  - Files: `spec/src/data-model/context-item.md`, `spec/src/conformance/format.md`, `spec/conformance/required/pipeline/greedy-chronological.toml`, `crates/cupel/conformance/required/pipeline/greedy-chronological.toml`, `.planning/issues/open/` (20 deletions)
  - Do: (1) `context-item.md` — change the `content` field table cell from "The textual content of this context item. Must be non-null and non-empty." to "The textual content of the item. Non-null and non-empty." (plain description, no informal MUST); verify Constraint 1 is unchanged and still normative. (2) `format.md` — in the Slicing Vectors > Set Comparison subsection, add a clarifying sentence after the existing note: "This applies to all slicers including QuotaSlice — ordering is always the placer's responsibility, not the slicer's." (3) Both copies of `greedy-chronological.toml` — update the `jan` density comment from `jan=0.0/200=0.0` to clarify this is the normal non-zero-token path (e.g., `jan=0.0/200=0.0 (non-zero token, normal density path)`); apply the identical change to both `spec/conformance/` and `crates/cupel/conformance/` per D007 drift guard. (4) Delete all 20 resolved issue files from `.planning/issues/open/`: `2026-03-14-spec-composite-pseudocode-storage.md`, `2026-03-14-spec-context-item-normative-inconsistency.md`, `2026-03-14-spec-greedy-zero-token-ordering.md`, `2026-03-14-spec-kindscorer-case-insensitivity-clarification.md`, `2026-03-14-spec-knapsack-floor-vs-truncation.md`, `2026-03-14-spec-pipeline-density-comment-clarity.md`, `2026-03-14-spec-slicer-set-comparison-clarification.md`, `2026-03-14-spec-ushaped-pinned-edge-case.md`, `2026-03-15-phase24-conformance-notes-placement.md`, `2026-03-15-phase24-event-ordering-within-stage-unspecified.md`, `2026-03-15-phase24-excluded-item-rationale-repeats-exclusion-reasons.md`, `2026-03-15-phase24-how-to-obtain-placement.md`, `2026-03-15-phase24-item-count-sentinel-ambiguity.md`, `2026-03-15-phase24-null-path-prose-duplicated.md`, `2026-03-15-phase24-observer-callback-normative-status.md`, `2026-03-15-phase24-pipeline-stage-sort-omission-redundancy.md`, `2026-03-15-phase24-rejected-alternative-formatting-inconsistency.md`, `2026-03-15-phase24-reserved-variants-no-json-example.md`, `2026-03-15-phase24-summary-table-column-header.md`, and `2026-03-14-unbounded-scaled-nesting-depth.md`. Keep `2026-03-14-spec-workflow-checksum-verification.md` open (deferred per research). (5) Run `cargo test` and `dotnet test`; confirm both pass.
  - Verify: `ls .planning/issues/open/ | grep -E '^2026-03-1[45]-(spec-|phase24-)' | wc -l` → 0; `diff spec/conformance/required/pipeline/greedy-chronological.toml crates/cupel/conformance/required/pipeline/greedy-chronological.toml` → no diff; `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0
  - Done when: All 20 issue files deleted (workflow checksum issue remains open), both test suites green, both TOML copies identical

## Files Likely Touched

- `spec/src/diagnostics/events.md`
- `spec/src/diagnostics/trace-collector.md`
- `spec/src/diagnostics/selection-report.md`
- `spec/src/diagnostics/exclusion-reasons.md`
- `spec/src/diagnostics.md`
- `spec/src/slicers/greedy.md`
- `spec/src/slicers/knapsack.md`
- `spec/src/placers/u-shaped.md`
- `spec/src/scorers/composite.md`
- `spec/src/scorers/scaled.md`
- `spec/src/scorers/kind.md`
- `spec/src/data-model/context-item.md`
- `spec/src/conformance/format.md`
- `spec/conformance/required/pipeline/greedy-chronological.toml`
- `crates/cupel/conformance/required/pipeline/greedy-chronological.toml`
- `.planning/issues/open/` (20 deletions)
