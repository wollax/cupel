# S02: .NET CountQuotaSlice — audit, complete, and test — UAT

**Milestone:** M006
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All acceptance criteria are mechanically verifiable via `dotnet test` and `dotnet build` — no UI, no server, no human interaction required. The 5 integration tests exercise real `CupelPipeline.DryRun()` calls with asserted `SelectionReport` field values.

## Preconditions

- .NET 10 SDK installed
- Repository root: `/Users/wollax/Git/personal/cupel`
- Solution file: `Cupel.slnx`
- No environment setup beyond the SDK is required

## Smoke Test

```bash
dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | tail -5
```

Expected: `total: 669, failed: 0, succeeded: 669`

## Test Cases

### 1. Full solution builds with 0 warnings

```bash
cd /Users/wollax/Git/personal/cupel
dotnet build Cupel.slnx 2>&1 | grep -E 'error|warning'
```

**Expected:** No output (0 errors, 0 warnings across all 14 projects)

### 2. CountRequirementShortfalls populated in scarcity scenario

```bash
dotnet test --project tests/Wollax.Cupel.Tests/ --filter "Scarcity" 2>&1 | tail -5
```

Or run the full suite and confirm test `CountQuotaSlice_ScarcityDegrade_PopulatesShortfalls` passes.

**Expected:** Test passes — `Report.CountRequirementShortfalls.Count == 1` with `Kind == ContextKind.ToolOutput`, `RequiredCount == 3`, `SatisfiedCount == 1`

### 3. CountCapExceeded appears in Excluded for cap scenario

Run `CountQuotaSlice_CapExclusion_ExcludesOverCapItems` test.

**Expected:** `Report.Excluded.Count(e => e.Reason == ExclusionReason.CountCapExceeded) == 2`; `Report.Included.Count == 1`

### 4. All 5 conformance integration tests pass

```bash
dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | grep -E "CountQuota|total:"
```

**Expected:** 5 CountQuota tests pass; `total: 669, failed: 0`

### 5. Full solution test run — no regressions

```bash
dotnet test --solution Cupel.slnx 2>&1 | tail -5
```

**Expected:** `total: 782, failed: 0` across all 6 test assemblies

## Edge Cases

### Budget takes priority over cap in classification

In the cap-exclusion test, items that exceed the token budget should receive `BudgetExceeded`, not `CountCapExceeded`. The test items are all 100 tokens with budget 1000 — cap fires correctly. If an item's token count exceeded the budget, `BudgetExceeded` would take priority.

**Verifiable by inspection:** `CupelPipeline.cs` line ~377 — budget guard fires first (`item.Tokens > adjustedBudget.TargetTokens → BudgetExceeded`); cap classification is the else branch.

### Shortfalls not reported when requirement is met

In the baseline test (3 tool items, require=2), `CountRequirementShortfalls.Count == 0`. The shortfall list is only populated when `LastShortfalls.Count > 0`.

**Expected:** Baseline test asserts `Report.CountRequirementShortfalls.Count == 0` — passes.

## Failure Signals

- `dotnet build Cupel.slnx` produces any `error` or `warning` lines → build regression
- `dotnet test --project tests/Wollax.Cupel.Tests/` reports `failed: N` (N > 0) → test regression
- `Report.CountRequirementShortfalls.Count == 0` when scarcity scenario is run → shortfall wiring broken
- No `ExclusionReason.CountCapExceeded` items in cap scenario → cap classification broken
- `git diff src/Wollax.Cupel/PublicAPI.Unshipped.txt` produces output → accidental public API addition

## Requirements Proved By This UAT

- R061 (.NET half) — `CountQuotaSlice` fully implemented in .NET; all 5 conformance scenarios pass in `Wollax.Cupel.Tests`; `SelectionReport.CountRequirementShortfalls` and `ExclusionReason.CountCapExceeded` populated in real `DryRun()` output; `dotnet build` 0 warnings; `dotnet test` (all projects) green; `quota_utilization` with `CountQuotaSlice` unbroken (R052 tests pass)

## Not Proven By This UAT

- R061 (cross-language composition) — `CountQuotaSlice + QuotaSlice` combined usage tested in both languages is S03 scope
- `PublicAPI.Unshipped.txt` final audit — S03 will confirm the complete new public API surface for M006
- Rust `CountQuotaSlice` behavior — proven in S01; not re-verified here

## Notes for Tester

All verification is fully automated — human review of test results only. No manual steps required beyond running the commands above. The TUnit test runner output includes test names; search for `CountQuota` to isolate the 5 new tests from the 664 pre-existing ones.
