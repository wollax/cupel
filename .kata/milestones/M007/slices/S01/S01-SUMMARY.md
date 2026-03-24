---
id: S01
parent: M007
milestone: M007
provides:
  - Public method CupelPipeline.DryRunWithPolicy(items, budget, policy) → ContextResult
  - Public static method PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string Label, CupelPolicy Policy)[]) → PolicySensitivityReport
  - Updated PublicAPI.Unshipped.txt with two new API entries
  - 6 new DryRunWithPolicy unit tests + 3 new PolicySensitivity policy-overload tests (all passing)
requires: []
affects:
  - S03
key_files:
  - src/Wollax.Cupel/CupelPipeline.cs
  - src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs
  - tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs
key_decisions:
  - DryRunWithPolicy delegates entirely to CreateBuilder().WithBudget(budget).WithPolicy(policy).Build().DryRunWithBudget(items, budget) — no duplication of policy→concrete mapping logic
  - PolicySensitivity policy overload mirrors the pipeline overload structure exactly; content-keyed diff algorithm is verbatim-consistent
  - UsesPolicy_Scorer_NotPipelines uses ScorerType.Priority (not a custom InvertedScorer) because CupelPolicy only accepts ScorerType enum values; Priority vs Reflexive reliably diverges when item Priority orderings are inverted relative to FutureRelevanceHint
  - XML doc for DryRunWithPolicy explicitly notes CountQuotaSlice gap (CupelPolicy has no CountQuota variant) and SlicerType.Stream sync fallback
patterns_established:
  - Policy-based dry run delegates to PipelineBuilder.WithPolicy() — keeps policy→concrete mapping in one place
  - Policy-based PolicySensitivity overload builds a temp pipeline per variant, identical to pipeline overload pattern
observability_surfaces:
  - result.Report!.Included / result.Report!.Excluded — full SelectionReport from DryRunWithPolicy output
  - result.Report!.Excluded[i].Reason — ExclusionReason per excluded item
  - PolicySensitivityReport.Diffs — items that swung across policy variants
  - PolicySensitivityReport.Variants[i].Report — full SelectionReport per variant
drill_down_paths:
  - .kata/milestones/M007/slices/S01/tasks/T01-SUMMARY.md
  - .kata/milestones/M007/slices/S01/tasks/T02-SUMMARY.md
duration: ~20min
verification_result: passed
completed_at: 2026-03-24
---

# S01: .NET DryRunWithPolicy and policy-accepting PolicySensitivity

**Added `CupelPipeline.DryRunWithPolicy` and a policy-based `PolicySensitivity` overload — all 679 tests pass with 0 warnings.**

## What Happened

**T01** established the red state: created `DryRunWithPolicyTests.cs` with 6 failing test methods and added 3 failing methods to `PolicySensitivityTests.cs`. All tests referenced the two not-yet-existing APIs, confirming the failing-test-first pattern. The build failed with exactly the expected CS1061/CS1503 errors.

**T02** delivered both APIs and made all tests green:

1. **`CupelPipeline.DryRunWithPolicy`** added to `CupelPipeline.cs` after `DryRunWithBudget`. Three `ArgumentNullException.ThrowIfNull` guards. Delegates to `CreateBuilder().WithBudget(budget).WithPolicy(policy).Build().DryRunWithBudget(items, budget)` — no duplication of policy→concrete mapping. Full XML doc states the CountQuota limitation (callers needing count-quota fork diagnostics must use the pipeline-based `PolicySensitivity` overload) and the `SlicerType.Stream` sync fallback.

2. **`PolicySensitivityExtensions.PolicySensitivity` (policy overload)** added as a second `public static` method alongside the existing pipeline overload. Builds a temp pipeline per variant using `WithBudget(budget).WithPolicy(variants[i].Policy).Build()`, then calls `DryRunWithBudget`. Content-keyed diff algorithm is structurally identical to the pipeline overload.

3. **`PublicAPI.Unshipped.txt`** updated with both new method signatures; `dotnet build` passes the PublicAPI analyzer with 0 warnings.

4. **Test fix for `UsesPolicy_Scorer_NotPipelines`**: T01's test used `ScorerType.Reflexive` for the policy but expected inverted behavior. Since `CupelPolicy` has no `Inverted` scorer type, T02 corrected this to `ScorerType.Priority` — items have Priority values ascending (1–4) while FutureRelevanceHint is descending, so `Reflexive` picks alpha+beta while `Priority` picks delta+gamma. This correctly exercises "policy scorer overrides pipeline scorer."

## Verification

```
# Build: 0 warnings
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental → 0 errors, 0 warnings

# Full test suite: 679 passed, 0 failed
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj → 679 passed, 0 failed
```

All slice-level acceptance checks:
- ✅ `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental` — 0 errors/warnings
- ✅ All 9 new tests pass (6 DryRunWithPolicy + 3 PolicySensitivity policy overload)
- ✅ Full suite (679 tests) — no regressions
- ✅ `PublicAPI.Unshipped.txt` contains both new method signatures

## Requirements Advanced

- R056 — .NET half of DryRunWithPolicy now shipped: `CupelPipeline.DryRunWithPolicy` and policy-based `PolicySensitivity` overload are public, tested, and documented. The requirement status remains `active` until S02 (Rust) and S03 (spec chapter) complete.

## Requirements Validated

- None in this slice — R056 validation requires all three slices (S01, S02, S03).

## New Requirements Surfaced

- None.

## Requirements Invalidated or Re-scoped

- None.

## Deviations

- **`UsesPolicy_Scorer_NotPipelines` test corrected in T02**: T01's authored test used `ScorerType.Reflexive` for the policy (same as pipeline's `ReflexiveScorer`) and expected inverted selection. `CupelPolicy` accepts only `ScorerType` enum values — there is no `Inverted` type. T02 switched the policy scorer to `ScorerType.Priority` with items having inverted Priority ordering. This is a plan-acknowledged adjustment (T01 summary flagged it as needing T02 revision) — not an unexpected deviation.

## Known Limitations

- `CupelPolicy` has no `CountQuota` slicer variant — callers needing count-quota fork diagnostics must use the pipeline-based `PolicySensitivity` overload. Documented in `DryRunWithPolicy` XML doc.
- `SlicerType.Stream` policies use `GreedySlice` as a synchronous fallback in `DryRunWithPolicy` (sync path). Documented in XML doc.
- No Rust equivalent yet — S02 delivers the Rust `Policy` struct and `dry_run_with_policy`; S03 completes the spec chapter.

## Follow-ups

- S02: Rust `Policy` struct + `PolicyBuilder` + `Pipeline::dry_run_with_policy`
- S03: Rust `policy_sensitivity` + spec chapter at `spec/src/analytics/policy-sensitivity.md` + R056 validation

## Files Created/Modified

- `src/Wollax.Cupel/CupelPipeline.cs` — Added `DryRunWithPolicy` public method with XML doc
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — Added policy-based `PolicySensitivity` overload
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Appended two new API entries
- `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` — New file with 6 test methods (including `UsesPolicy_Scorer_NotPipelines` correction in T02)
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — 3 new test methods added

## Forward Intelligence

### What the next slice should know
- `DryRunWithPolicy`'s delegation pattern (`CreateBuilder().WithBudget(budget).WithPolicy(policy).Build()`) is the correct pattern to replicate in Rust for `dry_run_with_policy` — the policy→concrete mapping lives entirely in the builder, not in the method.
- `CupelPolicy` maps `SlicerType.Stream` to `GreedySlice` for the sync path; the Rust equivalent will need an equivalent fallback decision for async slicers.
- The content-keyed diff algorithm in `PolicySensitivityExtensions` is the reference implementation — S03's Rust `policy_sensitivity` should produce the same diff semantics.

### What's fragile
- `UsesPolicy_Scorer_NotPipelines` relies on Priority orderings being inverted relative to FutureRelevanceHint in test data — if item fixture changes, this divergence assumption breaks silently.

### Authoritative diagnostics
- `result.Report!.Included` and `result.Report!.Excluded` — first place to look when any DryRunWithPolicy test fails; gives full SelectionReport with exclusion reasons.
- `PolicySensitivityReport.Diffs` — shows exactly which items swung between variants; empty means no divergence.

### What assumptions changed
- Original plan assumed a custom `InvertedRelevanceScorer` could be injected into `CupelPolicy` — this was impossible because `CupelPolicy` only accepts `ScorerType` enum values. The scorer test was redesigned to use contrasting enum types (Reflexive vs Priority) instead.
