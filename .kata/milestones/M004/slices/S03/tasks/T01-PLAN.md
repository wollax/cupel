---
estimated_steps: 5
estimated_files: 5
---

# T01: Rust QuotaPolicy trait, QuotaConstraint, and implementations

**Slice:** S03 — IQuotaPolicy abstraction + QuotaUtilization
**Milestone:** M004

## Description

Define the shared `QuotaPolicy` trait in Rust, along with `QuotaConstraint` and `QuotaConstraintMode` types. Implement the trait on both `QuotaSlice` and `CountQuotaSlice`. Re-export all new public types from `lib.rs`. This is the foundation that T02's analytics function will consume.

## Steps

1. In `crates/cupel/src/slicer/mod.rs`, define:
   - `QuotaConstraintMode` enum: `Percentage` and `Count` variants (derive `Debug, Clone, Copy, PartialEq`)
   - `QuotaConstraint` struct: `kind: ContextKind`, `mode: QuotaConstraintMode`, `require: f64`, `cap: f64` (derive `Debug, Clone, PartialEq`). For percentage mode, require/cap are the percentage values (0-100). For count mode, require/cap are the count values as f64.
   - `QuotaPolicy` trait: `fn quota_constraints(&self) -> Vec<QuotaConstraint>` — returns all per-kind constraints configured on this slicer
2. In `crates/cupel/src/slicer/quota.rs`, implement `QuotaPolicy` for `QuotaSlice` — iterate `self.quotas` and return `QuotaConstraint` entries with `mode: Percentage`
3. In `crates/cupel/src/slicer/count_quota.rs`, implement `QuotaPolicy` for `CountQuotaSlice` — iterate `self.entries` and return `QuotaConstraint` entries with `mode: Count` (require_count/cap_count as f64)
4. In `crates/cupel/src/lib.rs`, add `QuotaConstraint`, `QuotaConstraintMode`, and `QuotaPolicy` to the `pub use slicer::` re-export block
5. Run `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings` to confirm no regressions

## Must-Haves

- [ ] `QuotaPolicy` trait defined with `quota_constraints` method
- [ ] `QuotaConstraintMode` enum with `Percentage` and `Count` variants
- [ ] `QuotaConstraint` struct with kind, mode, require, cap fields
- [ ] `QuotaSlice` implements `QuotaPolicy` returning percentage-mode constraints
- [ ] `CountQuotaSlice` implements `QuotaPolicy` returning count-mode constraints
- [ ] All new types re-exported from `crates/cupel/src/lib.rs`
- [ ] Existing tests pass unchanged

## Verification

- `cargo test --all-targets` — all existing tests pass
- `cargo clippy --all-targets -- -D warnings` — clean
- Spot-check: `rg "impl QuotaPolicy for" crates/cupel/src/slicer/` returns 2 results (QuotaSlice + CountQuotaSlice)

## Observability Impact

- None — trait and types are pure data; no runtime behavior change

## Inputs

- `crates/cupel/src/slicer/quota.rs` — QuotaSlice with `Vec<QuotaEntry>` (kind, require%, cap%)
- `crates/cupel/src/slicer/count_quota.rs` — CountQuotaSlice with `Vec<CountQuotaEntry>` (kind, require_count, cap_count)
- `crates/cupel/src/slicer/mod.rs` — Slicer trait definition, re-exports

## Expected Output

- `crates/cupel/src/slicer/mod.rs` — QuotaConstraintMode, QuotaConstraint, QuotaPolicy trait defined and re-exported
- `crates/cupel/src/slicer/quota.rs` — `impl QuotaPolicy for QuotaSlice`
- `crates/cupel/src/slicer/count_quota.rs` — `impl QuotaPolicy for CountQuotaSlice`
- `crates/cupel/src/lib.rs` — new types in re-export block
