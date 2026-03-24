---
id: T02
parent: S03
milestone: M007
provides:
  - "`policy_sensitivity` free function in `analytics.rs` accepting `&[(impl AsRef<str>, &Policy)]` with minimum-variants guard"
  - "`pub(crate) fn run_policy(items, budget, policy)` standalone function in `pipeline/mod.rs` using dummy-pipeline approach"
  - "3 integration tests in `tests/policy_sensitivity_from_policies.rs`: all_items_swing, no_items_swing, partial_swing"
  - "`policy_sensitivity` exported from `lib.rs` alongside `policy_sensitivity_from_pipelines`"
  - "All tests green: 80 unit + 17 integration tests pass; clippy clean"
key_files:
  - "crates/cupel/src/pipeline/mod.rs"
  - "crates/cupel/src/analytics.rs"
  - "crates/cupel/src/lib.rs"
  - "crates/cupel/tests/policy_sensitivity_from_policies.rs"
key_decisions:
  - "Dummy-pipeline approach for run_policy: constructs a temporary Pipeline with ReflexiveScorer/GreedySlice/ChronologicalPlacer, then calls dry_run_with_policy — avoids Arc→Box complexity and need to access internal pipeline state"
  - "run_policy placed as a free function (not method) outside the impl Pipeline block in pipeline/mod.rs, using local imports of the dummy scorers"
patterns_established:
  - "`pub(crate) fn run_policy` in pipeline/mod.rs is the canonical bridge from analytics → policy execution; mirrors dry_run_with_policy but needs no Pipeline receiver"
  - "Test fixture pattern for policy_sensitivity: PriorityScorer vs ReflexiveScorer over items with orthogonal priority/relevance values creates guaranteed divergence with tight budget"
observability_surfaces:
  - "`cargo test --test policy_sensitivity_from_policies` — named tests identify which behavioral contract broke"
  - "`report.diffs` is the primary inspection surface; `.variants[i].1.included/excluded` gives per-variant detail"
  - "`CupelError::PipelineConfig` for minimum-variants guard; `CupelError` from dry_run_with_policy propagates unchanged"
duration: 15min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Implement policy_sensitivity and make all tests green

**Added `policy_sensitivity` free function accepting `&[(label, &Policy)]` with a `run_policy` bridge helper, plus 3 green integration tests covering all-swing, no-swing, and partial-swing behavioral contracts.**

## What Happened

Implemented in three parts:

**1. `run_policy` bridge (pipeline/mod.rs):** Added a `pub(crate)` free function outside the `impl Pipeline` block that constructs a throwaway `Pipeline` with dummy `ReflexiveScorer`/`GreedySlice`/`ChronologicalPlacer` components, then calls `dry_run_with_policy`. Since the policy fully overrides scorer/slicer/placer/flags, the dummy pipeline's own components never affect the result. This avoids Arc→Box newtype complexity and doesn't require access to internal pipeline fields.

**2. `policy_sensitivity` function (analytics.rs):** Added the function with a minimum-variants guard (`< 2 → CupelError::PipelineConfig`). The diff algorithm is identical to `policy_sensitivity_from_pipelines` — content-keyed HashMap, filter to entries where at least one status differs from the first. Imported `run_policy` and `Policy` from `crate::pipeline`.

**3. Integration tests (policy_sensitivity_from_policies.rs):** Replaced the three `todo!()` stubs with real test bodies:
- `all_items_swing`: 2 items, tight budget fitting 1. item-a has `priority=10, relevance=0.1`; item-b has `priority=1, relevance=0.9`. PriorityScorer picks item-a; ReflexiveScorer picks item-b. Both items appear in diffs.
- `no_items_swing`: 3 items, ample budget, two identical PriorityScorer policies. All 3 items included by both; diffs empty.
- `partial_swing`: 3 items at 30 tokens each, budget fits 2. "stable" has high priority and high relevance (included by both). "swing-relevance" has low priority / high relevance (included by ReflexiveScorer only). "swing-priority" has high priority / low relevance (included by PriorityScorer only). Used `KnapsackSlice::new(1).unwrap()` for tight deterministic selection. Diffs contain exactly 2 items.

One clippy fix required: a doc comment continuation line in the test file needed 4-space indent per `clippy::doc-lazy-continuation`.

## Verification

```
cargo test --test policy_sensitivity_from_policies   → 3 passed (all_items_swing, no_items_swing, partial_swing)
cargo test --test policy_sensitivity                 → 2 passed (unchanged)
cargo test --all-targets                             → all passed (80 unit + 17 integration tests)
cargo clippy --all-targets -- -D warnings            → 0 warnings, 0 errors
```

## Diagnostics

- `cargo test --test policy_sensitivity_from_policies` — named test failures identify which behavioral contract broke
- `report.diffs` (non-empty when items swing, empty when identical coverage) is the primary assertion surface
- `report.variants[i].1.included/excluded` gives per-variant detail for diagnosing disagreements
- `CupelError::PipelineConfig("policy_sensitivity requires at least 2 variants")` for minimum-variants guard

## Deviations

None. Used dummy-pipeline approach as specified. `KnapsackSlice::new(1)` required `.unwrap()` since it returns `Result<KnapsackSlice, CupelError>` — this was a minor correction during compilation, not a plan deviation.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/pipeline/mod.rs` — Added `pub(crate) fn run_policy(...)` free function
- `crates/cupel/src/analytics.rs` — Added `policy_sensitivity` function and updated imports
- `crates/cupel/src/lib.rs` — Added `policy_sensitivity` to the `pub use analytics::{ ... }` block
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — Replaced `todo!()` stubs with 3 passing integration tests
