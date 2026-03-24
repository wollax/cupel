# S02: .NET CountQuotaSlice — audit, complete, and test

**Goal:** Wire `CountQuotaSlice.LastShortfalls` into `SelectionReport.CountRequirementShortfalls` and classify cap-excluded items as `ExclusionReason.CountCapExceeded` in the pipeline re-association loop; then prove both flows end-to-end with 5 integration tests mirroring the Rust conformance vectors.
**Demo:** `CupelPipeline.DryRun()` with a `CountQuotaSlice` produces a `SelectionReport` where (a) shortfalls appear in `CountRequirementShortfalls` when candidates are insufficient, and (b) cap-excluded items appear in `Excluded` with `ExclusionReason.CountCapExceeded`. All 5 conformance scenarios pass. `dotnet build` 0 warnings. `dotnet test` (all projects) green.

## Must-Haves

- `ReportBuilder` gains a `SetCountRequirementShortfalls(IReadOnlyList<CountRequirementShortfall>)` setter method
- `CupelPipeline.Execute()` reads `CountQuotaSlice.LastShortfalls` after `_slicer.Slice()` and passes them to `ReportBuilder`
- `CupelPipeline.Execute()` classifies cap-excluded items as `ExclusionReason.CountCapExceeded` (not `BudgetExceeded`) in the re-association loop, using per-kind counts rebuilt from `slicedItems`
- `SelectionReport.CountRequirementShortfalls` is non-empty in a real `DryRun()` when candidates are scarce
- `SelectionReport.Excluded` contains items with `ExclusionReason.CountCapExceeded` in a real `DryRun()` when cap is hit
- 5 integration tests in `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` covering all 5 conformance vectors pass
- `dotnet build` exits with 0 warnings (all new code fully XML-documented)
- Full `dotnet test` (all projects, 664 → 669 tests) passes with 0 failures

## Proof Level

- This slice proves: integration
- Real runtime required: yes — `CupelPipeline.DryRun()` called with real `ContextItem` lists
- Human/UAT required: no

## Verification

- `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | tail -5` — all 669 tests pass, 0 failed
- `dotnet build 2>&1 | tail -5` — 0 errors, 0 warnings across all 14 projects
- `grep -n "CountCapExceeded\|CountRequirementShortfalls" src/Wollax.Cupel/CupelPipeline.cs` — shows both wiring sites
- `grep -c "CountQuotaIntegrationTests\|CountCapExceeded\|CountRequirementShortfalls" tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — non-zero

## Observability / Diagnostics

- Runtime signals: `ExclusionReason.CountCapExceeded` on `ExcludedItem.Reason` in `SelectionReport`; `CountRequirementShortfall` entries in `SelectionReport.CountRequirementShortfalls` — both machine-readable and surfaced via `DryRun()`
- Inspection surfaces: `pipeline.DryRun(items).Report` — structured, programmatically inspectable without parsing messages
- Failure visibility: `SelectionReport.Excluded[i].Reason` identifies cap-excluded items; `CountRequirementShortfalls[i]` exposes `Kind`, `RequiredCount`, `SatisfiedCount` for each shortfall
- Redaction constraints: none — diagnostic data is structural metadata, never item content

## Integration Closure

- Upstream surfaces consumed: `CountQuotaSlice.LastShortfalls` (slicer inspection surface per D087); `CountRequirementShortfall` sealed record; `ExclusionReason.CountCapExceeded = 8`; `ReportBuilder.AddExcluded()`
- New wiring introduced in this slice: `CupelPipeline.Execute()` → cast to `CountQuotaSlice` → read `LastShortfalls` → `ReportBuilder.SetCountRequirementShortfalls()`; post-hoc cap classification via per-kind count reconstruction from `slicedItems`
- What remains before the milestone is truly usable end-to-end: S03 (cross-language composition proof, `PublicAPI.Unshipped.txt` final audit, R061 validation)

## Tasks

- [x] **T01: Wire pipeline shortfall and cap-exclusion reporting** `est:1h`
  - Why: The two structural wiring gaps identified in research — `LastShortfalls` never flows to `SelectionReport`, and cap-excluded items always receive `BudgetExceeded` — are the core correctness issues to fix
  - Files: `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs`, `src/Wollax.Cupel/CupelPipeline.cs`
  - Do:
    1. Add `SetCountRequirementShortfalls(IReadOnlyList<CountRequirementShortfall> shortfalls)` setter to `ReportBuilder` (XML doc required; stores internally; `Build()` copies into `SelectionReport.CountRequirementShortfalls`)
    2. Update `ReportBuilder.Build()` to populate `CountRequirementShortfalls` from the stored list
    3. In `CupelPipeline.Execute()`, after `_slicer.Slice()` (line ~305), add: `if (_slicer is CountQuotaSlice countQuotaSlicer && reportBuilder is not null && countQuotaSlicer.LastShortfalls.Count > 0) reportBuilder.SetCountRequirementShortfalls(countQuotaSlicer.LastShortfalls);`
    4. Before the re-association loop (~line 327), reconstruct per-kind selected counts from `slicedItems`: build `Dictionary<ContextKind, int> selectedKindCounts` by iterating `slicedItems` and incrementing per item's `Kind`
    5. In the re-association loop's `else if (reportBuilder is not null)` branch (~line 340), replace the unconditional `BudgetExceeded` with cap-classification logic: if `_slicer is CountQuotaSlice cqs` and the item's kind has an entry in `cqs.Entries` with `CapCount` reached in `selectedKindCounts`, use `CountCapExceeded`; otherwise use `BudgetExceeded`. Edge case: if item tokens exceed `adjustedBudget.TargetTokens`, `BudgetExceeded` takes priority regardless of cap state
    6. Run `dotnet build` — confirm 0 warnings (all new code must have XML docs)
  - Verify: `dotnet build` exits 0 warnings; `grep -n "CountCapExceeded\|SetCountRequirementShortfalls\|LastShortfalls" src/Wollax.Cupel/CupelPipeline.cs` shows all three wiring sites
  - Done when: `dotnet build` 0 warnings; pipeline compiles with both wiring changes in place; existing 664 tests still pass

- [x] **T02: Write 5 conformance integration tests** `est:1h`
  - Why: The slice's integration-level proof requires tests that exercise the wiring end-to-end through a real `DryRun()` — unit tests calling `Slice()` directly cannot verify pipeline-level shortfall and cap-exclusion flows
  - Files: `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs`
  - Do:
    1. Create `CountQuotaIntegrationTests.cs` with a helper: `static ContextItem Item(string content, int tokens, double score, string kindName)` returning a `ContextItem` with the given properties
    2. Helper: `static ContextResult Run(IReadOnlyList<CountQuotaEntry> entries, IReadOnlyList<ContextItem> items, int budget, ScarcityBehavior scarcity = ScarcityBehavior.Degrade)` that builds a pipeline with `CountQuotaSlice(new GreedySlice(), entries, scarcity)` and calls `DryRun(items, budget)`
    3. Test 1 — **Baseline** (mirrors `count-quota-baseline.toml`): 3 tool items (100t each), budget 1000, require=2 cap=4. Assert: `Report.Included.Count == 3`; `Report.CountRequirementShortfalls.Count == 0`; no `CountCapExceeded` in `Report.Excluded`
    4. Test 2 — **Cap exclusion** (mirrors `count-quota-cap-exclusion.toml`): 3 tool items, budget 1000, require=0 cap=1. Assert: `Report.Included.Count == 1`; `Report.Included[0].Item.Content == "tool-a"`; `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 2`; `Report.CountRequirementShortfalls.Count == 0`
    5. Test 3 — **Require+cap combined** (mirrors `count-quota-require-and-cap.toml`): 4 tool items, budget 1000, require=2 cap=2. Assert: `Report.Included.Count == 2`; selected contents are `tool-a`, `tool-b`; `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 2`; `Report.CountRequirementShortfalls.Count == 0`
    6. Test 4 — **Scarcity degrade** (mirrors `count-quota-scarcity-degrade.toml`): 1 tool item (tool-a), budget 1000, require=3 cap=5. Assert: `Report.Included.Count == 1`; `Report.CountRequirementShortfalls.Count == 1`; shortfall has `Kind == ContextKind.Tool`, `RequiredCount == 3`, `SatisfiedCount == 1`; no `CountCapExceeded` in excluded
    7. Test 5 — **Tag non-exclusive** (mirrors `count-quota-tag-nonexclusive.toml`): critical+urgent items plus one extra critical, budget 1000, require=1 cap=4 for each kind. Assert: `Report.Included.Count == 3`; `Report.CountRequirementShortfalls.Count == 0`; no `CountCapExceeded`
    8. Run `dotnet test --project tests/Wollax.Cupel.Tests/` — all 5 new tests must pass; total should be 669
  - Verify: `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | grep -E "total:|failed:"` — total 669, failed 0; `grep -c "CountCapExceeded\|CountRequirementShortfalls" tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` > 5
  - Done when: All 5 integration tests pass; `CountRequirementShortfalls` and `CountCapExceeded` are both asserted from real `DryRun()` output

- [x] **T03: Final build verification and quota_utilization check** `est:30m`
  - Why: Milestone requires `dotnet build` 0 warnings (full solution), `dotnet test` (all projects) green, and `quota_utilization` with `CountQuotaSlice` verified — the research confirmed utilization tests already pass, but final end-to-end verification must confirm no regressions from T01/T02 changes
  - Files: no new files; verification only
  - Do:
    1. Run `dotnet build` on the full solution — confirm 0 errors, 0 warnings across all 14 projects
    2. Run `dotnet test --project tests/Wollax.Cupel.Tests/` — confirm 669 passed, 0 failed
    3. Locate and run quota_utilization tests: `grep -rn "CountQuotaSlice\|QuotaUtilization.*Count" tests/Wollax.Cupel.Tests/ --include="*.cs" -l` then confirm those test files pass
    4. Confirm `PublicAPI.Unshipped.txt` does not need updates: all new code in T01 is internal (`ReportBuilder` is `internal sealed`); `CupelPipeline` modifications add no new public members; no new public API surface was introduced
    5. Run `dotnet test` for all test projects (including `Wollax.Cupel.Testing.Tests`, `Wollax.Cupel.Diagnostics.OpenTelemetry.Tests`, etc.) to confirm no regressions
  - Verify: `dotnet build 2>&1 | grep -E "error|warning"` — 0 lines; `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | tail -5` — 669 passed; quota_utilization tests pass
  - Done when: Full solution builds clean; all 669+ tests pass across all projects; no `PublicAPI.Unshipped.txt` changes required

## Files Likely Touched

- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — add `SetCountRequirementShortfalls()` setter and wire into `Build()`
- `src/Wollax.Cupel/CupelPipeline.cs` — wire `LastShortfalls` after `Slice()`; classify cap-excluded items in re-association loop
- `tests/Wollax.Cupel.Tests/Pipeline/CountQuotaIntegrationTests.cs` — new file with 5 conformance integration tests
