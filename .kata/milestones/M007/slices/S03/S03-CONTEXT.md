---
id: S03
milestone: M007
status: ready
---

# S03: Rust policy_sensitivity and spec chapter — Context

## Goal

Deliver the Rust `policy_sensitivity` free function (taking `&[(label, &Policy)]`), its output types, a rename of the existing pipeline-based variant, and the `spec/src/analytics/policy-sensitivity.md` spec chapter — completing R056 and closing M007.

## Why this Slice

S01 and S02 are complete. S03 is the final gate: Rust fork-diagnostic callers need a `policy_sensitivity` that accepts `Policy` objects directly (not pre-built pipelines), and the spec chapter is required for R056 validation. Order matters because S03 consumes the locked API shapes from both S01 (.NET) and S02 (Rust `Policy`, `PolicyBuilder`, `dry_run_with_policy`).

## Scope

### In Scope

- Rename existing `policy_sensitivity` (pipeline-based, from M004/S02) to `policy_sensitivity_from_pipelines` — the new policy-based function gets the primary name
- New `policy_sensitivity(items, budget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>` free function in `analytics.rs`
- `PolicySensitivityReport` and `PolicySensitivityDiffEntry` and `ItemStatus` are already in `analytics.rs`; no new types needed — the existing ones are reused
- Minimum-variants guard: return `Err(CupelError::PipelineConfig(...))` when fewer than 2 variants are passed — consistent with .NET behavior
- `lib.rs` re-export update: `policy_sensitivity_from_pipelines` added (or confirm it was already re-exported under the old name)
- Integration tests in `crates/cupel/tests/policy_sensitivity_from_policies.rs` (at least 3: all-swing, no-swing, partial-swing)
- `spec/src/analytics/policy-sensitivity.md` — new standalone file covering: API contract for both languages, type shapes (`PolicySensitivityReport`, `PolicySensitivityDiffEntry`, `ItemStatus`/`ItemStatus`), diff semantics (content-keyed, swing-only), minimum-variants rule, CupelPolicy gap note (.NET), explicit budget parameter rationale
- `spec/src/SUMMARY.md` updated to link the new chapter
- `CHANGELOG.md` updated with M007 entries
- R056 marked `validated` in `.kata/REQUIREMENTS.md`
- `cargo test --all-targets` passes; `cargo clippy --all-targets -- -D warnings` clean; `dotnet test` no regressions

### Out of Scope

- Renaming any `.NET` types or methods (S01 is locked and merged)
- Adding serde support for `Policy` (trait objects cannot be serialized)
- Conformance TOML vectors for `policy_sensitivity` (not required; integration tests are sufficient per milestone context)
- Adding `policy_sensitivity` to the `cupel-testing` crate (Rust testing vocabulary is a separate crate, and this is a free function, not an assertion)
- `policy_sensitivity_from_pipelines` behavioral changes — rename only, no logic changes

## Constraints

- **D149**: Rust `Policy` uses `Arc<dyn Trait>` — `policy_sensitivity` must call `run_with_components` via `Arc::clone(&policy.scorer).as_ref()` pattern (not `Box`); `pub(crate)` fields are accessible from `analytics.rs` (same crate)
- **D150**: `policy_sensitivity` is a free function in `analytics.rs`, not a `Pipeline` method
- **D113**: Content-keyed diff matching — items are matched across variants by `content()` string; reference equality is impossible since each `dry_run_with_policy` call produces distinct `ContextItem` instances
- **D155**: `run_with_components` is the correct internal hook for each variant run — `policy_sensitivity` should call `Pipeline::dry_run_with_policy` (public API), which already wires `run_with_components` correctly
- **Naming**: existing `policy_sensitivity` becomes `policy_sensitivity_from_pipelines`; the new function takes the primary name — affects `lib.rs` re-exports and any existing callers in tests (currently 2 tests in `crates/cupel/tests/policy_sensitivity.rs` that use the pipeline-based version)
- Spec chapter is a standalone new file (not merged into `budget-simulation.md`)

## Integration Points

### Consumes

- `crates/cupel/src/pipeline/mod.rs` — `Pipeline::dry_run_with_policy`, `Policy` (pub(crate) Arc fields), `run_with_components` (indirectly via `dry_run_with_policy`)
- `crates/cupel/src/analytics.rs` — existing `PolicySensitivityReport`, `PolicySensitivityDiffEntry`, `ItemStatus`, and the pipeline-based `policy_sensitivity` to rename
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — reference implementation for content-keyed diff algorithm and minimum-variants semantics
- `spec/src/analytics/budget-simulation.md` — structural template for the new spec chapter

### Produces

- `crates/cupel/src/analytics.rs` — `policy_sensitivity_from_pipelines` (renamed), `policy_sensitivity` (new, policy-based)
- `crates/cupel/src/lib.rs` — updated re-exports
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — ≥3 integration tests
- `spec/src/analytics/policy-sensitivity.md` — normative spec chapter
- `spec/src/SUMMARY.md` — updated with chapter link
- `CHANGELOG.md` — M007 entries
- `.kata/REQUIREMENTS.md` — R056 marked validated

## Open Questions

- **Re-export name in lib.rs**: The existing `policy_sensitivity` is re-exported from `lib.rs`. After the rename, callers of the pipeline-based version will see a compile error. Since this is an in-repo rename and the external crate is not yet published with this function as stable API, this is acceptable — but the rename must be done atomically with the re-export update. Current thinking: update `lib.rs` in the same commit as the rename in `analytics.rs`.
- **Test file for existing policy_sensitivity tests**: `crates/cupel/tests/policy_sensitivity.rs` uses the current (pipeline-based) function. After rename it will fail to compile. Those tests need to be updated to call `policy_sensitivity_from_pipelines`. Current thinking: update in T01 (red phase) alongside adding the new failing tests — makes the rename part of the red-state setup.
