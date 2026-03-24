---
estimated_steps: 4
estimated_files: 1
---

# T02: .NET composition integration test

**Slice:** S03 — Integration proof + summaries
**Milestone:** M006

## Description

Write a new .NET integration test class that chains `CountQuotaSlice(inner: QuotaSlice(inner: GreedySlice))` and runs a real `DryRun()` call via `CupelPipeline`. This mirrors the Rust T01 test in .NET and proves the composition works without exceptions or constraint conflicts. The `Run()` static helper from `CountQuotaIntegrationTests.cs` is the model — adapted to swap the inner slicer from `GreedySlice` to `QuotaSlice(GreedySlice)`.

No production code is written in this task. This is exclusively a new test file.

Key constraints from research and decisions:
- `PipelineBuilder.Build()` throws if no scorer is registered — always include `WithScorer(new ReflexiveScorer())` (D142)
- `.LastShortfalls` is `public` but only use `DryRun()` report fields to inspect outcomes (D087)
- Solution file is `Cupel.slnx` — use `dotnet test --solution Cupel.slnx` for full-solution runs (S02 forward intelligence)

## Steps

1. Create `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs`. Add using statements matching `CountQuotaIntegrationTests.cs`: `TUnit.Core`, `TUnit.Assertions`, `TUnit.Assertions.Extensions`, `Wollax.Cupel.Diagnostics`, `Wollax.Cupel.Scoring`, `Wollax.Cupel.Slicing`.

2. Define the `CountQuotaCompositionTests` class with:
   - `Item()` helper: identical to the one in `CountQuotaIntegrationTests.cs`
   - `Run()` static helper that takes `countEntries`, `quotaSet` (a `QuotaSet` built via `QuotaBuilder`), `items`, `budgetTokens`, and builds: `CupelPipeline.CreateBuilder().WithBudget(new ContextBudget(budgetTokens, budgetTokens)).WithScorer(new ReflexiveScorer()).WithSlicer(new CountQuotaSlice(new QuotaSlice(new GreedySlice(), quotaSet), countEntries)).Build()` then calls `pipeline.DryRun(items)`.
   - Note: `QuotaSlice(ISlicer inner, QuotaSet quotas)` — inner first, then the `QuotaSet`; `QuotaSet` must be built via `new QuotaBuilder().Require(kind, pct).Cap(kind, pct).Build()` since `QuotaSet` has no public constructor.

3. Write one `[Test]` method `CompositionWithQuotaSlice_CountCapAndPercentageConstraintsBothActive`:
   - Items: 3 ToolOutput (100 tokens, scores 0.9/0.7/0.5) + 2 Message (100 tokens, scores 0.8/0.6)
   - Budget: 400 tokens
   - Count entry: `new CountQuotaEntry(ContextKind.ToolOutput, requireCount: 1, capCount: 2)`
   - Build quota set: `new QuotaBuilder().Require(ContextKind.ToolOutput, 10).Cap(ContextKind.ToolOutput, 60).Build()` (10% require, 60% cap)
   - Call `Run(countEntries, quotaSet, items, budgetTokens: 400)`
   - Assert: `result.Report!.Included.Count(i => i.Item.Kind == ContextKind.ToolOutput) <= 2` (count cap holds)
   - Assert: `result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) >= 1` (cap exclusion visible)
   - Assert: `result.Report.CountRequirementShortfalls.Count == 0` (require=1 satisfied — at least 1 tool item included)

4. Run `dotnet test --solution Cupel.slnx 2>&1 | grep -E "CountQuotaComposition|failed|error"` to verify the new test passes and full-solution remains at 0 failures.

## Must-Haves

- [ ] `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` exists with at least one `[Test]` method
- [ ] The `Run()` helper uses `new CountQuotaSlice(new QuotaSlice(new GreedySlice(), quotaEntries), countEntries)` — count quota wrapping percentage quota
- [ ] `PipelineBuilder` includes `WithScorer(new ReflexiveScorer())`
- [ ] `pipeline.DryRun(items)` completes without exception
- [ ] `result.Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) >= 1` assertion passes
- [ ] `result.Report.Included.Count(i => i.Item.Kind == ContextKind.ToolOutput) <= 2` assertion passes
- [ ] `dotnet test --solution Cupel.slnx` exits 0 with 0 failures
- [ ] `dotnet build Cupel.slnx` exits 0 with 0 warnings

## Verification

- `dotnet test --solution Cupel.slnx 2>&1 | grep "CountQuotaComposition"` → shows test passing
- `dotnet test --solution Cupel.slnx 2>&1 | grep "failed: 0"` → confirms 0 failures
- `dotnet build Cupel.slnx 2>&1 | grep -E "error|warning" | wc -l` → returns 0

## Observability Impact

- Signals added/changed: None — test-only file; no production code changed
- How a future agent inspects this: `dotnet test --solution Cupel.slnx` output includes test names and failure details including the specific assertion message
- Failure state exposed: Exception type and message visible in test output; `ExclusionReason.CountCapExceeded` vs `BudgetExceeded` mismatch produces a clear count assertion failure

## Inputs

- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — copy the `Item()` helper and `Run()` pattern; adapt `Run()` to accept a `QuotaSet` and nest `QuotaSlice(innerSlicer, quotaSet)`
- `tests/Wollax.Cupel.Tests/Diagnostics/QuotaUtilizationTests.cs` — shows `new QuotaBuilder().Require(kind, pct).Cap(kind, pct).Build()` pattern for constructing `QuotaSet`; `QuotaSet` has no public constructor
- S02 summary forward intelligence: `ReflexiveScorer` is mandatory; `Cupel.slnx` is the solution file; TUnit's `--filter` uses tree filters

## Expected Output

- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaCompositionTests.cs` — new test file (~70 lines) with one passing integration test
- `dotnet test --solution Cupel.slnx` total count rises by 1
