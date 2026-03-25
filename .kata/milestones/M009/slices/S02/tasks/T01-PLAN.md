---
estimated_steps: 5
estimated_files: 2
---

# T01: Write failing integration tests and PublicAPI.Unshipped.txt entries

**Slice:** S02 — CountConstrainedKnapsackSlice — .NET implementation
**Milestone:** M009

## Description

Establish the red baseline before implementing the class. Write 5 integration tests in `CountConstrainedKnapsackTests.cs` — one per TOML conformance vector — each asserting the correct included items, shortfall count, and cap-exclusion count via `CupelPipeline.DryRun()`. Add all required `PublicAPI.Unshipped.txt` entries for `CountConstrainedKnapsackSlice` so the RS0016 analyzer enforces the expected public surface from the start.

The tests should compile-fail deterministically (type not found) after this task, proving that the implementation task has clear, mechanically verifiable targets.

## Steps

1. Create `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs`. Add the same `Item()` helper as `CountQuotaIntegrationTests.cs` (static method returning a `ContextItem` with Content, Tokens, FutureRelevanceHint, Kind). Add a `DryRun()` helper that builds a `CupelPipeline` with `ReflexiveScorer`, a given `CountConstrainedKnapsackSlice`, and the specified budget, then calls `pipeline.DryRun(items)`.

2. Write test `Baseline_AllItemsIncluded_NoShortfalls_NoCap`: items = [tool-a(100t,0.9), tool-b(100t,0.7), msg-x(100t,0.5)], entries = [tool: require=2, cap=4], budget=1000, bucketSize=100. Assert Included.Count==3, Included contains "tool-a"/"tool-b"/"msg-x", CountRequirementShortfalls.Count==0, CountCapExceeded count==0.

3. Write test `CapExclusion_TwoCapExcluded`: items = [tool-a(100t,0.9), tool-b(100t,0.8), tool-c(100t,0.7), tool-d(100t,0.6)], entries = [tool: require=1, cap=2], budget=600, bucketSize=100. Assert Included.Count==2, Included contains "tool-a"/"tool-b", CountCapExceeded count==2.

4. Write test `ScarcityDegrade_ShortfallRecorded`: items = [tool-a(100t,0.9)], entries = [tool: require=3, cap=5], budget=500, bucketSize=100, scarcity=Degrade. Assert Included.Count==1, Included contains "tool-a", CountRequirementShortfalls.Count==1, CountRequirementShortfalls[0].Kind==tool, CountRequirementShortfalls[0].RequiredCount==3, CountRequirementShortfalls[0].SatisfiedCount==1.

5. Write test `TagNonExclusive_MultipleKindsRequiredIndependently`: items = [item-tool(100t,0.9,kind=tool), item-memory(100t,0.8,kind=memory), item-extra(100t,0.5,kind=tool)], entries = [tool: require=1, cap=4; memory: require=1, cap=4], budget=1000, bucketSize=100. Assert Included.Count==3, CountRequirementShortfalls.Count==0, CountCapExceeded count==0.

6. Write test `RequireAndCap_NoResidualExcluded`: items = [tool-a(100t,0.9), tool-b(100t,0.7), msg-s(50t,0.8), msg-m(150t,0.6), msg-l(200t,0.4)], entries = [tool: require=2, cap=2], budget=1000, bucketSize=1. Assert Included.Count==5, CountRequirementShortfalls.Count==0, CountCapExceeded count==0.

7. Add 5 entries to `src/Wollax.Cupel/PublicAPI.Unshipped.txt`:
   ```
   Wollax.Cupel.Slicing.CountConstrainedKnapsackSlice
   Wollax.Cupel.Slicing.CountConstrainedKnapsackSlice.CountConstrainedKnapsackSlice(System.Collections.Generic.IReadOnlyList<Wollax.Cupel.Slicing.CountQuotaEntry!>! entries, Wollax.Cupel.KnapsackSlice! knapsack, Wollax.Cupel.Slicing.ScarcityBehavior scarcity = Wollax.Cupel.Slicing.ScarcityBehavior.Degrade) -> void
   Wollax.Cupel.Slicing.CountConstrainedKnapsackSlice.LastShortfalls.get -> System.Collections.Generic.IReadOnlyList<Wollax.Cupel.Diagnostics.CountRequirementShortfall!>!
   Wollax.Cupel.Slicing.CountConstrainedKnapsackSlice.GetConstraints() -> System.Collections.Generic.IReadOnlyList<Wollax.Cupel.Slicing.QuotaConstraint!>!
   Wollax.Cupel.Slicing.CountConstrainedKnapsackSlice.Slice(System.Collections.Generic.IReadOnlyList<Wollax.Cupel.ScoredItem>! scoredItems, Wollax.Cupel.ContextBudget! budget, Wollax.Cupel.Diagnostics.ITraceCollector! traceCollector) -> System.Collections.Generic.IReadOnlyList<Wollax.Cupel.ContextItem!>!
   ```

## Must-Haves

- [ ] `CountConstrainedKnapsackTests.cs` exists with exactly 5 `[Test]` methods, each matching a TOML vector.
- [ ] Each test asserts Included contents, `CountRequirementShortfalls.Count`, and `ExclusionReason.CountCapExceeded` count from `result.Report`.
- [ ] `PublicAPI.Unshipped.txt` has entries for the class declaration, constructor, `LastShortfalls`, `GetConstraints()`, and `Slice()`.
- [ ] `dotnet build` fails with CS0246 (type not found), not due to test logic errors — confirming the test code itself is correct, just the implementation is absent.

## Verification

- `dotnet build 2>&1 | grep -E "CS0246|error"` — must show "CountConstrainedKnapsackSlice" as the missing type.
- The test file compiles once the class exists (no syntax errors in tests themselves).

## Observability Impact

- Signals added/changed: None (test scaffolding only)
- How a future agent inspects this: `dotnet test --filter "CountConstrainedKnapsack" -- --verbosity detailed` — shows per-test failure with assertion messages
- Failure state exposed: RS0016 build failures name missing PublicAPI entries explicitly; CS0246 names the missing class

## Inputs

- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — Template for `Item()` helper, `DryRun()` helper, and assertion pattern
- `crates/cupel/conformance/required/slicing/count-constrained-knapsack-*.toml` — 5 vectors providing exact item data, budgets, and expected counts

## Expected Output

- `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs` — New file with 5 failing integration tests
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — 5 new entries for `CountConstrainedKnapsackSlice`
