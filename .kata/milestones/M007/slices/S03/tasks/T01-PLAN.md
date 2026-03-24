---
estimated_steps: 4
estimated_files: 4
---

# T01: Red phase — rename existing function, add failing policy-based tests

**Slice:** S03 — Rust policy_sensitivity and spec chapter
**Milestone:** M007

## Description

Establish the failing-test contract before implementation. This task does two things atomically: (1) rename `policy_sensitivity` to `policy_sensitivity_from_pipelines` everywhere (analytics.rs, lib.rs, existing test file) so the rename is complete and existing tests still pass; (2) create `crates/cupel/tests/policy_sensitivity_from_policies.rs` with 3 named tests that reference the not-yet-existing `policy_sensitivity` function — confirming the red state for T02.

## Steps

1. In `crates/cupel/src/analytics.rs`: rename function `policy_sensitivity` → `policy_sensitivity_from_pipelines`. Update the function's doc comment to match the new name and note it accepts `Pipeline` references (to distinguish from the upcoming policy-based variant).
2. In `crates/cupel/src/lib.rs`: update the `pub use analytics::{ ... }` block — replace `policy_sensitivity` with `policy_sensitivity_from_pipelines` in the list. Keep all other re-exports unchanged.
3. In `crates/cupel/tests/policy_sensitivity.rs`: update `use cupel::{..., policy_sensitivity, ...}` to `policy_sensitivity_from_pipelines`; update the two call sites inside `policy_sensitivity_two_variants_produces_diff` and `policy_sensitivity_guaranteed_diff` to call `policy_sensitivity_from_pipelines(...)`. Run `cargo test --test policy_sensitivity` to confirm 2 tests pass.
4. Create `crates/cupel/tests/policy_sensitivity_from_policies.rs` with 3 `#[test]` functions: `all_items_swing`, `no_items_swing`, `partial_swing`. Each must import `cupel::{policy_sensitivity, Policy, PolicyBuilder, ...}` and reference `policy_sensitivity(...)` — which does not exist yet. Do NOT add any test logic beyond the function signature and a placeholder `todo!()` body; the goal is to confirm the compile error. Run `cargo test --test policy_sensitivity_from_policies` to confirm the expected compile error ("cannot find function `policy_sensitivity` in crate `cupel`" or similar).

## Must-Haves

- [ ] `policy_sensitivity_from_pipelines` exists in `analytics.rs` with updated doc comment
- [ ] `lib.rs` re-exports `policy_sensitivity_from_pipelines` (not the old name)
- [ ] `crates/cupel/tests/policy_sensitivity.rs` compiles and 2 tests pass under the new name
- [ ] `crates/cupel/tests/policy_sensitivity_from_policies.rs` exists with 3 named test functions that reference `policy_sensitivity`
- [ ] `cargo test --test policy_sensitivity_from_policies` fails with a compile error (function not found) — not a runtime failure

## Verification

```bash
# Rename is complete and existing tests pass
cargo test --test policy_sensitivity
# Expected: 2 passed, 0 failed

# New test file exists but fails to compile (red state confirmed)
cargo test --test policy_sensitivity_from_policies
# Expected: compilation error — "cannot find function `policy_sensitivity` in crate `cupel`"
```

## Observability Impact

- Signals added/changed: None — this task renames and scaffolds; no new runtime behavior
- How a future agent inspects this: `cargo test --test policy_sensitivity` confirms rename; compile error in `policy_sensitivity_from_policies` confirms red state
- Failure state exposed: Compile error message names exactly the missing function, making T02's implementation target unambiguous

## Inputs

- `crates/cupel/src/analytics.rs` — existing `policy_sensitivity` function to rename
- `crates/cupel/src/lib.rs` — re-export block to update
- `crates/cupel/tests/policy_sensitivity.rs` — existing 2 tests using old name

## Expected Output

- `crates/cupel/src/analytics.rs` — `policy_sensitivity_from_pipelines` function (renamed, doc updated)
- `crates/cupel/src/lib.rs` — updated re-exports
- `crates/cupel/tests/policy_sensitivity.rs` — updated call sites, 2 tests pass
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — new file with 3 failing test stubs
