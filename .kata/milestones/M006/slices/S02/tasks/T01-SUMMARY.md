---
id: T01
parent: S02
milestone: M006
provides:
  - ReportBuilder.SetCountRequirementShortfalls() setter wires shortfalls into SelectionReport
  - ReportBuilder._countRequirementShortfalls field and Build() integration
  - CupelPipeline.Execute() reads CountQuotaSlice.LastShortfalls after Slice() and sets them on reportBuilder
  - CupelPipeline.Execute() builds selectedKindCounts from slicedItems (only when CountQuotaSlice active)
  - Re-association loop classifies cap-excluded items as ExclusionReason.CountCapExceeded vs BudgetExceeded
  - CountQuotaSlice.Entries internal property exposes _entries to sibling assembly code
key_files:
  - src/Wollax.Cupel/Diagnostics/ReportBuilder.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/Slicing/CountQuotaSlice.cs
key_decisions:
  - "Used internal property CountQuotaSlice.Entries to expose _entries to CupelPipeline without adding public API surface (same assembly)"
  - "Cap-classification guard: budget constraint wins (Tokens > adjustedBudget.TargetTokens → BudgetExceeded); cap only fires when item fits budget AND selectedKindCounts[kind] >= entry.CapCount"
  - "selectedKindCounts built from slicedItems (post-Slice output) not from CountQuotaSlice internals — ensures correct count reflects actual pipeline output"
  - "LINQ FirstOrDefault used for Entries lookup in re-association loop; acceptable given diagnostic-only code path (not hot path)"
patterns_established:
  - "CountQuotaSlice cast pattern: _slicer is CountQuotaSlice cqs — mirrors existing _slicer is QuotaSlice quotaSlicer at pipeline line ~361"
  - "Null-guard selectedKindCounts: only allocated when reportBuilder is not null AND _slicer is CountQuotaSlice; null otherwise to avoid overhead on hot paths"
observability_surfaces:
  - "pipeline.DryRun(items).Report.CountRequirementShortfalls — populated when CountQuotaSlice candidate pool cannot satisfy RequireCount"
  - "pipeline.DryRun(items).Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded) — cap-excluded items"
duration: 15min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Wire pipeline shortfall and cap-exclusion reporting

**ReportBuilder now propagates CountQuotaSlice shortfalls and cap-exclusion reasons through the pipeline re-association loop.**

## What Happened

Two structural wiring gaps from the research audit were closed with surgical changes to three files:

1. **Shortfall wiring** (`ReportBuilder` + `CupelPipeline`): Added `_countRequirementShortfalls` field, `SetCountRequirementShortfalls()` setter (with XML doc), and wired `Build()` to include it in `SelectionReport`. In `CupelPipeline.Execute()`, immediately after `_slicer.Slice()` returns, a cast-and-check reads `countQuotaSlicer.LastShortfalls` and calls `reportBuilder.SetCountRequirementShortfalls()` when non-empty.

2. **Cap-exclusion classification** (`CupelPipeline`): Before the `slicedSet` construction, a `selectedKindCounts` dictionary is built from `slicedItems` (only when `CountQuotaSlice` is active and `reportBuilder` is present). In the re-association loop, the unconditional `ExclusionReason.BudgetExceeded` was replaced with a classification rule: budget constraint wins when `item.Tokens > adjustedBudget.TargetTokens`; otherwise if the item's kind has a `CountQuotaEntry` and `selectedKindCounts[kind] >= entry.CapCount`, `CountCapExceeded` is assigned.

3. **Entries accessor** (`CountQuotaSlice`): Added `internal IReadOnlyList<CountQuotaEntry> Entries => _entries;` to expose the private entries list to `CupelPipeline` without adding public API surface.

## Verification

- `dotnet build src/Wollax.Cupel/` — 0 errors, 0 warnings (verified)
- `dotnet test --project tests/Wollax.Cupel.Tests/` — 664 passed, 0 failed (verified)
- `grep -n "CountCapExceeded|SetCountRequirementShortfalls|LastShortfalls" CupelPipeline.cs` — shows all 3 wiring sites (line 308/311/313 shortfall read, line 377 cap classification)
- `grep -n "SetCountRequirementShortfalls|_countRequirementShortfalls" ReportBuilder.cs` — shows field (13), setter (54-55), Build() inclusion (84)

## Diagnostics

- `pipeline.DryRun(items).Report.CountRequirementShortfalls` — list of `CountRequirementShortfall(Kind, RequiredCount, SatisfiedCount)` for any unmet requirements
- `pipeline.DryRun(items).Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded)` — items excluded because their kind hit the cap
- Both surfaces are machine-readable via `SelectionReport` properties; no parsing required

## Deviations

None. All steps executed as planned. The `Entries` property was added as `internal` per the plan's guidance ("use internal visibility if the accessor needs to be added").

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — added `_countRequirementShortfalls` field, `SetCountRequirementShortfalls()` setter with XML doc, updated `Build()` to populate `CountRequirementShortfalls`
- `src/Wollax.Cupel/CupelPipeline.cs` — added shortfall read after `_slicer.Slice()`, per-kind count reconstruction before re-association loop, cap-classification logic in re-association loop
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — added `internal Entries` property exposing `_entries`
