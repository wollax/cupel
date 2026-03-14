# Phase 11: Language-Agnostic Specification — Verification

**Status:** gaps_found
**Date:** 2026-03-14
**Score:** 27/28 must-haves verified

## Must-Have Results

| # | Must-Have | Status | Evidence |
|---|-----------|--------|----------|
| 1 | mdBook builds locally with `mdbook build spec` producing a navigable HTML site | ✅ | `mdbook build spec` exits 0; HTML written to `spec/book` |
| 2 | Spec introduction declares spec version 1.0.0 | ✅ | `spec/src/introduction.md` line 3: `**Cupel Specification Version 1.0.0**` |
| 3 | Introduction states behavioral-equivalence conformance model | ✅ | `spec/src/introduction.md` lines 35–39: "behavioral equivalence" section with explicit definition |
| 4 | IEEE 754 64-bit doubles mandated for scoring | ✅ | `spec/src/introduction.md` lines 43–50: "Numeric Precision" section mandates IEEE 754 64-bit doubles, prohibits 32-bit floats |
| 5 | Data model: ContextItem defined with all fields, types, and constraints | ✅ | `spec/src/data-model/context-item.md`: 10-field table (content, tokens, kind, source, priority, tags, metadata, timestamp, futureRelevanceHint, pinned, originalTokens) with constraints |
| 6 | Data model: ContextBudget defined | ✅ | `spec/src/data-model/context-budget.md`: 5-field table with validation rules and effective budget formula |
| 7 | Data model: ScoredItem defined | ✅ | `spec/src/data-model/enumerations.md` lines 86–100: ScoredItem value type with item and score fields |
| 8 | Data model: ContextKind, ContextSource, OverflowStrategy defined | ✅ | `spec/src/data-model/enumerations.md`: all three enumerations with well-known values, comparison semantics, and construction rules |
| 9 | Pipeline chapter defines all 6 stages in fixed order with CLRS-style pseudocode for each | ✅ | `spec/src/pipeline.md` stage summary table lists all 6 in order; each stage chapter (`classify.md`, `score.md`, `deduplicate.md`, `sort.md`, `slice.md`, `place.md`) contains CLRS-style pseudocode |
| 10 | Stable sort with (Score, Index) tiebreaking prescribed — P1 addressed | ✅ | `spec/src/pipeline/sort.md`: Pitfall (P1) callout, composite key `(score descending, originalIndex ascending)`, both stable-sort and explicit-key approaches documented |
| 11 | Byte-exact deduplication comparison — P3 addressed | ✅ | `spec/src/pipeline/deduplicate.md`: Pitfall (P3) callout, "ordinal (byte-exact) comparison" with explicit no-normalization mandate |
| 12 | All 8 scorer algorithms have complete pseudocode with input/output contracts | ✅ | `spec/src/scorers/`: 8 files (recency, priority, kind, tag, frequency, reflexive, composite, scaled), each with CLRS pseudocode and conformance notes |
| 13 | Scorer interface contract defined: receives (item, allItems), returns float64 | ✅ | `spec/src/scorers.md` lines 9–15: `Score(item: ContextItem, allItems: list of ContextItem) -> float64` with pure-function and IEEE 754 contracts |
| 14 | KindScorer default weights specified (SystemPrompt=1.0, Memory=0.8, ToolOutput=0.6, Document=0.4, Message=0.2) | ✅ | `spec/src/scorers/kind.md` lines 19–25: default weights table with all 5 values matching plan |
| 15 | All 3 synchronous slicer algorithms have complete pseudocode specifications | ✅ | `spec/src/slicers/greedy.md`, `knapsack.md`, `quota.md` all contain CLRS pseudocode |
| 16 | KnapsackSlice score scaling and bucket discretization specified with precision caveats — P5 addressed | ✅ | `spec/src/slicers/knapsack.md`: "Precision Caveat (P5)" section, score scaling `floor(score * 10000)`, ceiling for weights/floor for capacity asymmetry documented |
| 17 | QuotaSlice budget distribution rounding documented — P6 addressed | ✅ | `spec/src/slicers/quota.md`: "Budget Distribution Rounding (P6)" section with floor truncation formula and example |
| 18 | Both placer algorithms have complete pseudocode specifications | ✅ | `spec/src/placers/chronological.md` and `u-shaped.md` contain CLRS pseudocode |
| 19 | UShapedPlacer even/odd index placement pattern precisely described | ✅ | `spec/src/placers/u-shaped.md`: `if i mod 2 = 0` → left, `else` → right; rank-to-position table; visual ASCII diagram |
| 20 | Conformance chapter defines two tiers: required (core behavior) and optional (edge cases) | ✅ | `spec/src/conformance/levels.md`: "Required" and "Optional" sections with coverage tables and conformance claiming rules |
| 21 | TOML test vector format fully documented with schema for each stage type | ✅ | `spec/src/conformance/format.md`: schemas for scoring, slicing, placing, pipeline vectors with all field types |
| 22 | Score assertions use epsilon tolerance (1e-9), not exact equality — P2 addressed | ✅ | `spec/src/conformance/format.md` lines 56–62 and `spec/src/introduction.md` lines 37–38: epsilon tolerance `1e-9` with formula `abs(actual - expected) < score_epsilon` |
| 23 | Required test vectors cover all 8 scorers | ✅ | `conformance/required/scoring/`: 13 TOML files covering all 8 scorer types (recency×2, priority×2, kind×2, tag×2, frequency×1, reflexive×2, composite×1, scaled×1) |
| 24 | Required test vectors cover all 3 sync slicers | ❌ | `conformance/required/slicing/`: 5 files covering only GreedySlice and KnapsackSlice. QuotaSlice is in `conformance/optional/slicing/quota-basic.toml` only. The must-have requires all 3 sync slicers in required tier. |
| 25 | Required test vectors cover both placers | ✅ | `conformance/required/placing/`: 4 files — `chronological-basic.toml`, `chronological-null-timestamps.toml`, `u-shaped-basic.toml`, `u-shaped-equal-scores.toml` |
| 26 | At least 2 end-to-end pipeline scenarios in required vectors | ✅ | `conformance/required/pipeline/`: 5 files (`greedy-chronological.toml`, `greedy-ushaped.toml`, `knapsack-chronological.toml`, `composite-greedy-chronological.toml`, `pinned-items.toml`) |
| 27 | Optional test vectors cover edge cases | ✅ | `conformance/optional/`: 10 files covering scoring edge cases (recency-single-item, recency-all-null, scaled-degenerate, composite-nested), slicing (greedy-empty-input, quota-basic), pipeline (empty-input, all-pinned, deduplication, overflow-truncate) |
| 28 | GitHub Actions workflow deploys spec to GitHub Pages on push to main | ✅ | `.github/workflows/spec.yml`: triggers on `push` to `main` with `paths: spec/**`, builds with `mdbook build spec`, deploys to `github-pages` environment |

## Verification Details

### Step 1: mdBook Build
`rtk proxy mdbook build spec` succeeded with output: `HTML book written to /Users/wollax/Git/personal/cupel/spec/book`.

### Step 2: Introduction
`spec/src/introduction.md` contains all required elements:
- Version string on line 3
- Behavioral equivalence conformance model (lines 35–39)
- IEEE 754 64-bit double mandate with explicit prohibition of 32-bit floats (lines 43–50)
- CLRS notation conventions table

### Step 3: Data Model
All 6 types from the must-haves are fully specified:
- `context-item.md`: 10 fields with types, defaults, and 6 named constraints
- `context-budget.md`: 5 fields with 7 validation rules
- `enumerations.md`: ContextKind (5 well-known values + extensibility), ContextSource (3 values), OverflowStrategy (3 values + detection algorithm), ScoredItem (2 fields)

### Step 4: Sort (P1)
`spec/src/pipeline/sort.md` explicitly mandates stable sort via composite key `(score descending, originalIndex ascending)` with a labelled Pitfall (P1) callout box.

### Step 5: Deduplicate (P3)
`spec/src/pipeline/deduplicate.md` mandates byte-exact ordinal comparison with a labelled Pitfall (P3) callout box, explicitly ruling out NFC/NFD/NFKC/NFKD normalization and case folding.

### Step 6: Scorer Interface
`spec/src/scorers.md` defines the interface as `Score(item: ContextItem, allItems: list of ContextItem) -> float64` with three named contracts (pure function, conventional range, IEEE 754).

### Step 7: Scorer Pseudocode Spot-Check
- `recency.md`: `RECENCY-SCORE` pseudocode with rank-based normalization
- `composite.md`: `COMPOSITE-SCORE` and `CONSTRUCT-COMPOSITE` pseudocode with weight normalization and cycle detection
- `kind.md`: `KIND-SCORE` pseudocode and `VALIDATE-KIND-WEIGHTS` pseudocode

### Step 8: KnapsackSlice (P5)
`spec/src/slicers/knapsack.md` section "Precision Caveat (P5)" explicitly states the score scaling factor (10000) and bucket size are implementation-defined parameters, and that conformance tests use score differences of at least 0.01.

### Step 9: QuotaSlice (P6)
`spec/src/slicers/quota.md` section "Budget Distribution Rounding (P6)" documents floor truncation for all three conversion points (requireTokens, capTokens, proportional) with a worked example showing 330+330=660 leaving 340 tokens.

### Step 10: UShapedPlacer
`spec/src/placers/u-shaped.md` algorithm uses `i mod 2 = 0` → left edge, `else` → right edge. A rank-to-position table and a 7-item ASCII visual diagram confirm the pattern precisely.

### Step 11: Conformance Tiers
`spec/src/conformance/levels.md` defines "Required" (MUST pass for Cupel conformance) and "Optional" (MAY pass for Cupel Full conformance) tiers with algorithm coverage tables and two distinct conformance labels.

### Step 12: Test Vector Format + Epsilon
`spec/src/conformance/format.md` documents schemas for all 4 stage types with `score_approx`/`score_epsilon` fields and the epsilon comparison formula `abs(actual_score - expected_score) < score_epsilon`.

### Step 13: TOML Vector Counts
- Required: 27 vectors (13 scoring, 5 slicing, 4 placing, 5 pipeline)
- Optional: 10 vectors (4 scoring, 2 slicing, 4 pipeline)

### Step 14: All 8 Scorers Have Required Vectors
`conformance/required/scoring/` contains 13 files covering: recency (2), priority (2), kind (2), tag (2), frequency (1), reflexive (2), composite (1), scaled (1). All 8 scorer types represented.

### Step 15: GitHub Actions Workflow
`.github/workflows/spec.yml` exists, triggers on `push` to `main` with path filter `spec/**`, installs mdBook v0.4.43, runs `mdbook build spec`, uploads `spec/book` as a Pages artifact, and deploys to `github-pages` environment.

### Step 16: Changelog
`spec/src/changelog.md`: version `[1.0.0]` dated 2026-03-14, lists all pipeline stages, all 8 scorers, all 3 slicers, both placers, and all 6 pitfall decisions (P1–P6).

## Gaps

### GAP-1: QuotaSlice not in required test vectors (Must-Have #24)

**Severity:** Medium

**Description:** Plan 03 must-have states: "Required test vectors cover all 8 scorers, all 3 sync slicers, both placers, and at least 2 end-to-end pipeline scenarios." The 3 synchronous slicers are GreedySlice, KnapsackSlice, and QuotaSlice. However, `conformance/required/slicing/` contains only GreedySlice and KnapsackSlice vectors (5 total). QuotaSlice has exactly one test vector, located in `conformance/optional/slicing/quota-basic.toml`.

**Impact:** An implementation could claim "Cupel Conformant" status without passing any QuotaSlice test. This weakens the conformance guarantee for QuotaSlice, which is the most complex slicer (budget distribution, require/cap constraints, inner slicer delegation).

**Note:** There is an internal contradiction in Plan 03 itself — the must-have says "all 3 sync slicers" in required, but the action tasks explicitly place `quota-basic.toml` in optional. The implementation followed the action tasks. Resolution options:
1. Move `conformance/optional/slicing/quota-basic.toml` to `conformance/required/slicing/` and update `spec/src/conformance/levels.md` accordingly.
2. Accept the current placement and amend the must-have to read "GreedySlice and KnapsackSlice" for required, with QuotaSlice as optional-only.

**Affected files:**
- `conformance/optional/slicing/quota-basic.toml` (should move to required, or stay with amended must-have)
- `spec/src/conformance/levels.md` (Required slicers table lists only GreedySlice, KnapsackSlice)
- `spec/src/changelog.md` (vector count would change if moved)
