# Phase 12 PR Review — Suggestions Backlog

**Created:** 2026-03-14
**Source:** PR #24 review (4 agents: code quality, error handling, test quality, type design)

## Code Quality Suggestions

1. **ContextKind constants require unwrap at call sites** — Add `pub fn message() -> Self` etc. factory methods (`model/context_kind.rs`)
2. **ScoredItem pub fields break encapsulation** — Consider private fields with constructor (`model/scored_item.rs`)
3. **CompositeScorer index loop** — Use `.zip()` idiom instead (`scorer/composite.rs:69`)
4. **Missing `available_tokens()` helper** — Formula duplicated in classify.rs and slice.rs (`model/context_budget.rs`)
5. **Deduplicate unconditional String clone** — Use `get(&str)` before `insert` (`pipeline/deduplicate.rs:18`)
6. **KnapsackSlice Vec<Vec<bool>> keep table** — Use flat Vec with manual 2D indexing for single allocation (`slicer/knapsack.rs:82`)
7. **KnapsackSlice unbounded DP table** — Add size limit check to prevent OOM (`slicer/knapsack.rs:82`)

## Type Design Suggestions

8. **OverflowStrategy not #[non_exhaustive]** — Future variants would be breaking (`model/overflow_strategy.rs`)
9. **ContextBudget needs a builder** — 5-arg constructor is awkward (`model/context_budget.rs`)
10. **Pipeline::run always clones** — Consider `run_owned(Vec<ContextItem>)` overload (`pipeline/mod.rs`)
11. **CupelError field messages lack "tokens" unit** — PinnedExceedsBudget/Overflow (`error.rs`)
12. **ContextItemBuilder::tags takes Vec<String>** — Should take `impl IntoIterator<Item = impl Into<String>>` (`model/context_item.rs:136`)
13. **Missing TryFrom<&str>/TryFrom<String>** for ContextKind/ContextSource
14. **ContextKind case not normalised** — Equal values display differently
15. **GreedySlice, KnapsackSlice, placers missing Debug/Clone/Copy derives**
16. **CompositeScorer/ScaledScorer not cloneable** — Consider dyn_clone approach
17. **reserved_slots() returns concrete HashMap** — Not future-safe API

## Test Suggestions

18. **Zero unit tests in src/** — Add #[cfg(test)] modules for: ContextBudget validation, ContextKind Hash/Eq, classify PinnedExceedsBudget, deduplicate tiebreaking, compute_effective_budget, KnapsackSlice/QuotaSlice constructor errors, CompositeScorer cycle detection
19. **FrequencyScorer case-insensitive tag matching untested** — Add conformance vector
20. **No overflow strategy tests** — Add pipeline vectors for Throw/Truncate
21. **No deduplication tests** — Add pipeline vector with duplicate content
22. **Composite child-scorer config always None** — Fix test harness to pass sub-table config
23. **ScaledScorer degenerate case (all-equal → 0.5) untested**
24. **ReflexiveScorer NaN/Inf untested**
25. **DRY violation** — placing.rs reimplements datetime parsing from conformance.rs
