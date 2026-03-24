---
id: S02
parent: M006
milestone: M006
provides:
  - ReportBuilder.SetCountRequirementShortfalls() setter wires CountQuotaSlice shortfalls into SelectionReport
  - CupelPipeline.Execute() reads CountQuotaSlice.LastShortfalls after Slice() and propagates them to ReportBuilder
  - CupelPipeline.Execute() classifies cap-excluded items as ExclusionReason.CountCapExceeded (not BudgetExceeded) via per-kind count reconstruction from slicedItems
  - CountQuotaSlice.Entries internal property exposes _entries to CupelPipeline (same assembly, no public API addition)
  - 5 CountQuota conformance integration tests covering baseline, cap exclusion, require+cap, scarcity-degrade, and tag non-exclusive scenarios
  - Full-solution build verified: 14 projects, 0 errors, 0 warnings
  - All solution test projects: 782 passed, 0 failed across 6 test assemblies
  - R052 quota_utilization tests unbroken; PublicAPI.Unshipped.txt unchanged
requires: []
affects:
  - S03
key_files:
  - src/Wollax.Cupel/Diagnostics/ReportBuilder.cs
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/Slicing/CountQuotaSlice.cs
  - tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs
key_decisions:
  - "Used internal property CountQuotaSlice.Entries to expose _entries to CupelPipeline without adding public API surface (same assembly)"
  - "Cap-classification guard: budget constraint wins (Tokens > adjustedBudget.TargetTokens → BudgetExceeded); cap only fires when item fits budget AND selectedKindCounts[kind] >= entry.CapCount"
  - "selectedKindCounts built from slicedItems (post-Slice output) not from CountQuotaSlice internals — ensures correct count reflects actual pipeline output"
  - "LINQ FirstOrDefault used for Entries lookup in re-association loop; acceptable given diagnostic-only code path (not hot path)"
  - "Added WithScorer(new ReflexiveScorer()) to integration test pipeline builder — scorer is mandatory even when CountQuotaSlice uses its own ranking; FutureRelevanceHint drives score"
  - "Content-based membership assertions via .Select(i => i.Item.Content).ToList() + .Contains() — avoids fragile index-based assertions on U-shaped placer output"
  - "Solution file is Cupel.slnx (not cupel.sln); full-solution commands use dotnet build/test Cupel.slnx"
patterns_established:
  - "CountQuotaSlice cast pattern: _slicer is CountQuotaSlice cqs — mirrors existing _slicer is QuotaSlice quotaSlicer at pipeline line ~361"
  - "Null-guard selectedKindCounts: only allocated when reportBuilder is not null AND _slicer is CountQuotaSlice; avoids overhead on hot paths"
  - "Run() helper pattern in integration tests: static helper builds CountQuotaSlice pipeline and calls DryRun() — mirrors ExplainabilityIntegrationTests SC helpers"
observability_surfaces:
  - "pipeline.DryRun(items).Report.CountRequirementShortfalls — populated when CountQuotaSlice candidate pool cannot satisfy RequireCount"
  - "pipeline.DryRun(items).Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded) — cap-excluded items"
  - "dotnet build Cupel.slnx 2>&1 | grep -E 'error|warning' — 0 lines = green"
  - "dotnet test --solution Cupel.slnx — total: 782, failed: 0 = green"
drill_down_paths:
  - .kata/milestones/M006/slices/S02/tasks/T01-SUMMARY.md
  - .kata/milestones/M006/slices/S02/tasks/T02-SUMMARY.md
  - .kata/milestones/M006/slices/S02/tasks/T03-SUMMARY.md
duration: 35min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S02: .NET CountQuotaSlice — audit, complete, and test

**CountQuotaSlice.LastShortfalls and ExclusionReason.CountCapExceeded now flow end-to-end through CupelPipeline.DryRun(); 5 conformance integration tests pass; 782 solution tests green, 0 warnings.**

## What Happened

Three tasks closed the two structural wiring gaps identified in the S02 research audit and proved them end-to-end with integration tests.

**T01 — Pipeline wiring (ReportBuilder + CupelPipeline + CountQuotaSlice):** Added `_countRequirementShortfalls` field, `SetCountRequirementShortfalls()` setter (XML-documented), and wired `ReportBuilder.Build()` to include shortfalls in `SelectionReport`. In `CupelPipeline.Execute()`, immediately after `_slicer.Slice()` returns, a cast-and-check reads `countQuotaSlicer.LastShortfalls` and calls `SetCountRequirementShortfalls()` when non-empty. Before the re-association loop, a `selectedKindCounts` dictionary is built from `slicedItems` (only when `CountQuotaSlice` is active and `reportBuilder` present). The loop's unconditional `BudgetExceeded` was replaced with a classification rule: budget constraint wins when `item.Tokens > adjustedBudget.TargetTokens`; otherwise `CountCapExceeded` fires when the item's kind has a `CountQuotaEntry` and `selectedKindCounts[kind] >= entry.CapCount`. A single `internal Entries` property on `CountQuotaSlice` exposes `_entries` to the pipeline without adding public API surface.

**T02 — Integration tests (5 conformance vectors):** Created `CountQuotaIntegrationTests.cs` with a `Run()` static helper that builds a `CountQuotaSlice(new GreedySlice(), entries, scarcity)` pipeline with `ReflexiveScorer` and calls `DryRun()`. Five tests mirror the Rust TOML conformance vectors: baseline (3 items, require=2 cap=4, all included, no shortfalls), cap exclusion (3 items, cap=1, 2 cap-excluded), require+cap combined (4 items, require=2 cap=2, 2 included + 2 cap-excluded), scarcity-degrade (1 tool item, require=3, shortfall reported), and tag non-exclusive (critical+urgent items, both kinds satisfied). All use content-based membership assertions to avoid fragile index-based checks.

**T03 — Final verification:** Full-solution build (`dotnet build Cupel.slnx`) — 14 projects, 0 errors, 0 warnings. `Wollax.Cupel.Tests` — 669 passed, 0 failed. All solution test assemblies — 782 passed, 0 failed. R052 quota_utilization tests confirmed present and passing. `git diff PublicAPI.Unshipped.txt` — no output (no accidental public API additions).

## Verification

| Check | Status | Evidence |
|-------|--------|----------|
| `dotnet build Cupel.slnx` 0 errors, 0 warnings | ✓ PASS | 14 projects, 0 errors, 0 warnings |
| Wollax.Cupel.Tests 669 passed, 0 failed | ✓ PASS | total: 669, failed: 0 |
| All solution tests 782 passed, 0 failed | ✓ PASS | 6 assemblies, total: 782, failed: 0 |
| Both wiring sites in CupelPipeline.cs | ✓ PASS | grep shows lines 308/311/313 (shortfalls) + 377 (cap) |
| CountRequirementShortfalls non-empty in scarcity DryRun | ✓ PASS | Test 4 (scarcity-degrade) asserts Count==1 with correct fields |
| CountCapExceeded in Excluded for cap DryRun | ✓ PASS | Tests 2 and 3 assert cap-excluded counts |
| PublicAPI.Unshipped.txt unchanged | ✓ PASS | git diff — no output |
| R052 quota_utilization unbroken | ✓ PASS | Both tests confirmed in 669 total |

## Requirements Advanced

- R061 — .NET half of CountQuotaSlice fully wired; all 5 conformance scenarios pass in integration tests; `SelectionReport.CountRequirementShortfalls` and `ExclusionReason.CountCapExceeded` proven in real `DryRun()` output

## Requirements Validated

- none — R061 awaits S03 cross-language composition proof before validation

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None from plan. The `Entries` internal property was added exactly as planned. `WithScorer(new ReflexiveScorer())` was a minor discovery (scorer mandatory in PipelineBuilder) — handled inline.

## Known Limitations

- S03 remains: `CountQuotaSlice + QuotaSlice` cross-language composition test and `PublicAPI.Unshipped.txt` final audit still required before R061 is validated
- `PublicAPI.Unshipped.txt` does not yet reflect any M006 additions — to be audited in S03

## Follow-ups

- S03: cross-language composition proof (`CountQuotaSlice + QuotaSlice` in both languages); `PublicAPI.Unshipped.txt` final audit; R061 validation in REQUIREMENTS.md; M006 summaries

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — added `_countRequirementShortfalls` field, `SetCountRequirementShortfalls()` setter with XML doc, updated `Build()` to populate `CountRequirementShortfalls`
- `src/Wollax.Cupel/CupelPipeline.cs` — added shortfall read after `_slicer.Slice()`, per-kind count reconstruction before re-association loop, cap-classification logic in re-association loop
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — added `internal Entries` property exposing `_entries`
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — new file, 5 integration test methods covering all conformance vectors (~165 lines)

## Forward Intelligence

### What the next slice should know
- `CountQuotaSlice.Entries` is `internal` — accessible within `Wollax.Cupel` assembly only; S03 cross-language composition test works without changes to this
- `ReflexiveScorer` is required in any `PipelineBuilder` even when `CountQuotaSlice` drives its own ranking — FutureRelevanceHint feeds score
- All `dotnet test` commands must use TUnit's test runner; `--filter` requires a "tree filter" not MSTest-style predicate
- The solution file is `Cupel.slnx` (not `cupel.sln`); always use `dotnet build Cupel.slnx` or `dotnet test --solution Cupel.slnx` for full-solution commands

### What's fragile
- Cap-exclusion classification relies on `selectedKindCounts` rebuilt from `slicedItems` — if the inner slicer returns items that don't fully match the original cap logic, counts could diverge; acceptable because `GreedySlice` is deterministic
- `LINQ FirstOrDefault` in the re-association loop is a linear scan per excluded item — not a concern for diagnostic-only paths but worth noting if item counts grow very large

### Authoritative diagnostics
- `pipeline.DryRun(items).Report.CountRequirementShortfalls` — machine-readable shortfall list; `Kind`, `RequiredCount`, `SatisfiedCount` fields are the canonical inspection surface
- `pipeline.DryRun(items).Report.Excluded.Where(e => e.Reason == ExclusionReason.CountCapExceeded)` — cap-excluded items, no parsing required

### What assumptions changed
- None — the skeleton was complete as researched; only wiring gaps required fixing; no new API surface was needed beyond the `internal Entries` property
