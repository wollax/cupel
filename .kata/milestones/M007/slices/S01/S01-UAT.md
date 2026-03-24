# S01: .NET DryRunWithPolicy and policy-accepting PolicySensitivity — UAT

**Milestone:** M007
**Written:** 2026-03-24

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: Both APIs are library methods with no UI, server, or human-experience surface. All observable behavior (scorer/slicer selection, budget usage, diff computation) is verifiable via automated tests and build artifact inspection. No runtime service is required.

## Preconditions

- .NET SDK installed (net10.0 target)
- Working directory: `/Users/wollax/Git/personal/cupel`
- `dotnet build` produces 0 errors/warnings (baseline green)

## Smoke Test

```bash
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental 2>&1 | grep -E "(error|warning)"
# Expected: no output (0 errors, 0 warnings)
```

## Test Cases

### 1. DryRunWithPolicy uses policy scorer, not pipeline scorer

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --treenode-filter "*/DryRunWithPolicyTests/*"`
2. Look for `UsesPolicy_Scorer_NotPipelines` in output
3. **Expected:** Test passes — policy with `ScorerType.Priority` selects delta+gamma (high-priority items) while a `ScorerType.Reflexive` pipeline would select alpha+beta (high hint items)

### 2. DryRunWithPolicy uses explicit budget parameter

1. Run the same test command as above
2. Look for `UsesExplicitBudget_NotPipelineBudget`
3. **Expected:** Test passes — budget of 100t restricts result to exactly 1 item, regardless of the pipeline's own 500t budget

### 3. Policy-based PolicySensitivity produces meaningful diffs

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`
2. Look for `PolicyOverload_TwoPoliciesWithDifferentScorers_ProducesMeaningfulDiff`
3. **Expected:** Test passes — two policies with Reflexive vs Priority scorers yield `Diffs.Count > 0`

### 4. Policy-based PolicySensitivity matches pipeline-based overload for equivalent configurations

1. Same test run as above
2. Look for `PolicyOverload_MatchesPipelineOverload_WhenEquivalentConfiguration`
3. **Expected:** Test passes — equivalent policy and pipeline configurations yield identical `Diffs.Count == 0`

### 5. PublicAPI surface is complete

1. Run: `grep "DryRunWithPolicy\|PolicySensitivity.*CupelPolicy" src/Wollax.Cupel/PublicAPI.Unshipped.txt`
2. **Expected:** Two entries present:
   - `Wollax.Cupel.CupelPipeline.DryRunWithPolicy(...CupelPolicy...)`
   - `...PolicySensitivityExtensions.PolicySensitivity(...CupelPolicy...)`

## Edge Cases

### Null-guard: items null

1. `UsesPolicy_Scorer_NotPipelines` → null items → `ArgumentNullException` with param name `items`
2. **Expected:** `ThrowsArgumentNullException_WhenItemsNull` passes

### Null-guard: budget null

1. **Expected:** `ThrowsArgumentNullException_WhenBudgetNull` passes

### Null-guard: policy null

1. **Expected:** `ThrowsArgumentNullException_WhenPolicyNull` passes

### Policy overload throws on fewer than 2 variants

1. **Expected:** `PolicyOverload_ThrowsWhenFewerThanTwoVariants` passes — `ArgumentException` thrown when only 1 variant passed

### Full regression

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`
2. **Expected:** 679 passed, 0 failed, 0 skipped

## Failure Signals

- Any `error` or `warning` in `dotnet build` output — build is not clean
- Any test failure in the DryRunWithPolicy or PolicySensitivity groups — implementation doesn't match spec
- `PublicAPI.Unshipped.txt` missing either new entry — PublicAPI analyzer will fail future builds when `PublicAPI.Shipped.txt` is out of sync
- `dotnet test` total below 679 — a test was accidentally removed

## Requirements Proved By This UAT

- R056 (.NET half) — `CupelPipeline.DryRunWithPolicy(items, budget, policy)` is public, returns a `ContextResult` driven by the policy's scorer/slicer/placer (not the pipeline's own configuration), verified by `UsesPolicy_Scorer_NotPipelines`, `UsesExplicitBudget_NotPipelineBudget`, `UsesPolicy_Slicer_Greedy_vs_Knapsack`. Policy-accepting `PolicySensitivity` overload is public and produces correct diffs, verified by `PolicyOverload_TwoPoliciesWithDifferentScorers_ProducesMeaningfulDiff` and `PolicyOverload_MatchesPipelineOverload_WhenEquivalentConfiguration`.

## Not Proven By This UAT

- R056 (Rust half) — Rust `Policy` struct, `PolicyBuilder`, and `Pipeline::dry_run_with_policy` are not yet implemented; deferred to S02.
- R056 (Rust `policy_sensitivity`) — Rust `policy_sensitivity` free function and `PolicySensitivityReport` type are not yet implemented; deferred to S03.
- Spec chapter at `spec/src/analytics/policy-sensitivity.md` — deferred to S03.
- `CountQuotaSlice` support in `CupelPolicy` — acknowledged limitation; `CupelPolicy` has no `CountQuota` slicer variant. Callers must use the pipeline-based `PolicySensitivity` overload for count-quota fork diagnostics.
- Operational/observability verification — library only; no runtime service, no OTel, no log sinks tested.

## Notes for Tester

- TUnit (the test framework used) requires `--treenode-filter` syntax, not `--filter`, for scoped test runs. Use the full test project path with `--project`.
- The `UsesPolicy_Scorer_NotPipelines` test was corrected in T02 from T01's original version. The T01 version used `ScorerType.Reflexive` for both policy and pipeline (logically a no-op divergence test). The corrected version uses `ScorerType.Priority` for the policy to reliably produce divergent selection — this is the intended behavior, not a test weakening.
- `DryRunWithPolicy` builds a fresh temp pipeline per call — it does not mutate the original pipeline. Safe to call multiple times on the same pipeline with different policies/budgets.
