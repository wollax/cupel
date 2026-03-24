---
id: T02
parent: S01
milestone: M007
provides:
  - Public method CupelPipeline.DryRunWithPolicy(items, budget, policy) → ContextResult
  - Public static method PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string, CupelPolicy)[]) → PolicySensitivityReport
  - Updated PublicAPI.Unshipped.txt with two new entries
key_files:
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs
key_decisions:
  - UsesPolicy_Scorer_NotPipelines test corrected to use ScorerType.Priority for the policy (not ScorerType.Reflexive) — CupelPolicy has no InvertedScorer type; Priority vs Reflexive reliably diverges when FutureRelevanceHint and Priority orderings are inverted
patterns_established:
  - DryRunWithPolicy delegates entirely to CreateBuilder().WithBudget(budget).WithPolicy(policy).Build().DryRunWithBudget(items, budget) — never duplicates policy→concrete mapping logic
  - PolicySensitivity policy overload mirrors the pipeline overload structure exactly; content-keyed diff algorithm is verbatim-consistent
observability_surfaces:
  - result.Report!.Included / result.Report!.Excluded — full SelectionReport available from DryRunWithPolicy output
  - result.Report!.Excluded[i].Reason — ExclusionReason available for each excluded item
duration: ~10min
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T02: Implement DryRunWithPolicy, policy-based PolicySensitivity overload, and update PublicAPI.Unshipped.txt

**Added `CupelPipeline.DryRunWithPolicy` and a policy-based `PolicySensitivity` overload — all 679 tests pass with 0 warnings.**

## What Happened

Implemented both new public APIs exactly as specified in the task plan:

1. **`CupelPipeline.DryRunWithPolicy`** inserted after `DryRunWithBudget` in `CupelPipeline.cs`. Uses `CreateBuilder().WithBudget(budget).WithPolicy(policy).Build()` then calls `DryRunWithBudget` on the temp pipeline — no duplication of policy→concrete mapping. Full XML doc includes CountQuota limitation and Stream slicer fallback notes. Three `ArgumentNullException.ThrowIfNull` guards.

2. **`PolicySensitivityExtensions.PolicySensitivity` (policy overload)** added as a second `public static` method alongside the existing pipeline overload. Builds a temp pipeline per variant using `WithBudget(budget).WithPolicy(variants[i].Policy).Build()`, then runs `DryRunWithBudget`. Content-keyed diff algorithm is structurally identical to the pipeline overload.

3. **`PublicAPI.Unshipped.txt`** — two entries appended; `dotnet build` passes the PublicAPI analyzer with 0 errors/warnings.

4. **Test fix**: `UsesPolicy_Scorer_NotPipelines` was updated (deviation from T01's authored test). The T01 test used `ScorerType.Reflexive` for the policy but expected "inverted" behavior (delta/gamma selected over alpha/beta). Since `CupelPolicy` accepts only `ScorerType` enum values and has no `Inverted` type, the test was corrected to use `ScorerType.Priority` — items have Priority values 1–4 (ascending) while FutureRelevanceHint is descending, so Reflexive picks alpha+beta while Priority picks delta+gamma. This correctly exercises the "policy scorer overrides pipeline scorer" behavior.

## Verification

```
# Build: 0 warnings
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental → 0 errors, 0 warnings

# Full test suite (679 tests)
dotnet run --project tests/Wollax.Cupel.Tests/ → 679 passed, 0 failed

# Full solution build
dotnet build --no-incremental → 0 errors, 0 warnings
```

All slice-level verification checks pass:
- ✅ `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental` — 0 errors/warnings
- ✅ All 9 new DryRunWithPolicy + PolicySensitivity tests pass
- ✅ Full suite (679 tests) — no regressions

## Diagnostics

- `result.Report!.Included` / `result.Report!.Excluded` — inspect which items were selected and why from DryRunWithPolicy output
- `result.Report!.Excluded[i].Reason` — ExclusionReason enum value for each excluded item
- `PolicySensitivityReport.Diffs` — items that swung inclusion status across policy variants
- `PolicySensitivityReport.Variants[i].Report` — full SelectionReport per variant

## Deviations

- **`UsesPolicy_Scorer_NotPipelines` test corrected**: The T01-authored test used `ScorerType.Reflexive` for the policy (same as the pipeline's `ReflexiveScorer`) but expected the opposite selection (delta+gamma instead of alpha+beta). This was logically inconsistent. T01 summary flagged this test as requiring T02 adjustment. Fixed by switching the policy scorer to `ScorerType.Priority` with items having inverted Priority ordering — this correctly demonstrates that `DryRunWithPolicy` uses the policy's scorer rather than the pipeline's.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/CupelPipeline.cs` — Added `DryRunWithPolicy` public method
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — Added policy-based `PolicySensitivity` overload
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Appended two new API entries
- `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` — Fixed `UsesPolicy_Scorer_NotPipelines` test scorer type
