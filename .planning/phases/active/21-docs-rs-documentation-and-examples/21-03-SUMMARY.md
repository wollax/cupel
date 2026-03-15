---
phase: 21-docs-rs-documentation-and-examples
plan: 03
subsystem: documentation
tags: [examples, basic-pipeline, serde-roundtrip, quota-slicing, runnable-examples]
dependency-graph:
  requires: [21-01, 21-02]
  provides: [standalone-examples, examples-directory]
  affects: []
tech-stack:
  added: []
  patterns: [commented-walkthrough-examples]
key-files:
  created:
    - "crates/cupel/examples/basic_pipeline.rs"
    - "crates/cupel/examples/serde_roundtrip.rs"
    - "crates/cupel/examples/quota_slicing.rs"
  modified:
    - "crates/cupel/Cargo.toml"
decisions:
  - "Added [[example]] section with required-features for serde_roundtrip to prevent compilation without the serde feature flag"
  - "Pre-existing rustfmt issues in context_budget.rs and tests/serde.rs left untouched as they are outside task scope"
metrics:
  duration: "4m"
  completed: "2026-03-15"
---

# Plan 21-03 Summary: Standalone Runnable Examples

## What was done

Created three standalone runnable examples in `crates/cupel/examples/` serving as commented-walkthrough tutorials for new users discovering the library on docs.rs.

### Task 1: basic_pipeline.rs and serde_roundtrip.rs

- **basic_pipeline.rs**: Demonstrates the core pipeline flow — creates 7 context items across 5 kinds (SystemPrompt pinned, Message, ToolOutput, Document, Memory), configures a CompositeScorer (RecencyScorer + KindScorer), GreedySlice, and ChronologicalPlacer. Prints selected items with kind, tokens, and pinned status.
- **serde_roundtrip.rs**: Demonstrates JSON serialization/deserialization of ContextItem and ContextBudget, including a validation-on-deserialize failure case where target_tokens > max_tokens is rejected at the boundary.

### Task 2: quota_slicing.rs and final verification

- **quota_slicing.rs**: Demonstrates per-kind budget allocation with 11 items across 4 kinds, QuotaEntry instances with require/cap percentages (ToolOutput 30/50%, Document 20/40%, Message 10/60%), QuotaSlice with GreedySlice inner, KindScorer, and UShapedPlacer. Prints per-kind breakdown showing how quotas affected selection.
- Added `[[example]]` section in Cargo.toml with `required-features = ["serde"]` for serde_roundtrip.

## Verification results

| Check | Result |
|-------|--------|
| `cargo run --example basic_pipeline` | Pass — prints 7 selected items |
| `cargo run --example serde_roundtrip --features serde` | Pass — prints JSON roundtrip and validation error |
| `cargo run --example quota_slicing` | Pass — prints 6 selected items with per-kind breakdown |
| `cargo test` | Pass — 61 tests |
| `cargo test --all-features` | Pass — 94 tests |
| `cargo doc --no-deps --all-features` | Pass — zero warnings |
| `cargo package --list` | Pass — all 3 examples included |
| `cargo clippy --all-features --examples` | Pass — zero warnings |
| `rustfmt --check` (examples only) | Pass |

## Notes

- Pre-existing `rustfmt` issues exist in `src/model/context_budget.rs` and `tests/serde.rs` — not touched as they are outside task scope.
- The `serde_roundtrip` example requires `--features serde` at runtime; the `required-features` Cargo.toml entry ensures `cargo test` and `cargo build --examples` skip it when the feature is not enabled.
