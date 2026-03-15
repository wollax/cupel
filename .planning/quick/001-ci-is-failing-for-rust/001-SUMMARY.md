# Quick Task 001 — Summary

## What was done

Ran `cargo fmt` on the cupel crate to fix formatting diffs that were failing the CI `cargo fmt --check` step.

## Files changed

| File | Change |
|------|--------|
| `crates/cupel/src/lib.rs` | Re-wrapped `pub use model::` import list |
| `crates/cupel/src/model/context_budget.rs` | Wrapped long `serialize_field` call |
| `crates/cupel/src/model/context_kind.rs` | Expanded 5 one-liner factory fns to multi-line |
| `crates/cupel/tests/serde.rs` | Collapsed `ScoredItem` struct literal to single line |

## Verification

- `cargo fmt --check` — pass
- `cargo test --features serde` — 111 tests pass (4 suites)
