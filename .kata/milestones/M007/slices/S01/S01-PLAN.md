# S01: .NET DryRunWithPolicy and policy-accepting PolicySensitivity

**Goal:** Add `CupelPipeline.DryRunWithPolicy(items, budget, policy)` and `PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string, CupelPolicy)[])` as public APIs — both verified by passing tests.
**Demo:** After this slice, `.NET` callers can call `pipeline.DryRunWithPolicy(items, budget, policy)` and get a `ContextResult` driven by the policy's scorer/slicer/placer (not the pipeline's own configuration), and `PolicySensitivityExtensions.PolicySensitivity(items, budget, ("a", policyA), ("b", policyB))` compares policies directly without pre-building pipelines.

## Must-Haves

- `CupelPipeline.DryRunWithPolicy(IReadOnlyList<ContextItem> items, ContextBudget budget, CupelPolicy policy)` is public and returns `ContextResult` driven by the policy's configuration
- `PolicySensitivityExtensions.PolicySensitivity(IReadOnlyList<ContextItem> items, ContextBudget budget, params (string Label, CupelPolicy Policy)[] variants)` overload is public and produces the same diff structure as the pipeline-based overload
- `DryRunWithPolicy` uses an explicit `budget` parameter (D148 — not inherited from the pipeline)
- `DryRunWithPolicy` XML doc notes the `CountQuotaSlice` gap: callers needing count-quota fork diagnostics must use the pipeline-based `PolicySensitivity` overload (D151)
- `DryRunWithPolicy` XML doc notes that `SlicerType.Stream` policies use `GreedySlice` as sync fallback (D151)
- `PublicAPI.Unshipped.txt` updated with both new method signatures
- `dotnet build` 0 warnings; `dotnet test` passes (no regressions)

## Proof Level

- This slice proves: contract + integration
- Real runtime required: yes (real pipeline execution in tests)
- Human/UAT required: no

## Verification

Tests in `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` (new file):
- `UsesPolicy_Scorer_NotPipelines` — `DryRunWithPolicy` with `InvertedRelevanceScorer` policy selects the lowest-scored items while a `ReflexiveScorer` pipeline would select the highest
- `UsesExplicitBudget_NotPipelineBudget` — same items, policy with `ScorerType.Reflexive`; budget is 100t (fits 1 item), pipeline's own budget is 500t — result includes exactly 1 item
- `UsesPolicy_Slicer_Greedy_vs_Knapsack` — same items at different token sizes; Greedy vs Knapsack policies produce different included sets
- `ThrowsArgumentNullException_WhenItemsNull` 
- `ThrowsArgumentNullException_WhenBudgetNull`
- `ThrowsArgumentNullException_WhenPolicyNull`

Tests added to `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` (existing file):
- `PolicyOverload_TwoPoliciesWithDifferentScorers_ProducesMeaningfulDiff` — two `CupelPolicy` objects with `ScorerType.Reflexive` and inverted scoring produce swinging diffs
- `PolicyOverload_ThrowsWhenFewerThanTwoVariants`
- `PolicyOverload_MatchesPipelineOverload_WhenEquivalentConfiguration` — policy-based call produces same diff as pipeline-based call when both use equivalent scorers and slicers

Acceptance commands:
```bash
dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental 2>&1 | grep -E "(error|warning)" | grep -v "^$"
dotnet test tests/Wollax.Cupel.Tests/ --filter "DryRunWithPolicy|PolicySensitivity"
dotnet test  # full regression
```

## Observability / Diagnostics

- Runtime signals: `ContextResult.Report` carries a full `SelectionReport` (same as `DryRunWithBudget`) — any test failure can be debugged by inspecting `result.Report.Included` / `result.Report.Excluded`
- Inspection surfaces: tests can call `result.Report!.Included` / `result.Report!.Excluded` to diagnose which items were selected and why
- Failure visibility: `ArgumentNullException` with parameter name; `InvalidOperationException` from `PipelineBuilder.Build()` if builder is misconfigured (missing budget)
- Redaction constraints: none — no secrets; test items use synthetic content strings

## Integration Closure

- Upstream surfaces consumed: `CupelPipeline.DryRunWithBudget` (internal, same assembly), `PipelineBuilder.WithPolicy(CupelPolicy)`, `PipelineBuilder.Build()`
- New wiring introduced in this slice: `CupelPipeline.DryRunWithPolicy` on the pipeline class; second overload in `PolicySensitivityExtensions`; two new `PublicAPI.Unshipped.txt` entries
- What remains before the milestone is truly usable end-to-end: S02 (Rust `Policy` struct + `dry_run_with_policy`), S03 (Rust `policy_sensitivity` + spec chapter)

## Tasks

- [x] **T01: Write failing tests for DryRunWithPolicy and the policy-based PolicySensitivity overload** `est:45m`
  - Why: Establishes the objective stopping condition before implementation. Tests must fail (APIs don't exist yet) to confirm they're exercising new code, not accidentally passing on existing surface.
  - Files: `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` (new), `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` (additions)
  - Do: Create `DryRunWithPolicyTests.cs` with 6 test methods: (1) policy scorer wins over pipeline scorer, (2) explicit budget used (not pipeline's), (3) policy slicer respected, (4–6) null-guard ArgumentNullException tests. Add 3 test methods to `PolicySensitivityTests.cs`: policy overload diff, policy overload throws on <2 variants, policy overload matches pipeline overload for equivalent configs. Use `ScorerType.Reflexive` and `ScorerType.Priority` for policy-based tests (no custom scorer needed in policy overload tests; custom `InvertedRelevanceScorer` private class in `DryRunWithPolicyTests.cs` for the scorer-wins test).
  - Verify: `dotnet build tests/Wollax.Cupel.Tests/` fails with CS0117 / member-not-found errors (expected — confirms tests reference the not-yet-existing API)
  - Done when: Test file compiles when the APIs exist (syntax is correct) but currently fails to build because `DryRunWithPolicy` and the policy `PolicySensitivity` overload don't exist yet

- [x] **T02: Implement DryRunWithPolicy, policy-based PolicySensitivity overload, and update PublicAPI.Unshipped.txt** `est:1h`
  - Why: Closes the slice — makes all failing tests pass and delivers the two public APIs.
  - Files: `src/Wollax.Cupel/CupelPipeline.cs`, `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs`, `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
  - Do: (1) In `CupelPipeline.cs`, add `public ContextResult DryRunWithPolicy(IReadOnlyList<ContextItem> items, ContextBudget budget, CupelPolicy policy)` — null-guard all three params, then `CreateBuilder().WithBudget(budget).WithPolicy(policy).Build().DryRunWithBudget(items, budget)`. XML doc must state the `CountQuota` gap (callers needing count-quota fork diagnostics use the pipeline-based overload) and the `SlicerType.Stream` sync fallback. (2) In `PolicySensitivityExtensions.cs`, add the second `PolicySensitivity` overload with `params (string Label, CupelPolicy Policy)[]` — same ≥2 variants guard; for each variant build a temp pipeline `CreateBuilder().WithBudget(budget).WithPolicy(variant.Policy).Build()` then call `tempPipeline.DryRunWithBudget(items, budget)`; reuse the same content-keyed diff loop. (3) In `PublicAPI.Unshipped.txt`, add entries for both new methods.
  - Verify: `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj 2>&1 | grep -c "warning"` → 0; `dotnet test tests/Wollax.Cupel.Tests/ --filter "DryRunWithPolicy|PolicySensitivity"` → all pass; `dotnet test` → no regressions
  - Done when: All tests in T01 pass; `dotnet build` 0 warnings; `PublicAPI.Unshipped.txt` contains both new method signatures; no existing tests broken

## Files Likely Touched

- `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` (new)
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` (additions)
- `src/Wollax.Cupel/CupelPipeline.cs`
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs`
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt`
