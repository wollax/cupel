---
estimated_steps: 8
estimated_files: 5
---

# T03: Patterns 8–13 + Test Project + Consumption Test Wiring

**Slice:** S04 — Core Analytics + Cupel.Testing Package
**Milestone:** M003

## Description

Complete the 13-pattern vocabulary by implementing patterns 8–13 (Aggregate, Budget, Coverage, Ordering groups). Create the `tests/Wollax.Cupel.Testing.Tests/` test project with ≥13 TUnit tests proving each pattern behaves correctly. Wire `Wollax.Cupel.Testing` into the consumption tests project to retire the standalone installability risk. Run full `dotnet test` and `cargo test --all-targets` to confirm zero failures.

## Steps

1. **Add patterns 8–13 to `SelectionReportAssertionChain.cs`**:

   **Pattern 8 — `HaveAtLeastNExclusions(int n)`**: `if (_report.Excluded.Count < n)` → throw with: `$"HaveAtLeastNExclusions({n}) failed: expected at least {n} excluded items, but Excluded had {_report.Excluded.Count}."`

   **Pattern 9 — `ExcludedItemsAreSortedByScoreDescending()`**: Loop adjacent pairs `for (int i = 0; i < _report.Excluded.Count - 1; i++)`: `if (_report.Excluded[i].Score < _report.Excluded[i + 1].Score)` → throw with: `$"ExcludedItemsAreSortedByScoreDescending failed: item at index {i + 1} (score={_report.Excluded[i + 1].Score:F6}) is higher than item at index {i} (score={_report.Excluded[i].Score:F6}). Expected non-increasing scores."`

   **Pattern 10 — `HaveBudgetUtilizationAbove(double threshold, ContextBudget budget)`**: `var includedTokens = _report.Included.Sum(i => (long)i.Item.Tokens); var actual = includedTokens / (double)budget.MaxTokens; if (actual < threshold)` → throw with: `$"HaveBudgetUtilizationAbove({threshold}) failed: computed utilization was {actual:F6} (includedTokens={includedTokens}, budget.MaxTokens={budget.MaxTokens})."`; use `using Wollax.Cupel;` for `ContextBudget`

   **Pattern 11 — `HaveKindCoverageCount(int n)`**: `var distinctKinds = _report.Included.Select(i => i.Item.Kind).Distinct().ToList(); if (distinctKinds.Count < n)` → throw with: `$"HaveKindCoverageCount({n}) failed: expected at least {n} distinct ContextKind values in Included, but found {distinctKinds.Count}: [{string.Join(", ", distinctKinds)}]."`

   **Pattern 12 — `PlaceItemAtEdge(Func<IncludedItem, bool> predicate)`**: `var included = _report.Included; if (included.Count == 0 || !included.Any(predicate)) { throw new SelectionReportAssertionException("PlaceItemAtEdge failed: no item in Included matched the predicate."); } var idx = included.Select((item, i) => (item, i)).First(t => predicate(t.item)).i; var last = included.Count - 1; if (idx != 0 && idx != last)` → throw with: `$"PlaceItemAtEdge failed: item matching predicate was at index {idx} (not at edge). Edge positions: 0 and {last}. Included had {included.Count} items."`

   **Pattern 13 — `PlaceTopNScoredAtEdges(int n)`**: If `n == 0` return this. If `n > _report.Included.Count` → throw with count mismatch message. Identify top-N items by score (sorted desc). Enumerate expected edge positions as `0, count-1, 1, count-2, ...` (first n positions). Verify each top-N item is at one of those positions. Count failures. If `failCount > 0` → throw with the spec message format including `topItems` and `edgePositions`.
   Implementation detail: `var topNScores = _report.Included.OrderByDescending(i => i.Score).Take(n).ToHashSet(); var edgePositions = new List<int>(); for (int lo = 0, hi = _report.Included.Count - 1; edgePositions.Count < n; lo++, hi--) { edgePositions.Add(lo); if (lo != hi && edgePositions.Count < n) edgePositions.Add(hi); }` Then verify each item at an edge position has a score ≥ the min score in topNScores.

2. **Update `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt`**: Add entries for all 6 new pattern methods. Build to confirm 0 RS0016 errors.

3. **Create `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj`**: Copy structure from `tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — `OutputType=Exe`, `IsPackable=false`, `PackageReference Include="TUnit"`, then `ProjectReference` to `../../src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` AND `../../src/Wollax.Cupel/Wollax.Cupel.csproj` (needed to construct SelectionReport in tests).

4. **Create `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs`**: Implement ≥13 TUnit test methods — one happy-path (passing assertion) + one failure-path (catches `SelectionReportAssertionException`) per pattern. Use a helper `MakeReport(...)` method that builds a `SelectionReport` using `DiagnosticTraceCollector` or constructs one directly using record init syntax. Cover these scenarios at minimum:
   - Pattern 1: report has a Message item → passes; report has no Message → throws with error containing kind name
   - Pattern 2: included item with specific content → passes; empty included → throws
   - Pattern 3: exactly 2 of kind → passes; 3 of kind when expecting 2 → throws with counts
   - Pattern 4: BudgetExceeded in excluded → passes; empty excluded → throws with reason list
   - Pattern 5: predicate matches + correct reason → passes; predicate matches wrong reason → throws showing matched reasons
   - Pattern 6 (degenerate): item with BudgetExceeded → passes; no BudgetExceeded → throws
   - Pattern 7: excluded has different kind → passes; excluded has matching kind → throws with first item details
   - Pattern 8: 2 exclusions, assert ≥2 → passes; 1 exclusion, assert ≥2 → throws with counts
   - Pattern 9: excluded sorted desc → passes; unsorted → throws with indices
   - Pattern 10: utilization above threshold → passes; utilization below → throws with computed value
   - Pattern 11: 2 distinct kinds, assert ≥2 → passes; 1 kind, assert ≥2 → throws with actual kinds
   - Pattern 12: predicate matches edge item → passes; predicate matches non-edge → throws with index
   - Pattern 13: top-2 at edges → passes; top-2 not at edges → throws with failure details
   Construct test data using in-line SelectionReport record construction (no running pipeline needed):
   ```csharp
   var report = new SelectionReport {
       Events = [],
       Included = [new IncludedItem { Item = someItem, Score = 0.9, Reason = InclusionReason.Scored }],
       Excluded = [...],
       TotalCandidates = ...,
       TotalTokensConsidered = ...
   };
   ```

5. **Run `dotnet test tests/Wollax.Cupel.Testing.Tests/`**: Must show ≥13 tests passing, 0 failed. Fix any failures before proceeding.

6. **Wire consumption tests**: Edit `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — add: `<PackageReference Include="Wollax.Cupel.Testing" Version="*-*" />` inside the existing `<ItemGroup>` with other package references. Note: this project has `ManagePackageVersionsCentrally=false` — the version must be specified inline, NOT in `Directory.Packages.props`.

7. **Pack and run consumption tests**: Run `dotnet pack src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj --output ./nupkg` first (required before consumption tests can reference the package by version `*-*`). Then `dotnet test tests/Wollax.Cupel.ConsumptionTests/`. If the consumption test project needs a reference to use `Should()`, add a minimal smoke test to `ConsumptionTests` that calls `report.Should()` to prove it compiles.

8. **Final verification**: Run `cargo test --all-targets` and `dotnet test` (full suite). Both must exit 0.

## Must-Haves

- [ ] Patterns 8–13 implemented in `SelectionReportAssertionChain.cs`; all return `this`; all throw `SelectionReportAssertionException` on failure
- [ ] Error messages for patterns 8–13 match spec format exactly (verified by failure-path tests)
- [ ] `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` exists; references both `Wollax.Cupel.Testing` and `Wollax.Cupel`
- [ ] `AssertionChainTests.cs` has ≥13 TUnit test methods; all pass
- [ ] `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` references `Wollax.Cupel.Testing Version="*-*"`
- [ ] `dotnet pack src/Wollax.Cupel.Testing/...` produces a `.nupkg` file
- [ ] `dotnet test` exits 0 (all projects, all tests)
- [ ] `cargo test --all-targets` exits 0

## Verification

- `dotnet test tests/Wollax.Cupel.Testing.Tests/ 2>&1 | tail -5` → shows "passed" count ≥13, failed=0
- `grep "Wollax.Cupel.Testing" tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` → match
- `ls ./nupkg/Wollax.Cupel.Testing.*.nupkg 2>/dev/null | wc -l` → 1
- `dotnet test 2>&1 | tail -10` → shows total pass count, failed=0
- `cargo test --all-targets 2>&1 | tail -5` → all tests passed
- `grep -c "public.*SelectionReportAssertionChain" src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` → ≥13 (one per assertion method)

## Observability Impact

- Signals added/changed: test output is now the primary signal; `SelectionReportAssertionException.Message` carries structured diagnostics for each failed assertion; test runner shows which pattern failed and what the actual vs expected values were
- How a future agent inspects this: `dotnet test tests/Wollax.Cupel.Testing.Tests/ --verbosity normal` to see individual test results; `grep "Assert\|Exception" tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` to find test coverage gaps
- Failure state exposed: `SelectionReportAssertionException` message is the primary failure signal — it must include enough context (actual values, counts, score values) for a test author to diagnose without a debugger

## Inputs

- T02 output: `SelectionReportAssertionChain.cs` (patterns 1–7 already implemented), `Wollax.Cupel.Testing.csproj`, exception class, `SelectionReportExtensions.cs`
- T01 output: `BudgetUtilization` extension method available from `Wollax.Cupel` (can use for pattern 10 implementation, or recompute inline — either is correct)
- `tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — template for new test csproj structure
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — existing file to edit (ManagePackageVersionsCentrally=false — version inline)
- `spec/src/testing/vocabulary.md` — exact spec for patterns 8–13 error message formats
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — SelectionReport record fields for test construction

## Expected Output

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — modified; +6 patterns added (~100 additional lines)
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — modified; ~6 new entries for patterns 8–13
- `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` — new; ~20 lines
- `tests/Wollax.Cupel.Testing.Tests/AssertionChainTests.cs` — new; ~200 lines; ≥13 TUnit tests
- `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` — modified; +1 PackageReference line
