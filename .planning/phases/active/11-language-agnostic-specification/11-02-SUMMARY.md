# Phase 11 Plan 02: Algorithm Specification Chapters (Scorers, Slicers, Placers)

**One-liner:** Complete CLRS-style pseudocode specifications for all 8 scorers, 3 slicers, and 2 placers

**Duration:** ~8 minutes
**Tasks:** 2/2 completed

## What Was Done

### Task 1: Scorer specification chapters
- Wrote `spec/src/scorers.md` overview defining the scorer interface contract (`Score(item, allItems) -> float64`), pure function semantics, conventional [0.0, 1.0] output range, and categorization table (absolute, relative, composite)
- Wrote 8 scorer chapters, each with: overview, CLRS-style pseudocode, edge cases table, complexity analysis, and conformance notes
  - **RecencyScorer**: rank-based temporal ordering; null timestamps score 0.0; single timestamped item scores 1.0
  - **PriorityScorer**: rank-based priority ordering; identical algorithm structure to RecencyScorer
  - **KindScorer**: dictionary lookup with default weights (SystemPrompt=1.0, Memory=0.8, ToolOutput=0.6, Document=0.4, Message=0.2); case-insensitive ContextKind lookup (P4)
  - **TagScorer**: weighted tag matching normalized by total weight, clamped to [0.0, 1.0]; case-sensitive default comparison
  - **FrequencyScorer**: proportion of peers sharing any tag; self-exclusion by reference identity; case-insensitive tag comparison
  - **ReflexiveScorer**: passthrough of futureRelevanceHint; null/non-finite returns 0.0; clamp to [0.0, 1.0]
  - **CompositeScorer**: weighted average with normalized weights; DAG cycle detection via DFS at construction; weight validation (positive, finite)
  - **ScaledScorer**: min-max normalization of inner scorer; degenerate case returns 0.5; O(N^2) total complexity

### Task 2: Slicer and placer specification chapters
- Wrote `spec/src/slicers.md` overview defining the slicer interface contract, summary table, and StreamSlice as implementation-optional
- Wrote 3 slicer chapters:
  - **GreedySlice**: value-density sort + greedy fill; zero-token items always included; O(N log N)
  - **KnapsackSlice**: 0/1 knapsack DP with discretized weights/capacity; score scaling (*10000, floor); ceiling-division weights, floor-division capacity; P5 precision caveat documented
  - **QuotaSlice**: decorator pattern partitioning by ContextKind; require/cap budget distribution with floor truncation; P6 rounding behavior documented; P4 case-insensitivity documented
- Wrote `spec/src/placers.md` overview defining the placer interface contract (reorder only, no add/remove)
- Wrote 2 placer chapters:
  - **ChronologicalPlacer**: stable sort ascending by timestamp; null timestamps sort last; tiebreak by index
  - **UShapedPlacer**: even-ranked items placed left edge, odd-ranked items placed right edge; ASCII art score plot diagram; visual example with 7 items

## Pitfalls Addressed
- **P4 (ContextKind Case-Insensitivity):** Explicitly documented in KindScorer and QuotaSlice
- **P5 (KnapsackSlice Precision):** Score scaling factor (10000) and bucket size documented as implementation-defined defaults; conformance tests use >= 0.01 score separation
- **P6 (QuotaSlice Rounding):** Floor truncation documented with example showing sum of kind budgets < targetTokens

## Deviations
None. Plan executed as specified.

## Commits
- `5bdd921`: docs(spec): write scorer algorithm specification chapters
- `62ba327`: docs(spec): write slicer and placer algorithm specification chapters

## Artifacts
- `spec/src/scorers.md` + 8 sub-pages (recency, priority, kind, tag, frequency, reflexive, composite, scaled)
- `spec/src/slicers.md` + 3 sub-pages (greedy, knapsack, quota)
- `spec/src/placers.md` + 2 sub-pages (chronological, u-shaped)

## Verification
- `mdbook build spec` succeeds with zero errors after both tasks
- All 8 scorer chapters have CLRS-style pseudocode, edge cases, and conformance notes
- All 3 slicer chapters have pseudocode, complexity analysis, and conformance notes
- Both placer chapters have pseudocode, edge cases, and conformance notes
- Scorer interface contract defined: `Score(item, allItems) -> float64`
- KindScorer default weights explicitly listed
- KnapsackSlice discretization and precision caveat documented
- QuotaSlice floor truncation rounding documented
- UShapedPlacer even/odd placement described with visual example
- StreamSlice documented as implementation-optional
