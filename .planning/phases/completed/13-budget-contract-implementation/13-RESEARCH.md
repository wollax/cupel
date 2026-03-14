# Phase 13: Budget Contract Implementation — RESEARCH

## Summary

This phase wires two unused `ContextBudget` properties (`ReservedSlots` and `EstimationSafetyMarginPercent`) into the pipeline execution path and updates `REQUIREMENTS.md` checkboxes. The properties already exist in the public API with validation; the work is purely about making them functional in pipeline budget computation.

## Spec vs. Phase Description — Critical Tension

**Confidence: HIGH** (verified by reading spec source directly)

The spec (Phase 11) explicitly states:

1. **`reservedSlots`**: "guarantees minimum representation per ContextKind. This is consumed by QuotaSlice; other slicers ignore it." (`spec/src/data-model/context-budget.md` line 46)

2. **`estimationSafetyMarginPercent`**: "available for caller use in budget computation but is not directly consumed by the core pipeline stages." (`spec/src/data-model/context-budget.md` line 48)

3. **Conformance note**: "The slicer receives a fresh ContextBudget with only `maxTokens` and `targetTokens` set. Other budget fields (`outputReserve`, `reservedSlots`, `estimationSafetyMarginPercent`) are not forwarded to the slicer in the adjusted budget." (`spec/src/pipeline/slice.md` line 68)

The phase description says to make these properties functional in the pipeline. This is a **deliberate spec evolution** — the phase is closing a gap where the API promises features the runtime ignores. The spec was written to document _current_ behavior; the phase changes the behavior. The spec will need updating to match.

**Recommendation:** Implement the pipeline changes as described in the phase, then update spec text to reflect the new behavior. The conformance note in `slice.md` line 68 will need revision.

## Existing Code Architecture

### Pipeline Budget Computation (CupelPipeline.cs lines 240-248)

**Confidence: HIGH** (read directly from source)

The effective budget for the slicer is computed in `ExecuteCore()`:

```csharp
// SLICE: create adjusted budget and slice
var effectiveMax = Math.Max(0, _budget.MaxTokens - _budget.OutputReserve - pinnedTokens);
var effectiveTarget = Math.Max(0, _budget.TargetTokens - pinnedTokens);
effectiveTarget = Math.Min(effectiveTarget, effectiveMax);

var adjustedBudget = new ContextBudget(
    maxTokens: effectiveMax,
    targetTokens: effectiveTarget);
```

This is the exact integration point. Both `ReservedSlots` and `EstimationSafetyMarginPercent` are on `_budget` but not used here.

### Streaming Path (CupelPipeline.cs lines 491-495)

The async `ExecuteStreamAsync` has a parallel budget computation:

```csharp
var effectiveMax = Math.Max(0, _budget.MaxTokens - _budget.OutputReserve);
var effectiveTarget = Math.Min(_budget.TargetTokens, effectiveMax);
var adjustedBudget = new ContextBudget(
    maxTokens: effectiveMax,
    targetTokens: effectiveTarget);
```

This path also needs the same adjustments. Note: streaming has no pinned items, so `pinnedTokens` is not subtracted here.

### What Slicers Receive

All slicers (`GreedySlice`, `KnapsackSlice`, `QuotaSlice`, `StreamSlice`) receive a `ContextBudget` and use `budget.TargetTokens` as their fill target. They never read `ReservedSlots` or `EstimationSafetyMarginPercent` from the budget they receive.

### ContextBudget Constructor Constraints

`ContextBudget` requires `targetTokens <= maxTokens`. Any budget adjustment code must maintain this invariant when constructing the adjusted budget.

## Integration Design

### ReservedSlots Integration

**Confidence: HIGH**

`ReservedSlots` is a `IReadOnlyDictionary<ContextKind, int>` mapping kinds to token counts. The phase description says "subtracts per-kind token reservations from the slicer's available budget."

**Where to apply:** In `ExecuteCore()`, after computing `pinnedTokens` and before constructing `adjustedBudget`. Sum the values in `ReservedSlots` and subtract from `effectiveTarget` and `effectiveMax`.

**Computation:**
```
reservedTokens = sum of _budget.ReservedSlots.Values
effectiveMax    = max(0, maxTokens - outputReserve - pinnedTokens - reservedTokens)
effectiveTarget = max(0, targetTokens - pinnedTokens - reservedTokens)
effectiveTarget = min(effectiveTarget, effectiveMax)
```

**Key considerations:**
- Default is empty dictionary (`.Count == 0`), so existing code paths are unaffected when no reserved slots are configured.
- Zero-allocation path: only iterate if `ReservedSlots.Count > 0`.
- The reserved tokens should NOT be subtracted from the pinned budget validation (line 138-143), only from the slicer budget. Pinned items are validated against `maxTokens - outputReserve` which is the hard ceiling before any reservations.
- The sum can be computed once with a simple `for`/`foreach` loop over the dictionary values. Since this is outside the hot scoring path and happens once per execution, `foreach` on dictionary values is acceptable.

**Interaction with QuotaSlice:** QuotaSlice already has its own per-kind budget distribution via Require/Cap percentages. ReservedSlots is a separate mechanism — it reserves token budget at the pipeline level, reducing what the slicer sees. The two systems are complementary: ReservedSlots carves out budget before the slicer runs; QuotaSlice distributes the remaining budget proportionally. No special coordination needed.

### EstimationSafetyMarginPercent Integration

**Confidence: HIGH**

`EstimationSafetyMarginPercent` is a `double` in range [0, 100]. The phase description says "applies a safety margin to the effective token ceiling" as a "multiplicative reduction."

**Where to apply:** In `ExecuteCore()`, after computing effective budget values but before constructing `adjustedBudget`.

**Computation:**
```
marginMultiplier = 1.0 - (_budget.EstimationSafetyMarginPercent / 100.0)
effectiveMax    = (int)(effectiveMax * marginMultiplier)
effectiveTarget = (int)(effectiveTarget * marginMultiplier)
effectiveTarget = min(effectiveTarget, effectiveMax)
```

**Key considerations:**
- Default is `0.0`, so `marginMultiplier = 1.0` and no reduction occurs (backward compatible).
- At 100%, `marginMultiplier = 0.0`, which zeros the budget — the slicer selects nothing (only zero-token items). This is valid edge behavior.
- The cast to `int` truncates toward zero (floor for positive values), which is consistent with the rest of the codebase's budget arithmetic.
- Apply AFTER `ReservedSlots` subtraction so the margin applies to the already-reduced budget.

### Order of Operations

The full budget computation becomes:

1. `effectiveMax = max(0, maxTokens - outputReserve - pinnedTokens - reservedTokens)`
2. `effectiveTarget = max(0, targetTokens - pinnedTokens - reservedTokens)`
3. `effectiveTarget = min(effectiveTarget, effectiveMax)`
4. Apply safety margin: multiply both by `(1.0 - margin/100.0)`, cast to int
5. Re-clamp: `effectiveTarget = min(effectiveTarget, effectiveMax)`

The re-clamp after step 4 is important because integer truncation during the cast could make `effectiveTarget > effectiveMax` in edge cases.

## Backward Compatibility

**Confidence: HIGH**

- `ReservedSlots` defaults to empty dictionary. Sum of empty = 0. No change.
- `EstimationSafetyMarginPercent` defaults to 0.0. Multiplier = 1.0. No change.
- All 589 existing tests use `ContextBudget` with default values for these properties.
- The `BuildPipeline` test helper constructs `new ContextBudget(maxTokens, targetTokens, outputReserve)` which uses defaults for both properties.

## Test Strategy

### ReservedSlots Tests

1. **Basic reduction test:** Create budget with `ReservedSlots = { Message: 200 }`, target 1000. Items totaling 900 tokens. Verify fewer items selected than without reservation (effective target = 800).
2. **Multiple kinds:** ReservedSlots with 2+ kinds, verify cumulative subtraction.
3. **Reserved exceeds remaining budget:** ReservedSlots sum > targetTokens after pinned subtraction. Effective target clamps to 0 — only zero-token items selected.
4. **Default (empty) is no-op:** Verify existing behavior unchanged.

### EstimationSafetyMarginPercent Tests

1. **10% margin:** Budget target 1000, margin 10%. Effective target = 900. Verify selection fits 900.
2. **100% margin:** Everything zeroed, only zero-token items selected.
3. **0% margin (default):** No change from current behavior.
4. **Combined with ReservedSlots:** Both active simultaneously — verify correct order of operations.

### Existing Test Compatibility

All existing tests must pass unchanged. The `BuildPipeline` helper uses default values, so no existing test exercises these new code paths.

## Common Pitfalls

1. **Forgetting the streaming path.** `ExecuteStreamAsync` has its own budget computation (lines 491-495) that also needs updating. Easy to miss because it's a separate method.
2. **Breaking ContextBudget constructor invariant.** The adjusted budget constructor requires `targetTokens <= maxTokens`. After applying both adjustments, must ensure this holds. Always clamp after each adjustment step.
3. **Integer overflow with large reserved slots.** Sum of `ReservedSlots.Values` could overflow `int` if someone configures absurd values. Use checked arithmetic or validate at a higher level. In practice, individual values are validated >= 0 and the budget's `MaxTokens` is the ceiling, so overflow is unlikely but worth a defensive `Math.Max(0, ...)` clamp.
4. **Modifying pinned budget validation.** The pinned items check on line 138-143 should NOT include reserved slots or safety margin. Pinned items are validated against the hard ceiling (`maxTokens - outputReserve`). The reservations affect only what the slicer receives.
5. **Not updating the spec.** The spec explicitly says these fields are not consumed by the pipeline. The spec must be updated to reflect the new behavior, or this creates a conformance contradiction.

## Don't Hand-Roll

- **Nothing external needed.** This is pure arithmetic adjustment to existing pipeline code. No new dependencies, no new patterns.

## Files to Modify

| File | Change |
|------|--------|
| `src/Wollax.Cupel/CupelPipeline.cs` | Budget computation in `ExecuteCore()` (~line 240) and `ExecuteStreamAsync()` (~line 491) |
| `tests/Wollax.Cupel.Tests/Pipeline/CupelPipelineTests.cs` | New tests for ReservedSlots and EstimationSafetyMarginPercent pipeline behavior |
| `.planning/REQUIREMENTS.md` | Already updated — PKG-02, PKG-03, PKG-05 show `[x]` (audit says checkboxes were `[ ]` but current file shows `[x]`; verify at implementation time) |
| `spec/src/data-model/context-budget.md` | Update semantics for `reservedSlots` and `estimationSafetyMarginPercent` |
| `spec/src/pipeline/slice.md` | Update conformance note on line 68 about which fields are forwarded |

## REQUIREMENTS.md Status

**Confidence: HIGH** (read directly)

The audit item says PKG-02, PKG-03, PKG-05 show `[ ] planned` but the current `REQUIREMENTS.md` already has `[x]` for all three (lines 71-74). The traceability table at the bottom also shows `complete` status. This gap appears to have been already fixed. Verify at implementation time — if already correct, this is a no-op.

## Code Examples

### Budget computation in ExecuteCore (target state)

```csharp
// SLICE: create adjusted budget and slice
var reservedTokens = 0;
if (_budget.ReservedSlots.Count > 0)
{
    foreach (var kvp in _budget.ReservedSlots)
        reservedTokens += kvp.Value;
}

var effectiveMax = Math.Max(0, _budget.MaxTokens - _budget.OutputReserve - pinnedTokens - reservedTokens);
var effectiveTarget = Math.Max(0, _budget.TargetTokens - pinnedTokens - reservedTokens);
effectiveTarget = Math.Min(effectiveTarget, effectiveMax);

if (_budget.EstimationSafetyMarginPercent > 0)
{
    var marginMultiplier = 1.0 - (_budget.EstimationSafetyMarginPercent / 100.0);
    effectiveMax = (int)(effectiveMax * marginMultiplier);
    effectiveTarget = (int)(effectiveTarget * marginMultiplier);
    effectiveTarget = Math.Min(effectiveTarget, effectiveMax);
}

var adjustedBudget = new ContextBudget(
    maxTokens: effectiveMax,
    targetTokens: effectiveTarget);
```

### Test pattern for ReservedSlots

```csharp
[Test]
public async Task ReservedSlots_ReduceEffectiveBudget()
{
    // 5 items at 200 tokens each = 1000 total
    // Target = 1000 (all fit normally), but ReservedSlots takes 400
    // Effective target = 600, so only 3 items fit
    var items = new List<ContextItem>();
    for (var i = 0; i < 5; i++)
        items.Add(CreateItem($"item-{i}", tokens: 200, futureRelevanceHint: (5 - i) / 5.0));

    var budget = new ContextBudget(
        maxTokens: 2000,
        targetTokens: 1000,
        reservedSlots: new Dictionary<ContextKind, int>
        {
            [ContextKind.SystemPrompt] = 200,
            [ContextKind.Memory] = 200
        });

    var pipeline = CupelPipeline.CreateBuilder()
        .WithBudget(budget)
        .WithScorer(new ReflexiveScorer())
        .Build();

    var result = pipeline.Execute(items);

    // Effective target = 1000 - 400 = 600, so at most 3 items (600 / 200)
    await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(600);
}
```

### Test pattern for EstimationSafetyMarginPercent

```csharp
[Test]
public async Task EstimationSafetyMargin_ReducesEffectiveBudget()
{
    // 10 items at 100 tokens each = 1000 total
    // Target = 1000, margin = 10% => effective target = 900
    var items = new List<ContextItem>();
    for (var i = 0; i < 10; i++)
        items.Add(CreateItem($"item-{i}", tokens: 100, futureRelevanceHint: (10 - i) / 10.0));

    var budget = new ContextBudget(
        maxTokens: 2000,
        targetTokens: 1000,
        estimationSafetyMarginPercent: 10);

    var pipeline = CupelPipeline.CreateBuilder()
        .WithBudget(budget)
        .WithScorer(new ReflexiveScorer())
        .Build();

    var result = pipeline.Execute(items);

    // Effective target = 900, so at most 9 items (900 / 100)
    await Assert.That(result.TotalTokens).IsLessThanOrEqualTo(900);
}
```
