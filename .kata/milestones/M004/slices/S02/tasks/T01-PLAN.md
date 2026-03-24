---
estimated_steps: 5
estimated_files: 3
---

# T01: Rust PolicySensitivityReport types and implementation

**Slice:** S02 — PolicySensitivityReport — fork diagnostic
**Milestone:** M004

## Description

Add the fork diagnostic to the Rust `cupel` crate. Define `PolicySensitivityReport` and `PolicySensitivityDiffEntry` structs, implement the `policy_sensitivity` orchestration function that calls `dry_run` on each variant pipeline and computes a content-keyed diff, then prove it with an integration test using ≥2 pipeline configurations.

## Steps

1. Define types in `crates/cupel/src/analytics.rs`:
   - `PolicySensitivityDiffEntry` — `content: String`, `statuses: Vec<(String, ItemStatus)>` where `ItemStatus` is an enum `{ Included, Excluded }`. Derive `Debug, Clone, PartialEq`.
   - `PolicySensitivityReport` — `variants: Vec<(String, SelectionReport)>`, `diffs: Vec<PolicySensitivityDiffEntry>`. Derive `Debug, Clone, PartialEq`.
   - `ItemStatus` enum — `Included`, `Excluded`. Derive `Debug, Clone, Copy, PartialEq, Eq`.
2. Implement `policy_sensitivity` function:
   - Signature: `pub fn policy_sensitivity(items: &[ContextItem], budget: &ContextBudget, variants: &[(impl AsRef<str>, &Pipeline)]) -> Result<PolicySensitivityReport, CupelError>`
   - For each `(label, pipeline)` in variants: call `pipeline.dry_run(items, budget)?` to get a `SelectionReport`, collect as `(label.as_ref().to_string(), report)`.
   - Compute diff: build a `HashMap<String, Vec<(String, ItemStatus)>>` keyed by `item.content()`. For each variant report, iterate `included` (mark `Included`) and `excluded` (mark `Excluded`). Then filter to entries where not all statuses are the same — only items that actually differ across variants.
   - Return `PolicySensitivityReport { variants, diffs }`.
3. Re-export from `crates/cupel/src/lib.rs`: add `PolicySensitivityReport`, `PolicySensitivityDiffEntry`, `ItemStatus`, and `policy_sensitivity` to the `pub use analytics::` line.
4. Create integration test in `crates/cupel/tests/policy_sensitivity.rs`:
   - Build 2 pipelines with different scorers or slicer configs such that some items are included in one variant but excluded in the other (e.g., one pipeline with a tight budget that excludes low-priority items, another with a looser budget that includes them).
   - Call `policy_sensitivity` with both variants.
   - Assert: `variants.len() == 2`, both labeled correctly.
   - Assert: `diffs` is non-empty — at least one item has different status across variants.
   - Assert: each diff entry has exactly 2 statuses, one `Included` and one `Excluded` (or vice versa).
5. Run `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings` to verify.

## Must-Haves

- [ ] `PolicySensitivityReport` struct exists with `variants` and `diffs` fields
- [ ] `PolicySensitivityDiffEntry` struct exists with `content` and `statuses` fields
- [ ] `ItemStatus` enum with `Included` and `Excluded` variants
- [ ] `policy_sensitivity` function calls `dry_run` per variant and produces correct diff
- [ ] All three types re-exported from `lib.rs`
- [ ] Integration test proves ≥2 variants with a meaningful diff (at least one item swaps status)
- [ ] `cargo test --all-targets` passes
- [ ] `cargo clippy --all-targets -- -D warnings` clean

## Verification

- `cargo test --all-targets` — all tests pass including new `policy_sensitivity` integration test
- `cargo clippy --all-targets -- -D warnings` — no warnings
- Test asserts specific diff entries exist with correct per-variant `ItemStatus`

## Observability Impact

- Signals added/changed: None — pure analytics function
- How a future agent inspects this: Read `PolicySensitivityReport.diffs` — structured data showing exactly which items swing
- Failure state exposed: `CupelError` propagated from `dry_run` if any variant pipeline fails

## Inputs

- `crates/cupel/src/analytics.rs` — existing analytics module to extend
- `crates/cupel/src/lib.rs` — existing re-exports to extend
- S01 summary: `PartialEq` on `SelectionReport` is available but diff uses content-keyed matching, not report equality

## Expected Output

- `crates/cupel/src/analytics.rs` — extended with 3 new types + `policy_sensitivity` function
- `crates/cupel/src/lib.rs` — re-exports updated
- `crates/cupel/tests/policy_sensitivity.rs` — new integration test file
