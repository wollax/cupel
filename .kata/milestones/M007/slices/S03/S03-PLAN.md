# S03: Rust policy_sensitivity and spec chapter

**Goal:** Add the Rust `policy_sensitivity` free function accepting `&[(label, &Policy)]`, rename the existing pipeline-based variant to `policy_sensitivity_from_pipelines`, write the `spec/src/analytics/policy-sensitivity.md` spec chapter, and mark R056 validated.
**Demo:** A Rust caller can `use cupel::policy_sensitivity;` with `Policy` objects (no pipeline construction) and get a `PolicySensitivityReport`; the spec chapter documents both languages; `cargo test --all-targets` and `dotnet test` pass; R056 is validated.

## Must-Haves

- `policy_sensitivity_from_pipelines` exists in `analytics.rs` (renamed from `policy_sensitivity`); re-exported from `lib.rs`; existing 2 pipeline-based tests updated and still passing
- `policy_sensitivity(items, budget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>` exists in `analytics.rs`, exported from `lib.rs`
- `policy_sensitivity` returns `Err(CupelError::PipelineConfig(...))` when fewer than 2 variants are passed
- `policy_sensitivity` produces correct diffs: content-keyed, swing-only (items where not all variants agree), using `dry_run_with_policy` per variant
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` contains ≥3 integration tests (all-swing, no-swing, partial-swing) — all passing
- `spec/src/analytics/policy-sensitivity.md` exists and covers: API contract, type shapes, diff semantics, minimum-variants rule, CupelPolicy gap note, explicit budget rationale
- `spec/src/SUMMARY.md` links the new chapter under Analytics
- `CHANGELOG.md` contains an unreleased/v1.2.x section with M007 entries
- R056 marked `validated` in `.kata/REQUIREMENTS.md`
- `cargo test --all-targets` passes (no regressions); `cargo clippy --all-targets -- -D warnings` clean; `dotnet test` passes (no regressions)

## Proof Level

- This slice proves: contract + integration
- Real runtime required: yes (cargo test with real pipeline runs)
- Human/UAT required: no

## Verification

```bash
# Red → Green integration tests
cargo test --test policy_sensitivity_from_policies   # ≥3 tests pass

# Rename didn't break existing pipeline-based tests
cargo test --test policy_sensitivity                 # 2 tests pass

# Full regression
cargo test --all-targets                             # all pass, 0 failed
cargo clippy --all-targets -- -D warnings            # 0 warnings

# .NET regression
cd /Users/wollax/Git/personal/cupel && dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj  # all pass

# Spec completeness
grep -c "TBD" spec/src/analytics/policy-sensitivity.md   # → 0
grep "policy-sensitivity" spec/src/SUMMARY.md            # → 1 match

# Requirements validation
grep "R056" .kata/REQUIREMENTS.md | grep "validated"     # → matches
```

## Observability / Diagnostics

- Runtime signals: `Result<PolicySensitivityReport, CupelError>` — callers inspect `report.diffs` (swing items) and `report.variants[i].1` (full SelectionReport per variant)
- Inspection surfaces: `cargo test --test policy_sensitivity_from_policies` — named tests identify which behavioral contract failed; `report.variants[i].1.included/excluded` for per-variant diagnosis
- Failure visibility: `CupelError::PipelineConfig(String)` for minimum-variants guard; `CupelError` variants from `dry_run_with_policy` propagate unchanged
- Redaction constraints: none (library; no content or metadata in error messages)

## Integration Closure

- Upstream surfaces consumed: `Pipeline::dry_run_with_policy` (S02), `Policy` pub(crate) Arc fields (S02), `.NET` `PolicySensitivity` diff algorithm as reference (S01)
- New wiring introduced in this slice: `policy_sensitivity` free function wires `Policy` → `dry_run_with_policy` per variant → content-keyed diff; `lib.rs` re-exports both `policy_sensitivity` and `policy_sensitivity_from_pipelines`
- What remains before the milestone is truly usable end-to-end: nothing — S03 is the final gate; R056 validated here

## Tasks

- [x] **T01: Red phase — rename existing function, add failing policy-based tests** `est:20m`
  - Why: Establish the failing-test contract before implementation. The rename of `policy_sensitivity` → `policy_sensitivity_from_pipelines` is part of the red state: existing tests in `policy_sensitivity.rs` will fail to compile, and new tests in `policy_sensitivity_from_policies.rs` will fail to compile (missing function). Both failures are expected and prove the red state.
  - Files: `crates/cupel/src/analytics.rs`, `crates/cupel/src/lib.rs`, `crates/cupel/tests/policy_sensitivity.rs`, `crates/cupel/tests/policy_sensitivity_from_policies.rs`
  - Do: (1) Rename `policy_sensitivity` to `policy_sensitivity_from_pipelines` in `analytics.rs` and update its doc comment. (2) Update `lib.rs` to re-export `policy_sensitivity_from_pipelines` instead of `policy_sensitivity`. (3) Update `crates/cupel/tests/policy_sensitivity.rs`: change `use cupel::{..., policy_sensitivity, ...}` to `policy_sensitivity_from_pipelines` and update the two call sites in the test functions. (4) Create `crates/cupel/tests/policy_sensitivity_from_policies.rs` with 3 named `#[test]` functions: `all_items_swing`, `no_items_swing`, `partial_swing` — all referencing `cupel::policy_sensitivity` and `cupel::Policy`/`PolicyBuilder`; confirm compilation fails with "cannot find function `policy_sensitivity`" (the new one is not yet added). Run `cargo test --test policy_sensitivity` to confirm existing tests now pass (rename done); confirm `cargo test --test policy_sensitivity_from_policies` fails with expected errors.
  - Verify: `cargo test --test policy_sensitivity` → 2 passed; `cargo test --test policy_sensitivity_from_policies` → compile error referencing `policy_sensitivity` not found
  - Done when: existing 2 pipeline-based tests pass under new name; new test file fails to compile with exactly the expected missing-function errors

- [x] **T02: Implement policy_sensitivity and make all tests green** `est:30m`
  - Why: Deliver the actual `policy_sensitivity` function and its minimum-variants guard. Re-exports updated. All 3 new integration tests pass.
  - Files: `crates/cupel/src/analytics.rs`, `crates/cupel/src/lib.rs`
  - Do: (1) Add `use crate::pipeline::Policy;` import to `analytics.rs`. (2) Implement `pub fn policy_sensitivity(items: &[ContextItem], budget: &ContextBudget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>`: guard `variants.len() < 2` → `return Err(CupelError::PipelineConfig("policy_sensitivity requires at least 2 variants".into()))`. For each `(label, policy)`, call a temporary `Pipeline`-like approach: since `Policy` fields are `pub(crate)` Arc, create a local `Pipeline` configured from the policy, then call `dry_run_with_policy`. Wait — actually the cleanest approach is: construct a `NullTraceCollector` run directly via `pipeline.dry_run_with_policy(items, budget, policy)` — but we don't have a `Pipeline` instance here since `policy_sensitivity` is a free function. Instead, use `Pipeline::builder().scorer(Box::new(...))` — but Arc<dyn Scorer> can't trivially become Box. The correct approach: call a helper that matches what `dry_run_with_policy` does internally — i.e., build a minimal pipeline and call `dry_run_with_policy` on it, OR expose a `Pipeline::run_policy` free function, OR — simplest — create a throwaway pipeline per variant using PipelineBuilder with the policy's components cloned from Arc. Actually the intended pattern from S02 forward intelligence: `Policy` holds `pub(crate) Arc<dyn Trait>` fields; `policy_sensitivity` (same crate) can call `pipeline.dry_run_with_policy(items, budget, policy)` by constructing a dummy pipeline. But `PipelineBuilder` requires a scorer/slicer/placer and those are Arc in Policy, not Box. Best approach: add a crate-internal `fn run_policy_components` helper on `Pipeline` that takes `&Policy` directly, or simpler — just build a real temporary pipeline from any base (e.g., using the policy's scorer/slicer/placer via `Arc::clone` + `.as_ref()` + downcasting is complex). Simplest correct approach consistent with S02 patterns: in `analytics.rs` (same crate as pipeline), build a `NullTraceCollector`, then call `pipeline.run_with_components` — but `run_with_components` is private. The correct path is to call `pipeline_instance.dry_run_with_policy(items, budget, policy)`. We need a `Pipeline` instance as the receiver. Solution: add a package-internal free function `pub(crate) fn policy_run(items, budget, policy) -> Result<SelectionReport, CupelError>` to `pipeline/mod.rs` that calls `run_with_components` directly (same module) — then `analytics.rs` calls it. This avoids needing a Pipeline instance as receiver and is clean. (3) After each variant run, build the content-keyed status map and filter to swing items — identical algorithm to existing `policy_sensitivity_from_pipelines`. (4) Update `lib.rs` to add `policy_sensitivity` to the pub use block (alongside `policy_sensitivity_from_pipelines`). (5) Write test fixture helpers in `policy_sensitivity_from_policies.rs`: 3 items with contrasting relevance/priority; `all_items_swing` uses budget fitting 1 item + 2 policies with opposite rankings (all 2 items appear in diffs); `no_items_swing` uses 2 identical policies (same Policy instance via Arc clone or identical configs — same results, empty diffs); `partial_swing` uses budget fitting 2 of 3 items + 2 policies that agree on one item but disagree on another. Run `cargo test --all-targets` to confirm all pass.
  - Verify: `cargo test --all-targets` → all pass, 0 failed; `cargo clippy --all-targets -- -D warnings` → 0 warnings
  - Done when: 3 new tests pass; 2 renamed pipeline tests pass; clippy clean; full suite green

- [x] **T03: Write spec chapter, update SUMMARY.md, CHANGELOG.md, and validate R056** `est:25m`
  - Why: Complete the milestone's documentation deliverables and mark R056 validated. The spec chapter is required by the milestone definition of done.
  - Files: `spec/src/analytics/policy-sensitivity.md`, `spec/src/SUMMARY.md`, `CHANGELOG.md`, `.kata/REQUIREMENTS.md`
  - Do: (1) Create `spec/src/analytics/policy-sensitivity.md` with these sections: **Overview** (purpose: run multiple policy configurations over the same items and compute a structured diff); **API** with subsections for both languages — .NET: `CupelPipeline.DryRunWithPolicy(items, budget, policy)` and `PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string Label, CupelPolicy Policy)[])` with signatures and semantics; Rust: `policy_sensitivity(items, budget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>` and `policy_sensitivity_from_pipelines` for the pipeline-based variant; **Types** section describing `PolicySensitivityReport` (variants + diffs), `PolicySensitivityDiffEntry` (content + statuses), `ItemStatus` (Included/Excluded); **Diff Semantics** — content-keyed matching, swing-only filter (items where not all variants agree), determinism note; **Minimum Variants** — MUST supply ≥ 2 variants; **Explicit Budget** — budget is a required parameter, never inherited from a pipeline, rationale per D148; **Language Notes** — CupelPolicy gap (.NET cannot express CountQuotaSlice, use pipeline-based overload for count-quota fork diagnostics); **Examples** section with code snippets for both languages (minimal, showing 2-variant comparison). (2) Add `- [Policy Sensitivity](analytics/policy-sensitivity.md)` to `spec/src/SUMMARY.md` after the Budget Simulation entry. (3) Update `CHANGELOG.md`: add `## [Unreleased]` section at top (before `## [1.1.0]`) with `### Added` entries for `.NET DryRunWithPolicy`, `.NET policy-based PolicySensitivity overload`, `Rust Policy + PolicyBuilder`, `Rust dry_run_with_policy`, `Rust policy_sensitivity`, `Rust policy_sensitivity_from_pipelines`, `spec/src/analytics/policy-sensitivity.md`. (4) Update `.kata/REQUIREMENTS.md`: change R056 `Status: active` to `Status: validated`; update the Validation field with proof summary; update the Coverage Summary table. (5) Verify `grep -c "TBD" spec/src/analytics/policy-sensitivity.md` → 0; verify `grep "policy-sensitivity" spec/src/SUMMARY.md` → match; run `dotnet test` to confirm no regressions.
  - Verify: `grep -c "TBD" spec/src/analytics/policy-sensitivity.md` → 0; `grep "policy-sensitivity" spec/src/SUMMARY.md` → 1 match; `dotnet test` passes; `cargo test --all-targets` still passes
  - Done when: spec file has 0 TBD fields, is linked from SUMMARY.md, CHANGELOG.md has Unreleased section, R056 shows `validated` in REQUIREMENTS.md, both test suites green

## Files Likely Touched

- `crates/cupel/src/analytics.rs`
- `crates/cupel/src/lib.rs`
- `crates/cupel/src/pipeline/mod.rs` (pub(crate) helper for policy run)
- `crates/cupel/tests/policy_sensitivity.rs`
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` (new)
- `spec/src/analytics/policy-sensitivity.md` (new)
- `spec/src/SUMMARY.md`
- `CHANGELOG.md`
- `.kata/REQUIREMENTS.md`
