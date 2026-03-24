---
id: T01
parent: S02
milestone: M007
provides:
  - crates/cupel/tests/dry_run_with_policy.rs — 5 failing integration tests defining the behavioral contracts for Policy, PolicyBuilder, and Pipeline::dry_run_with_policy
key_files:
  - crates/cupel/tests/dry_run_with_policy.rs
key_decisions:
  - KnapsackSlice requires KnapsackSlice::with_default_bucket_size() for zero-arg construction (bucket_size param is mandatory via new(), but with_default_bucket_size() is the convenient form)
  - overflow_strategy_is_respected uses a pinned item (110 tokens) with target=100 to trigger CupelError::Overflow via the Throw path; PinnedExceedsBudget is NOT triggered because max=200 > pinned(110)
patterns_established:
  - Integration tests use Arc<dyn Trait> wrapping for policy components; Box<dyn Trait> for host pipeline components — reflects the design intent of Policy holding shared references
observability_surfaces:
  - none (test file only)
duration: 15m
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T01: Write failing integration tests for dry_run_with_policy

**Created `crates/cupel/tests/dry_run_with_policy.rs` with 5 integration tests that compile-fail only on missing `Policy`, `PolicyBuilder`, and `dry_run_with_policy` symbols — the correct red phase.**

## What Happened

Created `crates/cupel/tests/dry_run_with_policy.rs` with 5 named `#[test]` functions covering the behavioral contracts for the upcoming `Policy`/`PolicyBuilder`/`Pipeline::dry_run_with_policy` implementation.

Two minor adjustments during writing:
1. `KnapsackSlice` requires a `bucket_size` — used `KnapsackSlice::with_default_bucket_size()` rather than bare struct instantiation.
2. Removed `ContextKind` from the import block since no test needed it (compiler warning).

The overflow test uses a pinned item of 110 tokens with target=100, max=200. `classify` accepts pinned items up to `max - output_reserve` (200), so no `PinnedExceedsBudget` error. The `place` stage then detects merged(110) > target(100) and returns `CupelError::Overflow` under `OverflowStrategy::Throw`, while `OverflowStrategy::Truncate` proceeds.

## Verification

```
cargo check --test dry_run_with_policy 2>&1 | grep "^error\[E" | grep -v "cannot find\|unresolved import\|no method named" | head -10
# → zero lines (all errors are missing-symbol errors only)
```

The compile errors present are exactly:
- `E0432: unresolved imports cupel::Policy, cupel::PolicyBuilder`
- `E0599: no method named dry_run_with_policy found for struct Pipeline` (×5)

No syntax errors, no logic errors, no structural issues.

5 `#[test]` functions confirmed:
- `scorer_is_respected`
- `slicer_is_respected`
- `deduplication_false_allows_duplicates`
- `deduplication_true_excludes_duplicates`
- `overflow_strategy_is_respected`

## Diagnostics

When T02 delivers the implementation, `cargo test --test dry_run_with_policy` will surface which of the 5 contracts pass or fail. Each test name clearly identifies the failing contract.

## Deviations

- Used `KnapsackSlice::with_default_bucket_size()` instead of bare `KnapsackSlice` — required by its constructor signature; not anticipated in the plan but not a blocker.
- Removed `ContextKind` from the import block (not needed by any test).

## Known Issues

None. The only open item is T02+T03 delivering the implementation so these tests pass.

## Files Created/Modified

- `crates/cupel/tests/dry_run_with_policy.rs` — new file with 5 failing integration tests
