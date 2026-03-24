---
id: T01
parent: S03
milestone: M007
provides:
  - "`policy_sensitivity_from_pipelines` renamed from `policy_sensitivity` in analytics.rs with updated doc comment"
  - "lib.rs re-exports `policy_sensitivity_from_pipelines` (old name removed)"
  - "Existing 2 tests in `tests/policy_sensitivity.rs` updated and passing under new name"
  - "`tests/policy_sensitivity_from_policies.rs` created with 3 named failing stubs: all_items_swing, no_items_swing, partial_swing"
  - "Red state confirmed: compile error `no policy_sensitivity in the root` targets T02's exact implementation goal"
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/tests/policy_sensitivity.rs
  - crates/cupel/tests/policy_sensitivity_from_policies.rs
key_decisions:
  - "Rename is complete; old name `policy_sensitivity` is fully vacated so T02 can introduce it as the new policy-based variant without ambiguity"
patterns_established:
  - "Red-phase stubs use `todo!()` bodies with real import paths to produce compile-error failures (not runtime panics)"
observability_surfaces:
  - "`cargo test --test policy_sensitivity` — 2 pass (rename regression)"
  - "`cargo test --test policy_sensitivity_from_policies` — compile error confirming red state"
duration: 10min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Red phase — rename existing function, add failing policy-based tests

**Renamed `policy_sensitivity` → `policy_sensitivity_from_pipelines` across 3 files and scaffolded 3 compile-failing test stubs that define T02's implementation target.**

## What Happened

Executed the four-step rename atomically:

1. `analytics.rs`: renamed `policy_sensitivity` to `policy_sensitivity_from_pipelines`, updated doc comment to clarify it accepts `&Pipeline` references and distinguish it from the upcoming policy-based variant.
2. `lib.rs`: replaced `policy_sensitivity` with `policy_sensitivity_from_pipelines` in the `pub use analytics::{ ... }` block.
3. `tests/policy_sensitivity.rs`: updated the `use` import and both call sites (`policy_sensitivity_two_variants_produces_diff`, `policy_sensitivity_guaranteed_diff`) to use the new name.
4. Created `tests/policy_sensitivity_from_policies.rs` with 3 named test functions (`all_items_swing`, `no_items_swing`, `partial_swing`). Each imports `cupel::policy_sensitivity` (which doesn't exist) and uses real types (`Policy`, `PolicyBuilder`, `ContextBudget`) with a `todo!()` body.

## Verification

```
cargo test --test policy_sensitivity
# → 2 passed (policy_sensitivity_two_variants_produces_diff, policy_sensitivity_guaranteed_diff)

cargo test --test policy_sensitivity_from_policies
# → error[E0432]: unresolved import `cupel::policy_sensitivity`
#   no `policy_sensitivity` in the root
# Compile error — red state confirmed, runtime failure not reached
```

## Diagnostics

- `cargo test --test policy_sensitivity` — regression check for rename; must stay green after T02
- `cargo test --test policy_sensitivity_from_policies` — flips from compile-error to passing after T02 adds the function

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — `policy_sensitivity` renamed to `policy_sensitivity_from_pipelines`, doc updated
- `crates/cupel/src/lib.rs` — re-export updated to `policy_sensitivity_from_pipelines`
- `crates/cupel/tests/policy_sensitivity.rs` — import and 2 call sites updated to new name
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — new file, 3 failing test stubs
