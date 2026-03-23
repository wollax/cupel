# S02: Spec Editorial Debt — UAT

**Milestone:** M002
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S02 produces only spec/doc edits and issue file deletions — there are no runtime components, APIs, or user-facing behaviors to exercise. Correctness is verified by inspecting the artifacts (spec markdown, TOML files, issue file list) and confirming no test regressions.

## Preconditions

- `cargo test --manifest-path crates/cupel/Cargo.toml` passes (confirms no unintended code changes)
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes (same)
- All 20 listed issue files deleted from `.planning/issues/open/`

## Smoke Test

Run `ls .planning/issues/open/ | grep -E 'spec-|phase24-'` — should return only `2026-03-14-spec-workflow-checksum-verification.md` (the one intentionally deferred issue). Any additional matches indicate a missed deletion.

## Test Cases

### 1. Event ordering rule present in events.md

1. Open `spec/src/diagnostics/events.md`.
2. Locate the Conformance Notes section.
3. **Expected:** A bullet stating that item-level `BeforeScore`/`AfterScore` events MUST precede the corresponding `BeforeStage`/`AfterStage` events for the same pipeline stage.

### 2. Observer Callback labeled MAY

1. Open `spec/src/diagnostics/trace-collector.md`.
2. Locate the Observer Callback section heading.
3. **Expected:** Heading reads "Observer Callback (Optional — MAY)" and Conformance Notes section appears before the Observer Callback section in document order.

### 3. JSON examples for reserved ExclusionReason variants

1. Open `spec/src/diagnostics/exclusion-reasons.md`.
2. Scroll past the table of variants.
3. **Expected:** JSON examples present for all four reserved variants: `ScoredTooLow`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `Filtered`.

### 4. diagnostics.md column header renamed

1. Open `spec/src/diagnostics.md`.
2. Inspect the Summary table header row.
3. **Expected:** Column header reads "Spec page", not "Defined in".

### 5. UShapedPlacer pinned row corrected

1. Open `spec/src/placers/u-shaped.md`.
2. Locate the Edge Cases table row for "Pinned items (score 1.0)".
3. **Expected:** Cell text explains that UShapedPlacer has no special pinned logic; pinned items naturally rank highly due to score 1.0 from the pipeline. Old text "Placed at edges alongside other high-scored items." is absent.

### 6. CompositeScorer pseudocode assignment-complete

1. Open `spec/src/scorers/composite.md`.
2. Locate the `CONSTRUCT-COMPOSITE` pseudocode block.
3. **Expected:** Two explicit assignment lines present (`self.scorers <- ...` and `self.normalizedWeights <- ...`). Comment `// Store scorers and normalizedWeights` is absent.

### 7. TOML copies are identical

1. Run `diff spec/conformance/required/pipeline/greedy-chronological.toml crates/cupel/conformance/required/pipeline/greedy-chronological.toml`.
2. **Expected:** No output (files are identical). The `jan` density comment in both files reads `jan=0.0/200=0.0 (non-zero token, normal score path)` or similar.

### 8. Test suites pass

1. Run `cargo test --manifest-path crates/cupel/Cargo.toml`.
2. Run `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`.
3. **Expected:** Both exit 0 with no failures.

## Edge Cases

### Deferred issue file retained

1. Run `ls .planning/issues/open/`.
2. **Expected:** `2026-03-14-spec-workflow-checksum-verification.md` is present. This is the one intentionally deferred issue (CI security concern). Its presence is correct.

### context-item.md normative hierarchy preserved

1. Open `spec/src/data-model/context-item.md`.
2. Check the `content` field table cell: should read "The textual content of the item. Non-null and non-empty." (no informal MUST).
3. Scroll to Constraint 1: should still read "MUST be a non-null, non-empty string" (normative source preserved).
4. **Expected:** Informal MUST removed from table; formal constraint unchanged.

## Failure Signals

- Any `grep -n "Defined in" spec/src/diagnostics.md` match → T01 column rename was reverted or missed
- Any `grep -n "// Store scorers and normalizedWeights" spec/src/scorers/composite.md` match → T02 pseudocode fix was reverted
- Any `grep -n "Placed at edges alongside other high-scored items" spec/src/placers/u-shaped.md` match → T02 UShapedPlacer fix was reverted
- Non-empty `diff` between the two TOML files → drift guard violation
- Any spec-/phase24- issue files remaining in `.planning/issues/open/` beyond the checksum file → missed deletion
- Test suite failure → unintended code change introduced

## Requirements Proved By This UAT

- R041 — Spec quality debt closure: all ~8-10 (actual: 20) open spec/phase24 editorial issues are resolved; event ordering, item_count sentinel, observer callback normative status, greedy zero-token tiebreak, knapsack floor/truncation equivalence, UShapedPlacer pinned edge, CompositeScorer pseudocode storage, ScaledScorer nesting warning, KindScorer case-insensitivity source, context-item normative alignment, conformance format QuotaSlice note, and TOML density comment are all addressed.

## Not Proven By This UAT

- Semantic correctness of the spec chapters at a deep review level — this UAT confirms structural and mechanical fixes are present, not that every word is optimal. Human review of spec clarity remains part of the milestone DoD.
- Runtime conformance of implementations against the new ordering guarantees — the MUST rules added to events.md and greedy.md Conformance Notes are normative but not yet covered by dedicated conformance test vectors. New vectors would be required to mechanically enforce these rules.
- The deferred `spec-workflow-checksum-verification.md` issue — CI checksum verification is not tested by this UAT.

## Notes for Tester

The wc count for `ls .planning/issues/open/ | grep -E '^2026-03-1[45]-(spec-|phase24-)' | wc -l` returns 1, not 0 — this is correct. The slice plan incorrectly stated the checksum verification file had a different prefix pattern; it actually matches the grep. The one result is the intentionally retained deferred file.

When reviewing spec clarity (human-review portion of milestone DoD), focus on: the new Conformance Notes bullets in `greedy.md` (MUST NOT wording), the JSON examples in `exclusion-reasons.md` (are they accurate wire format?), and the cross-references added in `trace-collector.md` and `selection-report.md` (do the anchor links resolve?).
