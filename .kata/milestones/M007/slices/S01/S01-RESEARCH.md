# S01: .NET DryRunWithPolicy and policy-accepting PolicySensitivity — Research

**Researched:** 2026-03-24
**Domain:** .NET CupelPipeline / PolicySensitivityExtensions
**Confidence:** HIGH

## Summary

S01 adds two public APIs to the existing .NET library: `CupelPipeline.DryRunWithPolicy(items, budget, policy)` and a new `PolicySensitivity(items, budget, (label, policy)[])` overload. Both are thin wrappers over existing machinery — the hard work is already done.

The implementation path is clear: the cleanest approach for `DryRunWithPolicy` is to build a temporary `CupelPipeline` from the given `CupelPolicy` (using the existing `PipelineBuilder.WithPolicy()` mapping logic) and then call the existing internal `DryRunWithBudget(items, budget)` on it. This avoids duplicating the policy→concrete mapping logic and requires no changes to `ExecuteCore`. The policy-based `PolicySensitivity` overload does the same for each variant.

`CupelPolicy` has a documented gap: `SlicerType` has no `CountQuota` variant, so `DryRunWithPolicy` cannot express `CountQuotaSlice` pipelines. This must be documented in XML docs. No code workaround needed (D151).

## Recommendation

Implement `DryRunWithPolicy` as a method on `CupelPipeline` that:
1. Validates `items`, `budget`, `policy` are non-null
2. Builds a temporary `CupelPipeline` via `CreateBuilder().WithBudget(budget).WithPolicy(policy).Build()`
3. Calls `tempPipeline.DryRunWithBudget(items, budget)` on the temp pipeline

The policy-based `PolicySensitivity` overload follows the same pattern per variant, then runs the same content-keyed diff logic as the existing overload.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Policy→concrete mapping (scorer/slicer/placer) | `PipelineBuilder.WithPolicy()` + private `CreateScorer()` | Already handles all ScorerType/SlicerType/PlacerType variants, quota wiring, dedup flag, overflow strategy |
| Budget override in pipeline run | `CupelPipeline.DryRunWithBudget(items, budget)` (internal) | Already passes `budgetOverride` to `ExecuteCore`; used by `PolicySensitivity` today |
| Content-keyed diff between variants | `PolicySensitivityExtensions.PolicySensitivity()` inner diff loop | Reuse same `statusMap` diff algorithm for the new overload |

## Existing Code and Patterns

- `src/Wollax.Cupel/CupelPipeline.cs` — `DryRunWithBudget(items, temporaryBudget)` at line ~115: internal method that calls `ExecuteCore(items, trace, temporaryBudget)`; this is the execution seam for both budget simulation and policy-sensitivity variants. `DryRunWithPolicy` will call this on the temp pipeline.
- `src/Wollax.Cupel/PipelineBuilder.cs` — `WithPolicy(CupelPolicy policy)` maps `SlicerType`/`PlacerType`/`ScorerType` to concrete instances; private static `CreateScorer(ScorerEntry)` handles all scorer types including `Scaled` (recursive). `DryRunWithPolicy` reuses this via `CreateBuilder().WithPolicy(policy).Build()`.
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — existing `PolicySensitivity(items, budget, params (string, CupelPipeline)[])`. The new overload joins this file as a second overload, sharing the same diff algorithm.
- `src/Wollax.Cupel/CupelPolicy.cs` — validated constructor; `SlicerType` enum has `Greedy`, `Knapsack`, `Stream` only (no `CountQuota`). `DryRunWithPolicy`'s XML doc must state this limitation.
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — must list every new public member. Pattern: one entry per method signature in the standard PublicAPI analyzer format.

## Constraints

- `CupelPipeline` constructor is `internal` — cannot be constructed externally; temp pipeline must go through `PipelineBuilder`.
- `DryRunWithBudget` is `internal` — accessible from `DryRunWithPolicy` (same assembly) and from the policy-based `PolicySensitivity` overload via the temp pipeline (same assembly). No visibility changes needed.
- `PipelineBuilder.Build()` requires a budget — pass the explicit `budget` parameter to satisfy this. `DryRunWithBudget` then overrides it anyway, so there's no semantic difference.
- `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt` must be kept consistent. New entries go in `Unshipped.txt`. The PublicAPI analyzer will fail the build if any new public surface is undeclared.
- `dotnet build` must produce 0 warnings — XML doc must be complete; no `[Obsolete]` without reason; all parameters documented.
- The `PolicySensitivity` new overload is a static method (no `this` pipeline) — it creates a temp pipeline per variant internally. This is consistent with the existing overload which calls `variants[i].Pipeline.DryRunWithBudget(...)`.

## Common Pitfalls

- **Duplicating policy→concrete mapping** — Don't copy `CreateScorer` or the switch blocks from `PipelineBuilder`. Call `CreateBuilder().WithPolicy(policy).Build()` and let the builder do the work. Duplication will diverge when new scorer/placer types are added.
- **Missing budget on builder** — `PipelineBuilder.Build()` throws if no budget is set. Always call `.WithBudget(budget)` before `.WithPolicy(policy).Build()` on the temp pipeline.
- **CountQuota gap not documented** — The `CupelPolicy` XML doc must state that `CountQuotaSlice` pipelines cannot be expressed via `CupelPolicy`. Callers needing count-quota fork diagnostics must use the pipeline-based `PolicySensitivity` overload.
- **Forgetting PublicAPI.Unshipped.txt entries** — The PublicAPI analyzer fails the build for any undeclared public member. New entries needed:
  - `CupelPipeline.DryRunWithPolicy(...)` method signature
  - `PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string, CupelPolicy)[])` static method signature
- **Test naming** — The existing `PolicySensitivityTests.cs` uses `InvertedRelevanceScorer` as a private nested class. The new tests for the policy-based overload should use `CupelPolicy` with `ScorerType.Reflexive` and `ScorerType.Priority` (or similar) to produce meaningful diffs — no custom scorers needed.

## Open Risks

- **Stream slicer in `DryRunWithPolicy`**: `CupelPolicy` with `SlicerType.Stream` calls `UseGreedySlice()` as a sync fallback in `PipelineBuilder.WithPolicy()`. This means `DryRunWithPolicy` with a Stream policy runs GreedySlice, not streaming. The existing behavior of the builder is the correct precedent — document that Stream policies use GreedySlice sync fallback in `DryRunWithPolicy`.
- **Quotas in policy**: `WithPolicy()` wires `QuotaSlice` from `policy.Quotas` via `WithQuotas()`. This wraps the slicer in a `QuotaSlice`. `DryRunWithPolicy` will correctly pick this up since we call `WithPolicy()` on the builder. No special handling needed.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET / C# | none needed | n/a — standard C# patterns, no external library |

## Sources

- Codebase: `src/Wollax.Cupel/CupelPipeline.cs`, `PipelineBuilder.cs`, `Diagnostics/PolicySensitivityExtensions.cs`, `CupelPolicy.cs` — direct reads
- Decisions: D114 (DryRunWithBudget for PolicySensitivity), D148 (explicit budget required), D151 (CupelPolicy gap, documented not worked around)
