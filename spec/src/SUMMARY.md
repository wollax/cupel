# Summary

[Introduction](introduction.md)

# Specification

- [Data Model](data-model.md)
  - [ContextItem](data-model/context-item.md)
  - [ContextBudget](data-model/context-budget.md)
  - [Enumerations](data-model/enumerations.md)
- [Pipeline](pipeline.md)
  - [Stage 1: Classify](pipeline/classify.md)
  - [Stage 2: Score](pipeline/score.md)
  - [Stage 3: Deduplicate](pipeline/deduplicate.md)
  - [Stage 4: Sort](pipeline/sort.md)
  - [Stage 5: Slice](pipeline/slice.md)
  - [Stage 6: Place](pipeline/place.md)
- [Scorers](scorers.md)
  - [RecencyScorer](scorers/recency.md)
  - [PriorityScorer](scorers/priority.md)
  - [KindScorer](scorers/kind.md)
  - [TagScorer](scorers/tag.md)
  - [FrequencyScorer](scorers/frequency.md)
  - [ReflexiveScorer](scorers/reflexive.md)
  - [CompositeScorer](scorers/composite.md)
  - [ScaledScorer](scorers/scaled.md)
- [Slicers](slicers.md)
  - [GreedySlice](slicers/greedy.md)
  - [KnapsackSlice](slicers/knapsack.md)
  - [QuotaSlice](slicers/quota.md)
- [Placers](placers.md)
  - [ChronologicalPlacer](placers/chronological.md)
  - [UShapedPlacer](placers/u-shaped.md)

# Conformance

- [Conformance Levels](conformance/levels.md)
- [Test Vector Format](conformance/format.md)
- [Running the Suite](conformance/running.md)

# Appendix

- [Changelog](changelog.md)
