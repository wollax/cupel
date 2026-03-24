---
estimated_steps: 4
estimated_files: 2
---

# T01: Add PartialEq derives to Rust diagnostic types

**Slice:** S01 — SelectionReport structural equality
**Milestone:** M004

## Description

Add `PartialEq` to the derive macros on the 6 Rust diagnostic structs that currently lack it: `IncludedItem`, `ExcludedItem`, `SelectionReport`, `TraceEvent`, `CountRequirementShortfall`, and `OverflowEvent`. Do NOT add `Eq` — `f64` fields (`score`, `duration_ms`, `tokens_over_budget`) prevent it. Create a dedicated equality test file exercising all types.

Already have `PartialEq`: `ContextItem`, `ExclusionReason`, `InclusionReason`, `PipelineStage`. No changes needed for those.

## Steps

1. In `crates/cupel/src/diagnostics/mod.rs`, add `PartialEq` to the `#[derive(...)]` on these 6 structs:
   - `CountRequirementShortfall` (line ~22): `Debug, Clone` → `Debug, Clone, PartialEq`
   - `TraceEvent` (line ~64): `Debug, Clone` → `Debug, Clone, PartialEq`
   - `OverflowEvent` (line ~86): `Debug, Clone` → `Debug, Clone, PartialEq`
   - `IncludedItem` (line ~260): `Debug, Clone` → `Debug, Clone, PartialEq`
   - `ExcludedItem` (line ~282): `Debug, Clone` → `Debug, Clone, PartialEq`
   - `SelectionReport` (line ~312): `Debug, Clone` → `Debug, Clone, PartialEq`
2. Create `crates/cupel/tests/equality.rs` with tests:
   - Two identical `SelectionReport` instances compare equal
   - Reports differing in one `IncludedItem` score compare unequal
   - Reports differing in `ExcludedItem` reason compare unequal
   - Reports differing in events compare unequal
   - Empty reports (no items, no events) compare equal
   - `IncludedItem` equality with same/different ContextItem
   - `ExcludedItem` equality with same/different ExclusionReason
   - `TraceEvent` equality with same/different stage and duration_ms
   - `CountRequirementShortfall` equality
3. Run `cargo test --all-targets` and `cargo test --all-targets --features serde`
4. Run `cargo clippy --all-targets -- -D warnings`

## Must-Haves

- [ ] `PartialEq` derived on all 6 structs (`IncludedItem`, `ExcludedItem`, `SelectionReport`, `TraceEvent`, `CountRequirementShortfall`, `OverflowEvent`)
- [ ] `Eq` NOT added to any type with f64 fields
- [ ] `crates/cupel/tests/equality.rs` exists with ≥8 test functions covering all 6 types
- [ ] `cargo test --all-targets` passes
- [ ] `cargo clippy --all-targets -- -D warnings` clean

## Verification

- `cargo test --all-targets` — all tests pass including new equality tests
- `cargo test --all-targets --features serde` — serde still works
- `cargo clippy --all-targets -- -D warnings` — no new warnings
- `grep -c "PartialEq" crates/cupel/src/diagnostics/mod.rs` — count increased by 6

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: `cargo test --all-targets` exercises equality
- Failure state exposed: None

## Inputs

- `crates/cupel/src/diagnostics/mod.rs` — current derive annotations on all diagnostic types
- S01-RESEARCH.md — confirms which types need PartialEq and which already have it

## Expected Output

- `crates/cupel/src/diagnostics/mod.rs` — 6 structs now derive `PartialEq`
- `crates/cupel/tests/equality.rs` — new integration test file with ≥8 equality tests
