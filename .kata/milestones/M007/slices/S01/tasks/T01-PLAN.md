---
estimated_steps: 4
estimated_files: 2
---

# T01: Write failing tests for DryRunWithPolicy and the policy-based PolicySensitivity overload

**Slice:** S01 — .NET DryRunWithPolicy and policy-accepting PolicySensitivity
**Milestone:** M007

## Description

Create `DryRunWithPolicyTests.cs` and extend `PolicySensitivityTests.cs` with test methods that reference the two new APIs (`CupelPipeline.DryRunWithPolicy` and the `CupelPolicy`-accepting `PolicySensitivity` overload). At this point the APIs do not yet exist, so the tests must fail to compile — this is intentional and confirms the tests reference real new surface rather than accidentally exercising existing code.

The tests define the objective stopping condition for this slice. T02 is "done" when these tests all pass.

## Steps

1. Create `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` with 6 test methods:
   - `UsesPolicy_Scorer_NotPipelines` — build a pipeline with `ReflexiveScorer`; call `pipeline.DryRunWithPolicy(items, budget, policy)` where `policy` uses a private `InvertedRelevanceScorer` (nested class: score = `1.0 - hint`). Items have descending hints; budget fits 2 of 4. Assert the result includes the two *lowest*-hint items (policy's scorer wins) and excludes the two highest.
   - `UsesExplicitBudget_NotPipelineBudget` — pipeline has budget 500t; `DryRunWithPolicy` is called with budget 100t (fits exactly 1 item of 100t); policy uses `ScorerType.Reflexive`. Assert `result.Report!.Included.Count == 1`.
   - `UsesPolicy_Slicer_Greedy_vs_Knapsack` — 4 items of mixed token sizes (e.g. 80, 80, 150, 150) with budget 200t; GreedySlice policy fits two 80t items (160t), KnapsackSlice policy also fits two 80t items (same result at these sizes is fine, just prove slicer is respected without error). Assert no exception and report is non-null.
   - `ThrowsArgumentNullException_WhenItemsNull` — `pipeline.DryRunWithPolicy(null!, budget, policy)` throws `ArgumentNullException`.
   - `ThrowsArgumentNullException_WhenBudgetNull` — `pipeline.DryRunWithPolicy(items, null!, policy)` throws `ArgumentNullException`.
   - `ThrowsArgumentNullException_WhenPolicyNull` — `pipeline.DryRunWithPolicy(items, budget, null!)` throws `ArgumentNullException`.

2. Add 3 test methods to `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs`:
   - `PolicyOverload_TwoPoliciesWithDifferentScorers_ProducesMeaningfulDiff` — 4 items each 100t, budget 200t (fits 2). PolicyA: `ScorerType.Reflexive`+`PlacerType.Chronological`. PolicyB: requires a custom `InvertedRelevanceScorer` ... but since `CupelPolicy` only accepts `ScorerType` enum, use `ScorerType.Priority` for PolicyB (PriorityScorer) paired with items that have `Priority` set so the two scorers produce different rankings, OR use two policies with `ScorerType.Reflexive` but different slicer configs that create different token fits. Cleanest approach: PolicyA uses `SlicerType.Greedy` with 4 items each 100t and budget 200t → includes 2. Create PolicyB with `ScorerType.Reflexive` and deduplication disabled, then set two items as exact-content duplicates so dedup-on includes them both uniquely. Actually, the simplest approach that doesn't require custom scorers: create items where `FutureRelevanceHint` differences matter for `ScorerType.Reflexive`, and distinguish the two policies by using `ScorerType.Recency` for the second (RecencyScorer ignores hint, scores by `Timestamp` ordinal). Items with null timestamps all score 0.5 under RecencyScorer. Combined with hint-based Reflexive the top-2 items differ. Assert `report.Diffs.Count >= 1`.
   - `PolicyOverload_ThrowsWhenFewerThanTwoVariants` — `PolicySensitivityExtensions.PolicySensitivity(items, budget, ("only", policy))` throws `ArgumentException`.
   - `PolicyOverload_MatchesPipelineOverload_WhenEquivalentConfiguration` — build two `CupelPolicy` objects (PolicyA: `ScorerType.Reflexive`, `SlicerType.Greedy`) and two matching `CupelPipeline` objects using the same scorer/slicer. Call both overloads with the same items and budget. Assert `report.Diffs.Count` is the same and `report.Variants.Count` is the same. (The diff is empty because both pipelines are equivalent, but both overloads agree on 0 diffs.)

3. Attempt `dotnet build tests/Wollax.Cupel.Tests/` and confirm it fails with member-not-found errors referencing `DryRunWithPolicy` and the new `PolicySensitivity` overload — this is the expected "red" state.

4. Commit the failing test files: `test(S01): add failing tests for DryRunWithPolicy and policy-based PolicySensitivity`.

## Must-Haves

- [ ] `DryRunWithPolicyTests.cs` contains all 6 test methods with real assertions (not just stubs)
- [ ] `PolicySensitivityTests.cs` additions contain all 3 test methods with real assertions
- [ ] Tests reference `pipeline.DryRunWithPolicy(items, budget, policy)` — exact future method signature
- [ ] Tests reference `PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string, CupelPolicy)[])` — exact future overload signature
- [ ] Build fails at this point due to unresolved member references (confirms tests are exercising new surface)
- [ ] All test files use `using TUnit.Core; using TUnit.Assertions; using TUnit.Assertions.Extensions;` — consistent with existing test files

## Verification

```bash
# Confirm tests reference the new APIs (will fail to compile — expected)
dotnet build tests/Wollax.Cupel.Tests/ 2>&1 | grep -E "CS0117|does not contain|no definition for"
```

Expected output: errors referencing `DryRunWithPolicy` and the `CupelPolicy` `PolicySensitivity` overload.

## Observability Impact

- Signals added/changed: None — tests only; no runtime code changes.
- How a future agent inspects this: `dotnet test --filter "DryRunWithPolicy|PolicySensitivity"` shows which of T01's tests still fail after T02.
- Failure state exposed: Test failure messages will include `result.Report!.Included.Count` assertions, making it clear which items were selected by which scorer.

## Inputs

- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — existing test file to extend; study its `InvertedRelevanceScorer` nested class pattern and TUnit assertion style
- `src/Wollax.Cupel/CupelPolicy.cs` — review `ScorerType` enum values available (Reflexive, Recency, Priority, Kind, Tag, Frequency, Scaled) to pick ones that produce meaningfully different selections in tests
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — understand existing `PolicySensitivity(params (string Label, CupelPipeline Pipeline)[])` signature to define the new overload signature correctly

## Expected Output

- `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` — new file with 6 failing test methods
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — 3 new test methods appended to existing file
- `dotnet build tests/Wollax.Cupel.Tests/` fails with CS member-not-found errors (expected red state)
