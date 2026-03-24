---
estimated_steps: 6
estimated_files: 2
---

# T01: Wire pipeline shortfall and cap-exclusion reporting

**Slice:** S02 — .NET CountQuotaSlice — audit, complete, and test
**Milestone:** M006

## Description

The two structural wiring gaps from the research audit: (1) `CountQuotaSlice.LastShortfalls` never reaches `SelectionReport.CountRequirementShortfalls` because `CupelPipeline.Execute()` never reads it after `_slicer.Slice()`; (2) cap-excluded items always receive `ExclusionReason.BudgetExceeded` because the re-association loop has no cap-classification logic. Both fixes are surgical changes to two files.

**Implementation constraints:**
- `ReportBuilder` is `internal sealed` — adding a setter is safe and has no public API impact
- `CountQuotaSlice.LastShortfalls` is only populated after `Slice()` is called — read it after the call, not before (D087)
- Cap-excluded vs budget-excluded classification rule: build per-kind count from `slicedItems` first; for each item not in `slicedSet`: if `item.Tokens > adjustedBudget.TargetTokens` → `BudgetExceeded` (budget constraint wins); else if item's kind has a `CountQuotaEntry` and `selectedKindCounts[kind] >= entry.CapCount` → `CountCapExceeded`; else `BudgetExceeded`
- The `_slicer is CountQuotaSlice cqs` cast pattern mirrors the existing `_slicer is QuotaSlice quotaSlicer` cast at line ~361
- All new code in both files requires XML documentation (`dotnet build` must remain at 0 warnings)
- `ReferenceEqualityComparer.Instance` is already used for `slicedSet` — confirm same-reference items come back from `CountQuotaSlice.Slice()`

## Steps

1. **Add `SetCountRequirementShortfalls` to `ReportBuilder`**: Add a private `IReadOnlyList<CountRequirementShortfall> _countRequirementShortfalls = [];` field; add `public void SetCountRequirementShortfalls(IReadOnlyList<CountRequirementShortfall> shortfalls) => _countRequirementShortfalls = shortfalls;` with XML doc; update `Build()` to include `CountRequirementShortfalls = _countRequirementShortfalls` in the `SelectionReport` initializer.

2. **Wire `LastShortfalls` in `CupelPipeline.Execute()`**: Immediately after the `_slicer.Slice()` call (line ~305) and before the re-association loop, add:
   ```csharp
   if (_slicer is CountQuotaSlice countQuotaSlicer
       && reportBuilder is not null
       && countQuotaSlicer.LastShortfalls.Count > 0)
   {
       reportBuilder.SetCountRequirementShortfalls(countQuotaSlicer.LastShortfalls);
   }
   ```

3. **Reconstruct per-kind counts from `slicedItems`**: Before the `slicedSet` construction loop at ~line 327, add a per-kind count map only when a `CountQuotaSlice` is configured:
   ```csharp
   Dictionary<ContextKind, int>? selectedKindCounts = null;
   if (reportBuilder is not null && _slicer is CountQuotaSlice)
   {
       selectedKindCounts = new Dictionary<ContextKind, int>();
       for (var i = 0; i < slicedItems.Count; i++)
       {
           var k = slicedItems[i].Kind;
           selectedKindCounts.TryGetValue(k, out var c);
           selectedKindCounts[k] = c + 1;
       }
   }
   ```

4. **Classify exclusion reason in the re-association loop**: In the `else if (reportBuilder is not null)` branch (~line 340), replace the unconditional `ExclusionReason.BudgetExceeded` with cap-classification:
   ```csharp
   var exclusionReason = ExclusionReason.BudgetExceeded;
   if (selectedKindCounts is not null && _slicer is CountQuotaSlice cqs)
   {
       var kind = sorted[i].Item.Kind;
       var entry = cqs.Entries.FirstOrDefault(e => e.Kind == kind);
       if (entry is not null
           && sorted[i].Item.Tokens <= adjustedBudget.TargetTokens
           && selectedKindCounts.TryGetValue(kind, out var kindCount)
           && kindCount >= entry.CapCount)
       {
           exclusionReason = ExclusionReason.CountCapExceeded;
       }
   }
   reportBuilder.AddExcluded(sorted[i].Item, sorted[i].Score, exclusionReason);
   ```

5. **Confirm `CountQuotaSlice.Entries` is accessible**: Check that the `Entries` property (or equivalent) is public on `CountQuotaSlice`. If it's not exposed, use `countQuotaSlicer.LastShortfalls` for shortfall detection and check the entries via the existing `GetConstraints()` method or expose `Entries` if needed. Do not add new public API surface — use internal visibility if the accessor needs to be added.

6. **Run `dotnet build`**: Confirm 0 errors, 0 warnings. Run `dotnet test --project tests/Wollax.Cupel.Tests/` to confirm existing 664 tests still pass.

## Must-Haves

- [ ] `ReportBuilder` has `SetCountRequirementShortfalls()` setter with XML doc; `Build()` populates `SelectionReport.CountRequirementShortfalls`
- [ ] `CupelPipeline.Execute()` reads `LastShortfalls` after `_slicer.Slice()` and calls `reportBuilder.SetCountRequirementShortfalls()` when non-empty
- [ ] `CupelPipeline.Execute()` builds `selectedKindCounts` from `slicedItems` before the re-association loop (only when `CountQuotaSlice` is configured)
- [ ] Re-association loop classifies cap-excluded items as `ExclusionReason.CountCapExceeded` using `selectedKindCounts` and `CountQuotaSlice.Entries`
- [ ] Budget-exceeding items (tokens > adjustedBudget.TargetTokens) always receive `BudgetExceeded` regardless of cap state
- [ ] `dotnet build` 0 warnings; existing 664 tests pass

## Verification

- `dotnet build 2>&1 | grep -E "error|warning"` — no output (0 lines)
- `grep -n "CountCapExceeded\|SetCountRequirementShortfalls\|LastShortfalls" src/Wollax.Cupel/CupelPipeline.cs` — shows all three wiring sites
- `grep -n "SetCountRequirementShortfalls\|_countRequirementShortfalls" src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — shows setter and field
- `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | tail -5` — 664 passed, 0 failed

## Observability Impact

- Signals added/changed: `SelectionReport.CountRequirementShortfalls` now populated for real `DryRun()` calls with `CountQuotaSlice`; `ExclusionReason.CountCapExceeded` now appears on `ExcludedItem.Reason` for cap-excluded items
- How a future agent inspects this: `pipeline.DryRun(items).Report.CountRequirementShortfalls` and `pipeline.DryRun(items).Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded)` — fully structured, no parsing required
- Failure state exposed: When shortfalls occur, `CountRequirementShortfall.Kind`, `.RequiredCount`, `.SatisfiedCount` identify the exact constraint that wasn't met; `ExclusionReason.CountCapExceeded` on excluded items identifies cap enforcement

## Inputs

- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — `LastShortfalls` property (populated after `Slice()`), `Entries` property (list of `CountQuotaEntry` with `Kind` and `CapCount`)
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — existing `SetTotalCandidates`/`SetTotalTokensConsidered` setter pattern to follow
- `src/Wollax.Cupel/CupelPipeline.cs:361` — existing `_slicer is QuotaSlice quotaSlicer` cast pattern to mirror
- S02-RESEARCH.md — classification edge-case rule: budget constraint wins over cap; cap only classified when item fits budget AND kind count >= cap

## Expected Output

- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — 1 new private field, 1 new setter method with XML doc, 1 updated `Build()` line
- `src/Wollax.Cupel/CupelPipeline.cs` — 3 wiring additions: (a) shortfall read after `Slice()`; (b) per-kind count reconstruction; (c) cap-classification in re-association loop
