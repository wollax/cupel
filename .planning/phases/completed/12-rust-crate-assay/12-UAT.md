# Phase 12: Rust Crate (Assay) — UAT

**Date:** 2026-03-14
**Tester:** User
**Status:** PASSED (8/8)

## Tests

| # | Test | Expected | Result |
|---|------|----------|--------|
| 1 | Crate compiles with zero warnings | `cargo check -p assay-cupel` exits 0, no warnings | PASS |
| 2 | Clippy passes with deny warnings | `cargo clippy -p assay-cupel -- -D warnings` exits 0 | PASS |
| 3 | All 28 conformance tests pass | `cargo test -p assay-cupel` shows 28 passed, 0 failed | PASS |
| 4 | Pipeline runs end-to-end (greedy+chronological) | Pipeline with RecencyScorer + GreedySlice + ChronologicalPlacer produces ordered output | PASS |
| 5 | ContextKind case-insensitive | ContextKind("Message") == ContextKind("message"), HashMap lookup works | PASS |
| 6 | ContextBudget rejects invalid config | target_tokens > max_tokens returns Err, negative max returns Err | PASS |
| 7 | Traits are Send + Sync | Pipeline wrapped in Arc, sent across threads compiles and runs | PASS |
| 8 | Crate consumable from assay workspace | `cargo check` at workspace root includes assay-cupel | PASS |
