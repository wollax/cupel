# Quick Task 001 — Fix Rust CI Formatting Failures

## Problem

CI (Rust) workflow failing on `cargo fmt --check` — formatting diffs in 3 source files and 1 test file.

## Tasks

### Task 1: Run cargo fmt

Run `cargo fmt` on the cupel crate to auto-fix all formatting issues.

**Files affected:**
- `crates/cupel/src/lib.rs` — import grouping
- `crates/cupel/src/model/context_budget.rs` — long line wrapping
- `crates/cupel/src/model/context_kind.rs` — one-liner fn expansion
- `crates/cupel/tests/serde.rs` — struct literal formatting

### Task 2: Verify

Run `cargo test --features serde` and `cargo fmt --check` to confirm fix.
