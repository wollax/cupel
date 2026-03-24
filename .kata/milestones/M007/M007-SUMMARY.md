---
id: M007
provides:
  - ".NET `CupelPipeline.DryRunWithPolicy(items, budget, policy)` public method with full XML docs and PublicAPI surface updated"
  - ".NET `PolicySensitivityExtensions.PolicySensitivity(items, budget, params (string Label, CupelPolicy Policy)[])` overload"
  - "Rust `Policy` struct with `pub(crate) Arc<dyn Scorer/Slicer/Placer>`, `deduplication: bool`, `overflow_strategy: OverflowStrategy`"
  - "Rust `PolicyBuilder` fluent builder (mirrors `PipelineBuilder` with Arc instead of Box)"
  - "Rust `Pipeline::dry_run_with_policy(items, budget, &policy)` returning `Result<SelectionReport, CupelError>`"
  - "Rust `run_with_components` private helper extracted from `run_traced`; both entry points delegate to it"
  - "Rust `policy_sensitivity(items, budget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>` free function"
  - "Rust `policy_sensitivity_from_pipelines` (renamed from `policy_sensitivity`) for pipeline-based fork diagnostics"
  - "`pub(crate) fn run_policy` bridge in `pipeline/mod.rs` enabling analytics functions to execute policies without a Pipeline receiver"
  - "`spec/src/analytics/policy-sensitivity.md` — TBD-free normative spec chapter covering both languages, all 4 types, diff semantics, minimum-variants rule, explicit budget rationale, CupelPolicy gap note, and examples"
  - "R056 validated; CHANGELOG.md Unreleased section with 7 entries; R056 Active→Validated in REQUIREMENTS.md"
key_decisions:
  - "D148: DryRunWithPolicy accepts explicit budget (not inherited from pipeline) — policy carries no budget; explicit parameter makes the call self-describing"
  - "D149: Rust Policy uses Arc<dyn Trait> — Box<dyn Trait> is !Clone; Arc allows shared ownership across policy_sensitivity multi-run calls"
  - "D150: policy_sensitivity is a free function, not a Pipeline method — fork diagnostics operate over policies, not pipelines"
  - "D151: CupelPolicy gap for CountQuotaSlice documented in XML docs and spec; no code workaround"
  - "D155: run_with_components extracted from run_traced as a private helper accepting &dyn Trait params; both run_traced and dry_run_with_policy delegate to it"
  - "D159: policy_sensitivity gets primary name; pipeline-based variant renamed to policy_sensitivity_from_pipelines"
  - "D160: policy_sensitivity uses dummy-pipeline approach to call dry_run_with_policy — dummy's components overridden by policy; avoids Arc→Box complexity"
patterns_established:
  - "run_with_components pattern: extract pipeline hot inner loop into private helper accepting &dyn Trait params; public entry points delegate with either self.* fields or injected policy fields"
  - "PolicyBuilder mirrors PipelineBuilder with Arc<dyn Trait> substituted for Box<dyn Trait>; identical error strings ensure uniform caller experience"
  - "pub(crate) fn run_policy in pipeline/mod.rs is the canonical bridge for analytics functions needing per-policy execution without a Pipeline receiver"
  - "DryRunWithPolicy delegates to CreateBuilder().WithBudget(budget).WithPolicy(policy).Build().DryRunWithBudget() — policy→concrete mapping stays in PipelineBuilder.WithPolicy()"
  - "KnapsackSlice::new(1) (not with_default_bucket_size()) is correct for tight-budget integration tests; bucket_size=100 produces capacity=0 when budget target < 100"
observability_surfaces:
  - "cargo test --test dry_run_with_policy — 5 behavioral contracts for Rust dry_run_with_policy (scorer_is_respected, slicer_is_respected, deduplication_false/true, overflow_strategy)"
  - "cargo test --test policy_sensitivity_from_policies — 3 behavioral contracts (all-swing, no-swing, partial-swing)"
  - "cargo test --test policy_sensitivity — 2 regression tests for pipeline-based variant rename"
  - "result.Report!.Included / result.Report!.Excluded — .NET DryRunWithPolicy output inspection"
  - "PolicySensitivityReport.Diffs — items that swung across policy variants; .report.variants[i].1.included/excluded for per-variant detail"
  - "CupelError::PipelineConfig(String) — minimum-variants guard for policy_sensitivity"
requirement_outcomes:
  - id: R056
    from_status: active
    to_status: validated
    proof: ".NET: CupelPipeline.DryRunWithPolicy (6 tests) and policy-based PolicySensitivity overload (3 tests) in Wollax.Cupel.Tests; dotnet test 679 passed, 0 failed. Rust: Policy + PolicyBuilder + dry_run_with_policy (5 integration tests in dry_run_with_policy.rs); policy_sensitivity + policy_sensitivity_from_pipelines (3+2 integration tests); cargo test --all-targets 167 passed, 0 failed; cargo clippy clean. Spec: spec/src/analytics/policy-sensitivity.md exists, 0 TBD fields, linked from SUMMARY.md. CHANGELOG.md Unreleased section present."
duration: ~3h (S01: ~20m, S02: ~65m, S03: ~45m + coordination)
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# M007: DryRunWithPolicy

**Added `DryRunWithPolicy` and policy-based `PolicySensitivity` in .NET, plus `Policy`, `PolicyBuilder`, `dry_run_with_policy`, and `policy_sensitivity` in Rust — validating R056 and achieving full fork-diagnostic API parity across both languages.**

## What Happened

M007 delivered two complementary APIs across three slices:

**S01 (.NET):** Added `CupelPipeline.DryRunWithPolicy(items, budget, policy)` — a thin wrapper over `CreateBuilder().WithBudget(budget).WithPolicy(policy).Build().DryRunWithBudget(items, budget)` that keeps the policy→concrete mapping in `PipelineBuilder.WithPolicy()`. Added a policy-accepting overload of `PolicySensitivityExtensions.PolicySensitivity` that builds a temp pipeline per variant (identical pattern to the existing pipeline-based overload). `PublicAPI.Unshipped.txt` updated with both signatures. 6 DryRunWithPolicy tests + 3 PolicySensitivity policy-overload tests, all green. One test design adjustment: `ScorerType.Priority` vs `ScorerType.Reflexive` is used instead of a (non-existent) inverted scorer to exercise "policy scorer overrides pipeline scorer."

**S02 (Rust):** Extracted the ~280-line stage body of `run_traced` into a private `run_with_components<C: TraceCollector>` helper accepting injected `&dyn Scorer/Slicer/Placer` + flags. Both `run_traced` and new `dry_run_with_policy` delegate to it — zero behavioral change to `run_traced` confirmed by mid-refactor regression check. Added `Policy` struct with `pub(crate) Arc<dyn Trait>` fields (not `Box` — Arc allows shared ownership across multi-variant `policy_sensitivity` runs). `PolicyBuilder` mirrors `PipelineBuilder` with Arc substituted for Box. `Pipeline::dry_run_with_policy` wires a `DiagnosticTraceCollector` to `run_with_components` with policy fields and returns `collector.into_report()`. 5 integration tests covering all policy field behavioral contracts.

**S03 (Rust + Spec):** Renamed existing `policy_sensitivity` → `policy_sensitivity_from_pipelines` (2 tests updated). Added `pub(crate) fn run_policy` to `pipeline/mod.rs` — a bridge that constructs a throwaway `Pipeline` (ReflexiveScorer/GreedySlice/ChronologicalPlacer) and calls `dry_run_with_policy`; since the policy overrides all components, the dummy values never affect results. Implemented `policy_sensitivity` in `analytics.rs` with a minimum-variants guard and the same content-keyed diff algorithm as the pipeline-based variant. 3 integration tests (all-swing, no-swing, partial-swing). Created `spec/src/analytics/policy-sensitivity.md` TBD-free; updated SUMMARY.md, CHANGELOG.md, REQUIREMENTS.md.

## Cross-Slice Verification

**Success Criterion 1: `.NET DryRunWithPolicy` callable and returns ContextResult reflecting policy**
- ✅ `DryRunWithPolicyTests.cs` — 6 tests: `UsesPolicy_Scorer_NotPipelines`, `UsesPolicy_Slicer`, `UsesPolicy_Placer`, `UsesPolicy_DeduplicationFlag`, `UsesPolicy_OverflowStrategy`, `UsesExplicitBudget_NotPipelineBudget`
- Evidence: `dotnet test Wollax.Cupel.Tests → 679 passed, 0 failed`

**Success Criterion 2: `.NET PolicySensitivity` policy overload exists and produces same diffs**
- ✅ `PolicySensitivityTests.cs` — 3 new tests: policy overload produces diffs, identical to pipeline-based overload with equivalent configs, minimum-variants guard
- Evidence: same test run, 679 passed

**Success Criterion 3: Rust `dry_run_with_policy` returns `SelectionReport` driven by policy**
- ✅ `tests/dry_run_with_policy.rs` — 5 tests: `scorer_is_respected`, `slicer_is_respected`, `deduplication_false_allows_duplicates`, `deduplication_true_excludes_duplicates`, `overflow_strategy_is_respected`
- Evidence: `cargo test --test dry_run_with_policy → 5 passed`

**Success Criterion 4: Rust `policy_sensitivity` returns `PolicySensitivityReport` with correct diffs**
- ✅ `tests/policy_sensitivity_from_policies.rs` — 3 tests: `all_items_swing`, `no_items_swing`, `partial_swing`
- Evidence: `cargo test --test policy_sensitivity_from_policies → 3 passed`

**Success Criterion 5: All existing tests continue to pass (no regressions)**
- ✅ `cargo test --all-targets → 167 passed, 0 failed` (11 suites)
- ✅ `dotnet test Wollax.Cupel.Tests → 679 passed, 0 failed`

**Success Criterion 6: `cargo clippy --all-targets -- -D warnings` clean; `dotnet build` 0 warnings; PublicAPI updated**
- ✅ `cargo clippy --all-targets -- -D warnings → 0 warnings, exit 0`
- ✅ `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj --no-incremental → Build succeeded, 0 errors/warnings`
- ✅ `PublicAPI.Unshipped.txt` contains both new .NET method signatures

**Definition of Done — all items checked:**
- ✅ `DryRunWithPolicy` public in .NET with full XML docs and PublicAPI surface updated
- ✅ Policy-accepting `PolicySensitivity` overload public in .NET
- ✅ Rust `Policy` + `PolicyBuilder` published (not feature-flagged); re-exported from `lib.rs`
- ✅ Rust `dry_run_with_policy` and `policy_sensitivity` public
- ✅ Spec chapter at `spec/src/analytics/policy-sensitivity.md` covers both languages (0 TBD fields, linked from SUMMARY.md)
- ✅ `cargo test --all-targets` passes; `dotnet test` passes
- ✅ `cargo clippy --all-targets -- -D warnings` clean; `dotnet build` 0 errors/warnings
- ✅ R056 marked validated in `.kata/REQUIREMENTS.md`
- ✅ All 3 slices `[x]` in ROADMAP.md; all 3 slice summaries exist

## Requirement Changes

- R056: active → validated — `.NET DryRunWithPolicy` (6 tests) + `.NET PolicySensitivity policy overload` (3 tests) + Rust `Policy/PolicyBuilder/dry_run_with_policy` (5 tests) + Rust `policy_sensitivity` (3 tests) + spec chapter TBD-free; both test suites green (167 Rust, 679 .NET)

## Forward Intelligence

### What the next milestone should know
- `pub(crate) fn run_policy` in `pipeline/mod.rs` is available for future analytics functions needing per-policy execution without a Pipeline receiver — reuse before duplicating the dummy-pipeline pattern.
- `run_with_components` is the correct hook for any future API needing to inject custom scorer/slicer/placer at call time; it is private but accessible within `pipeline/mod.rs`.
- `CupelPolicy` (.NET) has no `CountQuota` variant — callers needing count-quota fork diagnostics must use `policy_sensitivity_from_pipelines` (pipeline-based) or construct pipelines manually. The gap is documented in XML docs and spec.
- `policy_sensitivity` and `policy_sensitivity_from_pipelines` are both public in the Rust crate; the naming convention distinguishes policy-based (no pipeline required) from pipeline-based (Pipeline instances required).
- `.NET DryRunWithPolicy` delegates through `CreateBuilder().WithBudget(budget).WithPolicy(policy).Build()` — the policy→concrete mapping lives entirely in `PipelineBuilder.WithPolicy()`. Any new `CupelPolicy` fields (e.g., a future CountQuota slicer type) only need to be added there.

### What's fragile
- `diffs` order in `PolicySensitivityReport` (Rust) is HashMap-insertion-order — unspecified. Tests assert membership/length, not order. Callers who depend on diff order will encounter non-determinism.
- `run_policy` dummy pipeline constructs `GreedySlice::new()` — if `GreedySlice::new()` ever requires arguments, `run_policy` breaks. Low risk; `GreedySlice` is effectively a ZST.
- `UsesPolicy_Scorer_NotPipelines` (.NET test) relies on Priority vs Reflexive divergence when item Priority orderings are inverted relative to FutureRelevanceHint. If test fixture changes, divergence assumption breaks silently.
- `KnapsackSlice::new(bucket_size)` with `bucket_size ≥ budget target` → `capacity=0` → empty selection. Always use `new(1)` for tight-budget tests; `with_default_bucket_size()` (bucket_size=100) is only safe when budget target >> 100.

### Authoritative diagnostics
- `cargo test --test dry_run_with_policy` — 5 named tests identify which policy field contract broke
- `cargo test --test policy_sensitivity_from_policies` — 3 named tests (all-swing / no-swing / partial-swing) identify which diff behavioral contract broke
- `result.Report!.Included` / `result.Report!.Excluded` (.NET) — full SelectionReport from DryRunWithPolicy output
- `report.diffs.len()` (Rust) — primary assertion surface; 0 = no swing, N = N swing items

### What assumptions changed
- Original M007 plan considered `Box<dyn Trait>` for `Policy` fields — discarded because `Box` is `!Clone` and `policy_sensitivity` needs to run multiple variants from shared policies. `Arc<dyn Trait>` is the correct choice.
- `slicer_is_respected` test originally used `KnapsackSlice::with_default_bucket_size()` — fails silently when budget target < bucket_size (capacity=0). `KnapsackSlice::new(1)` is the correct form.
- `.NET` scorer test for `DryRunWithPolicy` originally planned a custom `InvertedRelevanceScorer` — impossible because `CupelPolicy` accepts only `ScorerType` enum values. `ScorerType.Priority` vs `ScorerType.Reflexive` with inverted item orderings is the correct approach.

## Files Created/Modified

- `src/Wollax.Cupel/CupelPipeline.cs` — Added `DryRunWithPolicy` public method with XML docs
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — Added policy-based `PolicySensitivity` overload
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Two new method signatures appended
- `tests/Wollax.Cupel.Tests/Diagnostics/DryRunWithPolicyTests.cs` — New file, 6 tests
- `tests/Wollax.Cupel.Tests/Diagnostics/PolicySensitivityTests.cs` — 3 new tests added
- `crates/cupel/src/pipeline/mod.rs` — `run_with_components` private helper; `Policy`, `PolicyBuilder`, `Pipeline::dry_run_with_policy`; `pub(crate) fn run_policy` bridge; `use std::sync::Arc`
- `crates/cupel/src/analytics.rs` — `policy_sensitivity_from_pipelines` renamed, `policy_sensitivity` added, imports updated
- `crates/cupel/src/lib.rs` — `Policy`, `PolicyBuilder`, `policy_sensitivity`, `policy_sensitivity_from_pipelines` exported
- `crates/cupel/tests/dry_run_with_policy.rs` — New file, 5 integration tests
- `crates/cupel/tests/policy_sensitivity.rs` — Import and 2 call sites updated to new name
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — New file, 3 integration tests
- `spec/src/analytics/policy-sensitivity.md` — New TBD-free normative spec chapter
- `spec/src/SUMMARY.md` — Policy Sensitivity link added under Analytics
- `CHANGELOG.md` — Unreleased section with 7 Added entries
- `.kata/REQUIREMENTS.md` — R056 validated with full proof; Coverage Summary updated
