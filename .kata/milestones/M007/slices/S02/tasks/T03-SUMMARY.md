---
id: T03
parent: S02
milestone: M007
provides:
  - crates/cupel/tests/dry_run_with_policy.rs — all 5 integration tests passing (no changes needed)
key_files:
  - crates/cupel/tests/dry_run_with_policy.rs
key_decisions: []
patterns_established: []
observability_surfaces:
  - cargo test --test dry_run_with_policy — direct check; all 5 named tests report ok
duration: ~5m
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# T03: Make dry_run_with_policy integration tests pass

**All 5 integration tests in `dry_run_with_policy.rs` passed on first run — no fixes needed.**

## What Happened

T02 delivered a complete, correct implementation of `Policy`, `PolicyBuilder`, and `Pipeline::dry_run_with_policy`. When T03 ran `cargo test --test dry_run_with_policy`, all 5 tests passed immediately:

- `scorer_is_respected` — ok
- `slicer_is_respected` — ok
- `deduplication_false_allows_duplicates` — ok
- `deduplication_true_excludes_duplicates` — ok
- `overflow_strategy_is_respected` — ok

No changes to `dry_run_with_policy.rs` or `pipeline/mod.rs` were required.

## Verification

```bash
cd crates/cupel
cargo test --test dry_run_with_policy
# 5 passed; 0 failed

cargo test --all-targets
# 159 tests across all test binaries; 0 failed

cargo clippy --all-targets -- -D warnings
# Finished; 0 warnings
```

All must-haves confirmed:
- [x] All 5 tests in `crates/cupel/tests/dry_run_with_policy.rs` report `ok`
- [x] No existing tests broken (`cargo test --all-targets` exits 0)
- [x] `cargo clippy --all-targets -- -D warnings` exits 0
- [x] `scorer_is_respected` proves policy scorer was used (PriorityScorer picks C+B, not A+B)
- [x] `deduplication_false_allows_duplicates` asserts `report.included.len() == 2`
- [x] `overflow_strategy_is_respected` proves policy `overflow_strategy` governs behavior

## Diagnostics

- `cargo test --test dry_run_with_policy` is the direct check for S02 behavioral contracts
- Test names clearly identify which contract they cover; `--nocapture` shows assertion values on failure

## Deviations

None — T02 delivered all required implementation; T03 required no code changes.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel/tests/dry_run_with_policy.rs` — unchanged; all 5 tests pass as written
