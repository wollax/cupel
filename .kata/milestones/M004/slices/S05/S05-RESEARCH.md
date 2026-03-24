# S05: Rust budget simulation parity — Research

**Date:** 2026-03-23

## Summary

S05 implements `get_marginal_items` and `find_min_budget_for` on the Rust `Pipeline`, matching the .NET `BudgetSimulationExtensions` API. The Rust implementation is significantly simpler than .NET because `Pipeline::dry_run` already accepts an explicit `budget: &ContextBudget` parameter — there is no internal `DryRunWithBudget` seam to create. The .NET version required an internal method because `CupelPipeline` stores a fixed budget; the Rust `Pipeline` has no stored budget at all.

The main design challenge is the monotonicity guard. The `.NET` implementation uses runtime type checks (`pipeline.Slicer is QuotaSlice` and `pipeline.Slicer is QuotaSlice or CountQuotaSlice`). Rust's `dyn Slicer` trait objects don't support downcasting. The existing pattern is `is_knapsack()` — a defaulted method on the `Slicer` trait (D085). We need analogous `is_quota()` and `is_count_quota()` methods (or a single `is_monotonic()` inverse) to implement the guards without `Any`/TypeId complexity.

## Recommendation

Implement budget simulation as two public methods on `Pipeline` (not free functions in `analytics.rs`) to match the .NET extension-method-on-pipeline pattern. Add `is_quota()` and `is_count_quota()` defaulted methods (both returning `false`) to the `Slicer` trait, overridden in `QuotaSlice` and `CountQuotaSlice` respectively. This follows the established `is_knapsack()` pattern (D085) and keeps the trait clean.

The methods should live in a new `crates/cupel/src/pipeline/budget_simulation.rs` module (or inline in `pipeline/mod.rs` behind a section comment) to keep the pipeline module organized.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Pipeline execution at different budgets | `Pipeline::dry_run(&self, items, budget)` | Already takes explicit budget — no temporary-budget seam needed |
| Item identity comparison across runs | Content-based matching via `item.content()` | Same pattern used by `policy_sensitivity` (D113); reference equality is impossible across independent dry runs |
| Monotonicity type check | `Slicer::is_knapsack()` trait pattern (D085) | Extend with `is_quota()` / `is_count_quota()` — avoids `Any`/TypeId |

## Existing Code and Patterns

- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — The .NET reference implementation. Port logic from here. Key differences: .NET uses `DryRunWithBudget` (internal); Rust uses public `dry_run`. .NET uses reference equality (`ReferenceEqualityComparer`); Rust must use content-based matching (items are cloned during pipeline execution).
- `crates/cupel/src/pipeline/mod.rs` — `Pipeline::dry_run` accepts `&[ContextItem]` + `&ContextBudget`, returns `Result<SelectionReport, CupelError>`. This is the primitive both methods build on.
- `crates/cupel/src/analytics.rs` — Houses `policy_sensitivity` (D113 content-keyed matching pattern), `budget_utilization`, `kind_diversity`, `quota_utilization`. Budget simulation methods go on `Pipeline` (not here) because they need `&self` access to the pipeline's slicer for monotonicity guards.
- `crates/cupel/src/slicer/mod.rs` — `Slicer` trait with `is_knapsack()` defaulted method. Add `is_quota()` and `is_count_quota()` here.
- `crates/cupel/src/slicer/quota.rs` — `QuotaSlice` — override `is_quota() → true`.
- `crates/cupel/src/slicer/count_quota.rs` — `CountQuotaSlice` — override `is_count_quota() → true`.
- `crates/cupel/src/model/context_budget.rs` — `ContextBudget::new(max_tokens, target_tokens, output_reserve, reserved_slots, estimation_safety_margin_percent)` constructor for creating reduced budgets.
- `spec/src/analytics/budget-simulation.md` — Spec chapter with pseudocode, preconditions, and error messages.

## Constraints

- **No stored budget on Rust Pipeline** — Rust `Pipeline` does not store a budget (unlike .NET `CupelPipeline`). Methods take `budget` as a parameter directly. This is simpler.
- **Item identity is content-based in Rust** — Pipeline stages clone items internally. Reference equality (`ReferenceEquals` in .NET) is impossible. Use `content()` string matching, consistent with `policy_sensitivity` (D113).
- **`ContextBudget::new` requires 5 params** — For reduced budgets, we need to propagate `output_reserve` and `reserved_slots` from the original budget. `ContextBudget` has public accessors: `max_tokens()`, `target_tokens()`, `output_reserve()`. Need to check if `reserved_slots()` is accessible.
- **`Slicer` trait is public** — Adding `is_quota()` / `is_count_quota()` with default `false` is non-breaking (same pattern as D085 `is_knapsack()`).
- **Error type** — `GetMarginalItems` throws `InvalidOperationException` in .NET. Rust equivalent: return `Err(CupelError::SlicerConfig(...))` or a new variant. Check what fits best.

## Common Pitfalls

- **Content-based matching edge case: duplicate content items** — If two items have identical `content()` but different tokens/metadata, content-keyed matching via `HashMap` will collide. The .NET version uses reference equality which doesn't have this issue. However, `policy_sensitivity` already uses content-based matching (D113) and this is the established Rust pattern. For budget simulation, the same items slice is passed to both runs, so content duplicates would behave consistently across runs — the diff is still meaningful.
- **ContextBudget reduced budget construction** — Must propagate `output_reserve` and `reserved_slots` from the original. The .NET code creates `new ContextBudget(maxTokens: budget.MaxTokens - slackTokens, targetTokens: budget.TargetTokens - slackTokens, outputReserve: budget.OutputReserve)` — note it does NOT propagate `reservedSlots`. Need to match this behavior exactly. For `FindMinBudgetFor`, the .NET code creates `new ContextBudget(maxTokens: mid, targetTokens: mid)` — using default `outputReserve=0` and empty `reservedSlots`. Must replicate.
- **Binary search off-by-one** — The .NET code checks both `low` and `high` after the loop exits. The spec pseudocode only checks `high`. The .NET implementation is more thorough — port the .NET behavior (check `low` first, then `high`).
- **`is_quota` on QuotaSlice used as inner slicer** — A `CountQuotaSlice` wraps an inner slicer. The monotonicity guard for `GetMarginalItems` checks only `QuotaSlice`, not `CountQuotaSlice`. For `FindMinBudgetFor`, both are checked. Must match these asymmetric guards exactly.

## Open Risks

- **`reserved_slots` propagation in reduced budgets** — Need to verify `ContextBudget::new` handles the `HashMap::new()` / `Default::default()` case correctly for `GetMarginalItems`'s reduced budget construction. The .NET code doesn't propagate reserved slots — the `ContextBudget` constructor signature takes 3 params there (max, target, outputReserve). Rust's constructor takes 5. Need to pass `HashMap::default()` and `0.0` for the extra params.
- **Error variant choice** — `CupelError` has `SlicerConfig(String)` and `PipelineConfig(String)`. Budget simulation guards are runtime checks on the pipeline's slicer type — `PipelineConfig` or `SlicerConfig` could both work. Choose whichever is most consistent with the error's meaning ("this pipeline configuration doesn't support budget simulation").

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | n/a | No Rust agent skills found — standard Rust library development, no exotic dependencies |

## Sources

- `spec/src/analytics/budget-simulation.md` — Spec chapter with pseudocode, preconditions, error messages (HIGH confidence — canonical spec)
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — .NET reference implementation (HIGH confidence — port source)
- `crates/cupel/src/pipeline/mod.rs` — Rust Pipeline API (HIGH confidence — integration target)
- `crates/cupel/src/slicer/mod.rs` — Slicer trait with `is_knapsack()` pattern (HIGH confidence — extension point)
- D069, D085, D098, D099, D113 from DECISIONS.md — locked design decisions governing API shape and patterns
