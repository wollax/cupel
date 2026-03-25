---
estimated_steps: 8
estimated_files: 3
---

# T02: Implement CountConstrainedKnapsackSlice and extend pipeline wiring

**Slice:** S02 — CountConstrainedKnapsackSlice — .NET implementation
**Milestone:** M009

## Description

Implement `CountConstrainedKnapsackSlice.cs` and extend the 3 pipeline wiring checks in `CupelPipeline.cs` so all 5 integration tests pass through `CupelPipeline.DryRun()`.

The class is a faithful port of the Rust implementation. Phase 1 (count-satisfy) and Phase 3 (cap-enforce seeded from Phase 1 counts) are copied verbatim from `CountQuotaSlice.cs` with field-name adaptations. Phase 2 calls `_knapsack.Slice()` on the stored `KnapsackSlice` field, then sorts the output by score descending before Phase 3 (D180). The pipeline receives 3 parallel extensions matching the 3 existing `CountQuotaSlice` checks.

## Steps

1. Create `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs`. Declare `public sealed class CountConstrainedKnapsackSlice : ISlicer, IQuotaPolicy` in namespace `Wollax.Cupel.Slicing`. Fields: `private readonly KnapsackSlice _knapsack`, `private readonly IReadOnlyList<CountQuotaEntry> _entries`, `private readonly ScarcityBehavior _scarcity`. Properties: `public IReadOnlyList<CountRequirementShortfall> LastShortfalls { get; private set; } = []` and `internal IReadOnlyList<CountQuotaEntry> Entries => _entries`.

2. Implement the constructor: `public CountConstrainedKnapsackSlice(IReadOnlyList<CountQuotaEntry> entries, KnapsackSlice knapsack, ScarcityBehavior scarcity = ScarcityBehavior.Degrade)`. Use `ArgumentNullException.ThrowIfNull(entries)` and `ArgumentNullException.ThrowIfNull(knapsack)`. No KnapsackSlice guard (unlike `CountQuotaSlice` — accepting `KnapsackSlice` is the whole point of this class). Assign fields.

3. Implement `GetConstraints()`: identical to `CountQuotaSlice.GetConstraints()` — returns `QuotaConstraint` list with `QuotaConstraintMode.Count` for each entry.

4. Implement Phase 1 of `Slice()`: copy the Phase 1 block verbatim from `CountQuotaSlice.Slice()`. Group candidates by kind, sort per-kind by score descending, commit top-N per entry with `RequireCount > 0`, accumulate `preAllocatedTokens`, build `committedSet`, populate `selectedCount` from Phase 1 satisfied counts. On scarcity: `_scarcity == ScarcityBehavior.Throw` throws `InvalidOperationException` with message `$"CountConstrainedKnapsackSlice: candidate pool for kind '{entry.Kind}' has {satisfied} items but RequireCount is {entry.RequireCount}."`. Set `LastShortfalls = shortfalls`.

5. Implement Phase 2: Build `scoreByContent` dictionary (`Dictionary<string, double>`) from `residual` items mapping `Content → Score` before calling the knapsack. Call `_knapsack.Slice(residual, residualBudget, traceCollector)`. After getting Phase 2 results, sort them by score descending using `scoreByContent` lookup — use `b.Score.CompareTo(a.Score)` but derive score from the dictionary when the `ScoredItem` score is unavailable (note: `IReadOnlyList<ContextItem>` is returned, so use the `scoreByContent` dict). Use `innerSelected.OrderByDescending(item => scoreByContent.GetValueOrDefault(item.Content, 0.0)).ToList()` before Phase 3.

6. Implement Phase 3: copy the cap-enforcement block verbatim from `CountQuotaSlice.Slice()` Phase 3. Iterate `innerSelected` (now score-sorted). For each item, check `entryByKind` for a cap; if `count >= entry.CapCount`, exclude (record trace event); otherwise add to result and increment `selectedCount[kind]`. Add Phase 1 `committed` items first, then filtered Phase 2 items.

7. Open `src/Wollax.Cupel/CupelPipeline.cs`. Make 3 changes:
   - **Change 1 — shortfall wiring (~line 349):** After the existing `if (_slicer is CountQuotaSlice countQuotaSlicer ...)` block, add `else if (_slicer is CountConstrainedKnapsackSlice ccksShortfall && reportBuilder is not null && ccksShortfall.LastShortfalls.Count > 0) { reportBuilder.SetCountRequirementShortfalls(ccksShortfall.LastShortfalls); }`
   - **Change 2 — selectedKindCounts construction (~line 378):** Change `if (reportBuilder is not null && _slicer is CountQuotaSlice)` to `if (reportBuilder is not null && (_slicer is CountQuotaSlice || _slicer is CountConstrainedKnapsackSlice))`
   - **Change 3 — cap-classification (~line 408):** After the existing `if (selectedKindCounts is not null && _slicer is CountQuotaSlice cqs)` block, add `else if (selectedKindCounts is not null && _slicer is CountConstrainedKnapsackSlice ccks)` with parallel logic using `ccks.Entries.FirstOrDefault(e => e.Kind == kind)`.

8. Run `dotnet test --filter "CountConstrainedKnapsack"` and confirm all 5 pass. Run `dotnet test` and confirm full suite green. Run `dotnet build` and confirm 0 warnings.

## Must-Haves

- [ ] `CountConstrainedKnapsackSlice` implements `ISlicer` and `IQuotaPolicy`.
- [ ] Constructor accepts `IReadOnlyList<CountQuotaEntry>` and `KnapsackSlice` (no guard rejecting KnapsackSlice).
- [ ] Phase 2 output is sorted by score descending before Phase 3 cap loop (D180).
- [ ] Phase 3 `selectedCount` is seeded from Phase 1 committed counts, not zero (D181).
- [ ] `internal IReadOnlyList<CountQuotaEntry> Entries` exposed for pipeline cap-classification.
- [ ] All 3 pipeline wiring changes applied in `CupelPipeline.cs`.
- [ ] `dotnet test --filter "CountConstrainedKnapsack"` — 5 pass, 0 fail.
- [ ] `dotnet test` — full suite green.
- [ ] `dotnet build` — 0 warnings.

## Verification

```bash
dotnet test --filter "CountConstrainedKnapsack"
# Expected: 5 passed, 0 failed

dotnet test
# Expected: all existing tests + 5 new ones pass, 0 failed

dotnet build 2>&1 | grep -E "warning|error"
# Expected: no output (0 warnings, 0 errors)
```

Specific assertions to verify manually by reading test output:
- `CapExclusion_TwoCapExcluded`: `Included.Count == 2` with "tool-a" and "tool-b" (not "tool-c" or "tool-d"), `CountCapExceeded count == 2` — this confirms D180 (score-descending sort) is working correctly.
- `ScarcityDegrade_ShortfallRecorded`: `CountRequirementShortfalls.Count == 1` with `RequiredCount=3, SatisfiedCount=1` — confirms D087-equivalent shortfall wiring through pipeline.

## Observability Impact

- Signals added/changed: `result.Report.CountRequirementShortfalls` now populated for `CountConstrainedKnapsackSlice` (pipeline Change 1); `result.Report.Excluded` entries with `CountCapExceeded` reason now classified for `CountConstrainedKnapsackSlice` (pipeline Change 3)
- How a future agent inspects this: `dotnet test --filter "CountConstrainedKnapsack" -- --verbosity detailed` surfaces per-test assertion failures with field names and actual/expected values
- Failure state exposed: D180 regression visible as `CapExclusion_TwoCapExcluded` test failure (wrong items survive cap); D181 regression visible as too many items passing Phase 3 cap check

## Inputs

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — Phase 1 and Phase 3 algorithm to copy
- `src/Wollax.Cupel/KnapsackSlice.cs` — Phase 2 delegate (class stored by reference, calls `Slice()`)
- `src/Wollax.Cupel/CupelPipeline.cs` lines 347–420 — 3 checks to extend
- `tests/Wollax.Cupel.Tests/Pipeline/CountConstrainedKnapsackTests.cs` — Failing tests from T01 that this task must make pass
- S01 Summary D180/D181 — Phase 2 re-sort and Phase 3 seeding decisions

## Expected Output

- `src/Wollax.Cupel/Slicing/CountConstrainedKnapsackSlice.cs` — New: full implementation (~250 lines, matching `CountQuotaSlice` structure with Phase 2 knapsack delegation and score re-sort)
- `src/Wollax.Cupel/CupelPipeline.cs` — Modified: 3 pipeline check extensions for `CountConstrainedKnapsackSlice`
- All 5 tests in `CountConstrainedKnapsackTests.cs` passing; `dotnet build` 0 warnings
