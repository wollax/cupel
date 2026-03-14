# Plan 11-03 Summary: Conformance Suite and Deployment

## Status: Complete

## What Was Done

### Task 1: Conformance Chapters, TOML Test Vectors, and Changelog

**Conformance chapters** (replaced placeholder stubs):

- `spec/src/conformance/levels.md` — Defines Required and Optional tiers, lists covered algorithms per tier, documents conformance claim levels
- `spec/src/conformance/format.md` — Documents TOML test vector schema for all 4 stage types (scoring, slicing, placing, pipeline), field types, comparison modes, epsilon tolerance for floating-point scores (P2)
- `spec/src/conformance/running.md` — Pseudocode conformance runner, per-stage test execution approach, score comparison with epsilon, TOML library recommendations for 8 languages

**Changelog**: `spec/src/changelog.md` — Version 1.0.0 (2026-03-14) with complete feature listing and pitfall resolution summary (P1-P6)

**Conformance README**: `conformance/README.md` — Directory structure, usage instructions, link to rendered spec

**Test vectors** — 37 total (27 required, 10 optional):

| Category | Required | Optional | Total |
|---|---|---|---|
| Scoring | 13 | 4 | 17 |
| Slicing | 5 | 2 | 7 |
| Placing | 4 | 0 | 4 |
| Pipeline | 5 | 4 | 9 |
| **Total** | **27** | **10** | **37** |

Required scoring vectors cover all 8 scorer types:
- RecencyScorer (basic + null timestamps)
- PriorityScorer (basic + null priorities)
- KindScorer (default weights + unknown kind)
- TagScorer (basic weight overlap + no tags)
- FrequencyScorer (basic tag sharing)
- ReflexiveScorer (basic clamping + null hint)
- CompositeScorer (weighted average of recency + priority)
- ScaledScorer (min-max normalization of reflexive)

Required slicing vectors: GreedySlice (density sort, zero-token inclusion, exact fit), KnapsackSlice (optimal selection, zero-token pre-filter)

Required placing vectors: ChronologicalPlacer (basic + null timestamps), UShapedPlacer (basic edge placement + equal-score tiebreaking)

Required pipeline vectors: greedy+chronological, greedy+u-shaped, knapsack+chronological, pinned items, composite scorer pipeline

Optional vectors: single-item recency, all-null timestamps, degenerate ScaledScorer, nested CompositeScorer, empty slicer input, QuotaSlice require/cap, empty pipeline, all-pinned, deduplication, overflow truncation

### Task 2: GitHub Pages Deployment Workflow

- `.github/workflows/spec.yml` — Triggers on push to main (spec/ or conformance/ paths), plus workflow_dispatch. Uses pre-built mdBook v0.4.43 binary. Deploys to GitHub Pages via actions/deploy-pages@v4.

## Verification

- All 37 TOML files parsed successfully with Python's `tomllib`
- `mdbook build spec` succeeded with no warnings
- All conformance chapter HTML files generated (levels, format, running) — no stub pages remain
- Changelog HTML generated
- Workflow YAML validated

## Must-Have Checklist

- [x] Conformance chapter defines Required and Optional tiers
- [x] TOML test vector format documented with schema for scoring, slicing, placing, pipeline
- [x] Required test vectors cover all 8 scorers, GreedySlice, KnapsackSlice, both placers, 5 pipeline scenarios
- [x] Optional test vectors cover edge cases: empty input, single item, all-pinned, deduplication, equal scores, overflow
- [x] Score assertions use epsilon tolerance (1e-9)
- [x] GitHub Actions workflow deploys spec to GitHub Pages
- [x] Conformance README explains how to parse and run vectors

## Commits

1. `docs(11): add conformance chapters, TOML test vectors, and changelog`
2. `ci(11): add GitHub Pages deployment workflow for spec`
