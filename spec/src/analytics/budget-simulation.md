# Budget Simulation

Budget simulation methods are extension methods on `CupelPipeline` in the .NET implementation. They orchestrate internal `DryRun` calls to answer questions about what the pipeline would select at different token budgets — for example, which items are marginal at a given budget, or what is the minimum budget required to include a specific item.

**Language Parity Note:** The budget simulation API is scoped to the .NET implementation in v1.3. Rust parity is deferred to M003+.

**`SweepBudget` Out-of-Scope Note:** `SweepBudget` (exhaustive budget sweep) has been assigned to the Smelt project and will not be added to Cupel.

---

## DryRun Determinism Invariant

`DryRun` MUST produce identical output for identical inputs. Tie-breaking order MUST be stable across calls — items with equal scores or equal token counts MUST be ordered consistently across repeated invocations with the same inputs. Implementations that depend on non-deterministic ordering (e.g., hash-map iteration order) are non-conformant.

This invariant is the foundation on which the budget simulation methods rely. Both `GetMarginalItems` and `FindMinBudgetFor` call `DryRun` multiple times and compare the results; non-deterministic `DryRun` output would make those comparisons meaningless.

---

## `GetMarginalItems`

### Purpose

Identify which items are included in a full-budget run but excluded when the budget is reduced by `slackTokens`. These are the items that become available as the budget grows from `(budget.MaxTokens - slackTokens)` up to `budget.MaxTokens`.

### Signature

```csharp
IReadOnlyList<ContextItem> GetMarginalItems(
    IReadOnlyList<ContextItem> items,
    ContextBudget budget,
    int slackTokens)
```

### Budget Parameter

The `budget` parameter overrides the pipeline's stored budget for both internal `DryRun` calls. This is required because `DryRun` uses the pipeline's fixed budget by construction; the extension method supplies a temporary budget for its internal calls.

The reduced-budget run uses `budget.MaxTokens - slackTokens` as the max token count, with the same `outputReserve` and `reservedSlots` as the full budget. Formally:

```
reducedBudget = ContextBudget(
    maxTokens:    budget.MaxTokens - slackTokens,
    targetTokens: budget.TargetTokens - slackTokens,
    outputReserve: budget.OutputReserve)
```

### Diff Direction

`primary \ margin` — items present in the full-budget result (`primary`) that are absent from the reduced-budget result (`margin`). Item identity is determined by object reference equality.

### Monotonicity Assumption

`GetMarginalItems` assumes that a lower budget never causes a new item to appear that was not present at the higher budget (monotonic inclusion). This assumption holds for `GreedySlice` and `KnapsackSlice` but does **not** hold for `QuotaSlice`, where percentage-based allocations shift as the budget changes and can cause different kinds to appear at lower budgets.

### QuotaSlice Guard

If the pipeline's slicer is `QuotaSlice`, the method MUST throw `InvalidOperationException` with the message:

> `"GetMarginalItems requires monotonic item inclusion. QuotaSlice produces non-monotonic inclusion as budget changes shift percentage allocations."`

### Pseudocode

```text
GET-MARGINAL-ITEMS(pipeline, items, budget, slackTokens):
    reducedBudget <- ContextBudget(maxTokens: budget.maxTokens - slackTokens,
                                   targetTokens: budget.targetTokens - slackTokens,
                                   outputReserve: budget.outputReserve)
    primary <- pipeline.DRY-RUN(items, budget)
    margin  <- pipeline.DRY-RUN(items, reducedBudget)
    return [item in primary.included where item not in margin.included]
```

---

## `FindMinBudgetFor`

### Purpose

Find the minimum token budget (within a search ceiling) at which `targetItem` would be included in the selection result. Returns `null` if `targetItem` is not selectable within `[targetItem.Tokens, searchCeiling]`.

### Signature

```csharp
int? FindMinBudgetFor(
    IReadOnlyList<ContextItem> items,
    ContextItem targetItem,
    int searchCeiling)
```

### Preconditions

Both conditions are checked before the search begins. If either is violated, the method MUST throw `ArgumentException`:

- `targetItem` must be an element of `items`.
- `searchCeiling >= targetItem.Tokens`.

### Binary Search

The search range is `[targetItem.Tokens, searchCeiling]`. The lower bound is `targetItem.Tokens` as an optimization: the target item cannot be included in a budget smaller than its own token count.

The stop condition is `high - low <= 1`. At termination, `high` is the candidate minimum budget. The search performs approximately `log₂(searchCeiling)` `DryRun` invocations — typically 10–15 for realistic budget ceilings.

After the loop exits, the method performs one final `DryRun` at `high` to confirm inclusion. If `targetItem` is present in `report.included`, `high` is returned. Otherwise `null` is returned.

### Return Value

`int?` — `null` means `targetItem` is not selectable within `[targetItem.Tokens, searchCeiling]` at any tested budget.

### QuotaSlice + CountQuotaSlice Guard

If the pipeline's slicer is `QuotaSlice` or `CountQuotaSlice`, the method MUST throw `InvalidOperationException` with the message:

> `"FindMinBudgetFor requires monotonic item inclusion. QuotaSlice and CountQuotaSlice produce non-monotonic inclusion as budget changes shift allocations. Use a GreedySlice or KnapsackSlice inner slicer for budget simulation."`

The general precondition is: any slicer whose item inclusion is sensitive to absolute budget value in a non-monotonic way is incompatible with `FindMinBudgetFor`.

### Pseudocode

```text
FIND-MIN-BUDGET-FOR(pipeline, items, targetItem, searchCeiling):
    low  <- targetItem.tokens      // inclusive lower bound
    high <- searchCeiling          // inclusive upper bound

    if targetItem not in items:
        throw ArgumentException("targetItem must be an element of items")
    if searchCeiling < targetItem.tokens:
        throw ArgumentException("searchCeiling must be >= targetItem.Tokens")

    while high - low > 1:
        mid <- low + (high - low) / 2
        midBudget <- ContextBudget(maxTokens: mid, ...)
        report <- pipeline.DRY-RUN(items, midBudget)
        if targetItem in report.included:
            high <- mid
        else:
            low <- mid

    // Verify at high (the candidate minimum)
    finalBudget <- ContextBudget(maxTokens: high, ...)
    finalReport <- pipeline.DRY-RUN(items, finalBudget)
    if targetItem in finalReport.included:
        return high
    return null
```

---

## Conformance Notes

- `DryRun` is the primitive; budget simulation builds entirely on top of it. Implementations MUST satisfy the DryRun Determinism Invariant before exposing budget simulation methods.
- Custom slicers that implement non-monotonic inclusion MUST NOT be used with `FindMinBudgetFor` or `GetMarginalItems` without explicit documentation of the monotonicity property.
- The `included` field of the `DryRun` result (see [SelectionReport](../diagnostics/selection-report.md)) is the source of truth for item identity comparisons in both methods.
