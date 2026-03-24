---
estimated_steps: 6
estimated_files: 4
---

# T02: Implement policy_sensitivity and make all tests green

**Slice:** S03 — Rust policy_sensitivity and spec chapter
**Milestone:** M007

## Description

Implement the `policy_sensitivity` free function that accepts `&[(impl AsRef<str>, &Policy)]`, then complete the 3 integration tests so they exercise real behavioral contracts. The function lives in `analytics.rs` (same crate as `Policy`) and calls `dry_run_with_policy` per variant. The key architectural question is how to call `dry_run_with_policy` from a free function — the solution is to add a `pub(crate)` helper in `pipeline/mod.rs` that executes a policy run without requiring a `&self` `Pipeline` receiver, or alternatively use a minimal pipeline constructed from the policy's components.

**Key implementation insight from S02 forward intelligence:** `Policy` fields are `pub(crate)` Arc. `run_with_components` is private to `pipeline/mod.rs`. The cleanest approach: add `pub(crate) fn run_policy(items: &[ContextItem], budget: &ContextBudget, policy: &Policy) -> Result<SelectionReport, CupelError>` to `pipeline/mod.rs` — this is a free function in the same module that calls `run_with_components` directly using a `NullTraceCollector`, then returns `collector.into_report()`. `analytics.rs` imports and calls it. This avoids needing a `Pipeline` instance as receiver.

## Steps

1. In `crates/cupel/src/pipeline/mod.rs`: add `pub(crate) fn run_policy(items: &[ContextItem], budget: &ContextBudget, policy: &Policy) -> Result<SelectionReport, CupelError>` as a free function (not a method — no `self`). Confirmed approach: `run_with_components` takes `&self` for `self.kind_map` (the classifier stage's kind-to-class map). To avoid needing a `Pipeline` instance, add this `pub(crate)` free function that constructs an internal state directly: create `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`, then call `run_with_components_inner(items, budget, &default_kind_map(), policy.scorer.as_ref(), policy.slicer.as_ref(), policy.placer.as_ref(), policy.deduplication, policy.overflow_strategy, &mut collector)?` — but this requires extracting the classify stage's kind map. **Simpler confirmed approach**: add `pub(crate) fn run_policy(items: &[ContextItem], budget: &ContextBudget, policy: &Policy) -> Result<SelectionReport, CupelError>` to the `impl Pipeline` block as an associated function (no `self`). Inside it, build a temporary pipeline: since all three of scorer/slicer/placer are overridden by `dry_run_with_policy`, the dummy pipeline's own components don't matter — construct it using the policy's own Arc components wrapped in Box via a thin `ArcScorer(Arc<dyn Scorer>)` newtype wrapper that delegates, OR use the simplest possible real components (e.g., `GreedySlice`, `ChronologicalPlacer`, `ReflexiveScorer` as dummy values) and call `pipeline.dry_run_with_policy(items, budget, policy)` on the dummy. The dummy's scorer/slicer/placer are fully overridden by the policy. This avoids all Arc→Box complexity. **Implement this dummy-pipeline approach**: `let dummy = Pipeline::builder().scorer(Box::new(ReflexiveScorer)).slicer(Box::new(GreedySlice)).placer(Box::new(ChronologicalPlacer)).build().expect("dummy pipeline always valid"); dummy.dry_run_with_policy(items, budget, policy)`. Make this a `pub(crate)` free function or static method on `Pipeline`.
2. In `crates/cupel/src/analytics.rs`: add `use crate::pipeline::Policy;` (if not already present). Add `use crate::pipeline::run_policy;` (or equivalent import based on step 1 decision). Implement `pub fn policy_sensitivity`: guard `variants.len() < 2` → `return Err(CupelError::PipelineConfig("policy_sensitivity requires at least 2 variants".to_string()))`. For each `(label, policy)`, call `run_policy(items, budget, policy)?` (or the dummy-pipeline approach) and push `(label.as_ref().to_string(), report)` to `results`. Build the content-keyed status map and diff filter — copy the exact same algorithm from `policy_sensitivity_from_pipelines` (already proven correct). Return `Ok(PolicySensitivityReport { variants: results, diffs })`.
3. In `crates/cupel/src/lib.rs`: add `policy_sensitivity` to the `pub use analytics::{ ... }` block alongside `policy_sensitivity_from_pipelines`.
4. In `crates/cupel/tests/policy_sensitivity_from_policies.rs`: implement the 3 test bodies. **`all_items_swing`**: 2 items (40 tokens each), budget fitting 1 item (`max_tokens=50, target=40`), PolicyA uses `PriorityScorer` picking item-A (higher priority), PolicyB uses `ReflexiveScorer` picking item-B (higher relevance hint) — both items should appear in diffs with opposing statuses. **`no_items_swing`**: 2 identical policies (same PolicyBuilder config: `PriorityScorer`, `GreedySlice`, `ChronologicalPlacer`, `deduplication=true`, `overflow_strategy=Throw`) over 3 items with ample budget — both variants include the same items, diffs should be empty. **`partial_swing`**: 3 items (30 tokens each), budget fitting 2 items (`max_tokens=70, target=60`), one policy scores by relevance hint (picks top 2 by hint), another by priority (picks same top 2 by priority if they agree on item A but disagree on item C vs item B) — 1 item swings, 2 items don't. Adjust fixture to guarantee exactly one swing item. Use `KnapsackSlice::new(1)` if any test uses a tight budget with KnapsackSlice (D157 pattern).
5. Run `cargo test --all-targets` to confirm all tests pass including new 3.
6. Run `cargo clippy --all-targets -- -D warnings` to confirm clean. Fix any clippy issues (e.g., `clippy::too_many_arguments` if needed, dead code warnings, etc.).

## Must-Haves

- [ ] `policy_sensitivity` function exists in `analytics.rs` with minimum-variants guard
- [ ] `policy_sensitivity` returns correct diffs for all 3 behavioral scenarios (all-swing, no-swing, partial-swing)
- [ ] All 3 new integration tests in `policy_sensitivity_from_policies.rs` pass
- [ ] Existing 2 pipeline-based tests in `policy_sensitivity.rs` still pass
- [ ] `lib.rs` exports both `policy_sensitivity` and `policy_sensitivity_from_pipelines`
- [ ] `cargo test --all-targets` → all pass, 0 failed
- [ ] `cargo clippy --all-targets -- -D warnings` → 0 warnings

## Verification

```bash
cargo test --test policy_sensitivity_from_policies
# Expected: 3 passed (all_items_swing, no_items_swing, partial_swing)

cargo test --test policy_sensitivity
# Expected: 2 passed (unchanged)

cargo test --all-targets
# Expected: all passed, 0 failed

cargo clippy --all-targets -- -D warnings
# Expected: 0 warnings, 0 errors
```

## Observability Impact

- Signals added/changed: `policy_sensitivity` returns `Result<PolicySensitivityReport, CupelError>` — `report.diffs` is the primary inspection surface; `report.variants[i].1.included/excluded` gives per-variant detail
- How a future agent inspects this: `cargo test --test policy_sensitivity_from_policies` — named test failures identify which contract broke; `report.diffs.is_empty()` vs expected non-empty is the key assertion to inspect on failure
- Failure state exposed: `CupelError::PipelineConfig` for minimum-variants guard; `CupelError` from `dry_run_with_policy` propagates unchanged

## Inputs

- `crates/cupel/src/pipeline/mod.rs` — `run_with_components` (private), `Policy` pub(crate) Arc fields, `dry_run_with_policy` — review signature to decide implementation approach
- `crates/cupel/src/analytics.rs` — `policy_sensitivity_from_pipelines` algorithm as reference (content-keyed diff is identical)
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — T01's stub file with 3 `todo!()` test bodies to implement
- S02 forward intelligence: `Policy` fields are `pub(crate)` Arc; `run_with_components` takes `&self` for the pipeline's classify context

## Expected Output

- `crates/cupel/src/pipeline/mod.rs` — possibly `pub(crate) fn run_policy(...)` helper (or no change if dummy-pipeline approach used)
- `crates/cupel/src/analytics.rs` — `policy_sensitivity` function added with guard and diff algorithm
- `crates/cupel/src/lib.rs` — both functions exported
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — 3 passing integration tests
