---
estimated_steps: 5
estimated_files: 4
---

# T03: Write spec chapter, update SUMMARY.md, CHANGELOG.md, and validate R056

**Slice:** S03 — Rust policy_sensitivity and spec chapter
**Milestone:** M007

## Description

Produce the `spec/src/analytics/policy-sensitivity.md` spec chapter covering both languages, link it from `SUMMARY.md`, add an Unreleased changelog section, and mark R056 validated in REQUIREMENTS.md. This is a documentation task — no code changes. The spec chapter must be TBD-free and complete enough to serve as the normative API contract for both .NET and Rust implementations.

## Steps

1. Create `spec/src/analytics/policy-sensitivity.md` with the following structure:
   - **Overview**: purpose (run multiple policy configurations over the same item set; compute a structured diff showing which items changed inclusion status)
   - **API** subsections:
     - `.NET`: `DryRunWithPolicy(IReadOnlyList<ContextItem> items, ContextBudget budget, CupelPolicy policy) → ContextResult` on `CupelPipeline`; `PolicySensitivity(IReadOnlyList<ContextItem> items, ContextBudget budget, params (string Label, CupelPolicy Policy)[] variants) → PolicySensitivityReport` on `PolicySensitivityExtensions`
     - `Rust`: `pub fn policy_sensitivity(items: &[ContextItem], budget: &ContextBudget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>` in `cupel::analytics`; `pub fn policy_sensitivity_from_pipelines(items: &[ContextItem], budget: &ContextBudget, variants: &[(impl AsRef<str>, &Pipeline)]) -> Result<PolicySensitivityReport, CupelError>` for the pipeline-based variant
   - **Types**: `PolicySensitivityReport { variants: Vec<(String, SelectionReport)>, diffs: Vec<PolicySensitivityDiffEntry> }`; `PolicySensitivityDiffEntry { content: String, statuses: Vec<(String, ItemStatus)> }`; `ItemStatus` enum `{ Included, Excluded }`
   - **Diff Semantics**: content-keyed matching (items matched by content string); swing-only filter (only items where not all variants agree on inclusion appear in `diffs`); `variants` preserves input order; `diffs` order is unspecified (implementation-defined)
   - **Minimum Variants**: implementations MUST require at least 2 variants; fewer variants MUST return an error (`CupelError::PipelineConfig` in Rust, `ArgumentException` in .NET)
   - **Explicit Budget Parameter**: `DryRunWithPolicy` and `policy_sensitivity` both take an explicit `budget` parameter; this is a required parameter — there is no "inherit from pipeline" budget option. Rationale: the policy does not carry a budget; an implicit budget would silently apply the pipeline's configuration to a different policy, producing surprising results.
   - **Language Notes**: CupelPolicy cannot express `CountQuotaSlice` (no `CountQuota` variant in `SlicerType`); callers needing count-quota fork diagnostics MUST use the pipeline-based overload (`PolicySensitivity(items, budget, (label, pipeline)[])` in .NET, `policy_sensitivity_from_pipelines` in Rust).
   - **Examples**: minimal 2-variant comparison code snippet for each language (adapt from the integration tests written in T01/T02)
   - Verify `grep -c "TBD" spec/src/analytics/policy-sensitivity.md` → 0 before proceeding.

2. Update `spec/src/SUMMARY.md`: add `- [Policy Sensitivity](analytics/policy-sensitivity.md)` as a list item after the `- [Budget Simulation](analytics/budget-simulation.md)` entry (same indentation level, under the Analytics section).

3. Update `CHANGELOG.md`: insert `## [Unreleased]` section at the top (before `## [1.1.0] - 2026-03-15`) with a `### Added` list:
   - `.NET: \`CupelPipeline.DryRunWithPolicy(items, budget, policy)\` — run a policy configuration over an item set without constructing a new pipeline`
   - `.NET: Policy-accepting \`PolicySensitivity\` overload in \`PolicySensitivityExtensions\` — pass \`(label, CupelPolicy)\` tuples instead of pre-built pipelines`
   - `Rust: \`Policy\` struct and \`PolicyBuilder\` — construct a policy from \`Arc<dyn Scorer/Slicer/Placer>\` with deduplication and overflow flags`
   - `Rust: \`Pipeline::dry_run_with_policy\` — run a pipeline using a caller-supplied Policy instead of the pipeline's own components`
   - `Rust: \`policy_sensitivity\` — fork-diagnostic free function accepting \`&[(label, &Policy)]\` variants, returning \`PolicySensitivityReport\``
   - `Rust: \`policy_sensitivity_from_pipelines\` — pipeline-based variant (renamed from \`policy_sensitivity\` for disambiguation)`
   - `Spec: \`spec/src/analytics/policy-sensitivity.md\` — normative API contract for both languages`

4. Update `.kata/REQUIREMENTS.md`: change R056 from `Status: active` to `Status: validated`; update the Validation field to: `validated — .NET: CupelPipeline.DryRunWithPolicy (6 tests) and policy-based PolicySensitivity overload (3 tests) in Wollax.Cupel.Tests; dotnet test 679 passed. Rust: Policy + PolicyBuilder + dry_run_with_policy (5 integration tests in dry_run_with_policy.rs); policy_sensitivity (3 integration tests in policy_sensitivity_from_policies.rs, minimum-variants guard); cargo test --all-targets passed; cargo clippy clean. Spec: spec/src/analytics/policy-sensitivity.md exists, TBD-free, linked from SUMMARY.md`; update the traceability table row for R056 to show `validated`; update the Coverage Summary: change `Active requirements: 1` to `Active requirements: 0` and adjust the counts accordingly.

5. Run final verification: `grep -c "TBD" spec/src/analytics/policy-sensitivity.md` → 0; `grep "policy-sensitivity" spec/src/SUMMARY.md` → match; `cargo test --all-targets` → all pass; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → all pass.

## Must-Haves

- [ ] `spec/src/analytics/policy-sensitivity.md` exists with 0 TBD fields
- [ ] Spec covers both language APIs, all 4 types, diff semantics, minimum-variants rule, explicit budget rationale, CupelPolicy gap note
- [ ] `spec/src/SUMMARY.md` contains `policy-sensitivity` link under Analytics
- [ ] `CHANGELOG.md` has `## [Unreleased]` section with all 7 entries
- [ ] `.kata/REQUIREMENTS.md` shows R056 as `validated` with full validation proof
- [ ] `cargo test --all-targets` passes
- [ ] `dotnet test` passes

## Verification

```bash
grep -c "TBD" spec/src/analytics/policy-sensitivity.md
# Expected: 0

grep "policy-sensitivity" spec/src/SUMMARY.md
# Expected: 1 match

grep "Unreleased" CHANGELOG.md
# Expected: match

grep "R056" .kata/REQUIREMENTS.md | grep "validated"
# Expected: match

cargo test --all-targets
# Expected: all passed, 0 failed

dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
# Expected: all passed, 0 failed
```

## Observability Impact

- Signals added/changed: None — documentation task only; no runtime behavior added
- How a future agent inspects this: `grep -c "TBD"` for completeness; SUMMARY.md link check; Requirements.md grep for validated status
- Failure state exposed: grep commands above are the diagnostic surface

## Inputs

- `spec/src/analytics/budget-simulation.md` — structural template for section layout and formatting conventions
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — test bodies as the basis for spec examples
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — .NET API signatures for the spec
- `.kata/REQUIREMENTS.md` — current R056 entry to update

## Expected Output

- `spec/src/analytics/policy-sensitivity.md` — TBD-free normative spec chapter
- `spec/src/SUMMARY.md` — policy-sensitivity link added
- `CHANGELOG.md` — Unreleased section with 7 entries
- `.kata/REQUIREMENTS.md` — R056 validated with full proof summary
