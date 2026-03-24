---
id: S03
parent: M007
milestone: M007
provides:
  - "`policy_sensitivity_from_pipelines` in `analytics.rs` (renamed from `policy_sensitivity`); existing 2 pipeline-based tests updated and passing"
  - "`policy_sensitivity(items, budget, variants: &[(impl AsRef<str>, &Policy)]) -> Result<PolicySensitivityReport, CupelError>` free function in `analytics.rs`; minimum-variants guard → `CupelError::PipelineConfig`"
  - "`pub(crate) fn run_policy(items, budget, policy)` bridge in `pipeline/mod.rs` using dummy-pipeline approach"
  - "3 integration tests in `tests/policy_sensitivity_from_policies.rs`: all_items_swing, no_items_swing, partial_swing — all passing"
  - "`spec/src/analytics/policy-sensitivity.md` — TBD-free normative spec chapter (both languages, all types, diff semantics, minimum-variants rule, budget rationale, CupelPolicy gap note, examples)"
  - "`spec/src/SUMMARY.md` updated with Policy Sensitivity link under Analytics"
  - "`CHANGELOG.md` `## [Unreleased]` section with 7 Added entries covering all M007 deliverables"
  - "R056 marked validated in `.kata/REQUIREMENTS.md`; Coverage Summary: Active 0, Validated 32"
requires:
  - slice: S01
    provides: ".NET DryRunWithPolicy and policy-based PolicySensitivity overload — API shapes locked and consumed as reference for spec chapter"
  - slice: S02
    provides: "Rust Policy + PolicyBuilder + dry_run_with_policy — consumed by run_policy bridge and policy_sensitivity function"
affects: []
key_files:
  - crates/cupel/src/analytics.rs
  - crates/cupel/src/lib.rs
  - crates/cupel/src/pipeline/mod.rs
  - crates/cupel/tests/policy_sensitivity.rs
  - crates/cupel/tests/policy_sensitivity_from_policies.rs
  - spec/src/analytics/policy-sensitivity.md
  - spec/src/SUMMARY.md
  - CHANGELOG.md
  - .kata/REQUIREMENTS.md
key_decisions:
  - "Dummy-pipeline approach for run_policy: constructs a throwaway Pipeline with ReflexiveScorer/GreedySlice/ChronologicalPlacer, then calls dry_run_with_policy — avoids Arc→Box complexity; policy fully overrides components so dummy values are never used (D148-adjacent)"
  - "`run_policy` placed as a pub(crate) free function in pipeline/mod.rs (not an impl Pipeline method) so analytics.rs can call it without a Pipeline receiver"
  - "Spec chapter structure mirrors budget-simulation.md sectioning convention: Overview → API (.NET then Rust) → Types → Diff Semantics → Minimum Variants → Explicit Budget → Language Notes → Examples"
  - "Rust code examples in spec chapter derived directly from integration test fixtures to guarantee runnable accuracy"
patterns_established:
  - "`pub(crate) fn run_policy` in pipeline/mod.rs is the canonical bridge from analytics → policy execution; use for any future analytics function needing per-policy dry-run without a Pipeline receiver"
  - "Red-phase stubs use `todo!()` bodies with real import paths to produce compile-error failures (not runtime panics) — establishes clear red state"
  - "Test fixture pattern for policy_sensitivity: PriorityScorer vs ReflexiveScorer over items with orthogonal priority/relevance values creates guaranteed divergence with tight budget"
observability_surfaces:
  - "`cargo test --test policy_sensitivity_from_policies` — 3 named tests; test name identifies which behavioral contract (all-swing, no-swing, partial-swing) broke"
  - "`cargo test --test policy_sensitivity` — 2 named tests; regression check for pipeline-based variant rename"
  - "`report.diffs` — primary inspection surface; non-empty when items swing, empty when identical coverage"
  - "`report.variants[i].1.included/excluded` — per-variant detail for diagnosing disagreements"
  - "`CupelError::PipelineConfig(String)` for minimum-variants guard; propagates unchanged from dry_run_with_policy"
drill_down_paths:
  - .kata/milestones/M007/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M007/slices/S03/tasks/T02-SUMMARY.md
  - .kata/milestones/M007/slices/S03/tasks/T03-SUMMARY.md
duration: 45min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
---

# S03: Rust policy_sensitivity and spec chapter

**Added `policy_sensitivity` free function accepting `&[(label, &Policy)]` with `run_policy` bridge helper, 3 green integration tests, and a TBD-free normative spec chapter covering both languages — completing M007 and validating R056.**

## What Happened

**T01 — Red phase (rename + failing test stubs):**
Renamed `policy_sensitivity` → `policy_sensitivity_from_pipelines` across `analytics.rs`, `lib.rs`, and the existing test file `tests/policy_sensitivity.rs`. The two existing tests were updated to use the new name and confirmed passing. Created `tests/policy_sensitivity_from_policies.rs` with 3 named test stubs (`all_items_swing`, `no_items_swing`, `partial_swing`) that import `cupel::policy_sensitivity` (not yet defined) and use real types with `todo!()` bodies — producing a compile error that defined T02's exact implementation target.

**T02 — Green phase (implementation):**
Added `pub(crate) fn run_policy` to `pipeline/mod.rs` — a free function that constructs a throwaway `Pipeline` with `ReflexiveScorer`/`GreedySlice`/`ChronologicalPlacer` and immediately calls `dry_run_with_policy` with the caller's `Policy`. Since the policy overrides all components, the dummy pipeline values are never used in scoring/slicing/placing. This avoids Arc→Box conversion complexity and keeps `analytics.rs` decoupled from pipeline internals.

Implemented `policy_sensitivity` in `analytics.rs` with the minimum-variants guard (`< 2 → CupelError::PipelineConfig`) and a diff algorithm identical to `policy_sensitivity_from_pipelines` (content-keyed HashMap, swing-only filter). Updated `lib.rs` to export both functions.

Replaced the three `todo!()` test stubs with real bodies:
- `all_items_swing`: 2 items, tight budget (1 item). PriorityScorer picks item-a; ReflexiveScorer picks item-b. Both appear in diffs.
- `no_items_swing`: 3 items, ample budget, two identical PriorityScorer policies. All included by both; diffs empty.
- `partial_swing`: 3 items × 30 tokens, budget fits 2. "stable" included by both; "swing-relevance" included by ReflexiveScorer only; "swing-priority" included by PriorityScorer only. Diffs contain exactly 2 items.

One clippy fix: doc comment continuation line needed 4-space indent per `clippy::doc-lazy-continuation`.

**T03 — Documentation and requirements:**
Created `spec/src/analytics/policy-sensitivity.md` TBD-free covering both language APIs, all 4 public types, diff semantics, minimum-variants rule (with exact error messages per language), explicit budget rationale, CupelPolicy/CountQuotaSlice gap note, and 2-variant code examples per language. Added Policy Sensitivity link to `spec/src/SUMMARY.md` after Budget Simulation. Added `## [Unreleased]` section to `CHANGELOG.md` with 7 Added entries. Updated R056 in `.kata/REQUIREMENTS.md` from `active` to `validated` with full proof; Coverage Summary updated to Active: 0, Validated: 32.

## Verification

```
cargo test --test policy_sensitivity_from_policies   → 3 passed (all_items_swing, no_items_swing, partial_swing)
cargo test --test policy_sensitivity                 → 2 passed (pipeline-based rename regression)
cargo test --all-targets                             → 167 passed, 0 failed
cargo clippy --all-targets -- -D warnings            → 0 warnings, 0 errors
dotnet test Wollax.Cupel.Tests                       → 679 passed, 0 failed
grep -c "TBD" spec/src/analytics/policy-sensitivity.md  → 0
grep "policy-sensitivity" spec/src/SUMMARY.md            → 1 match
grep "R056" .kata/REQUIREMENTS.md | grep "validated"     → match
```

## Requirements Advanced

- R056 — All three owning slices (S01, S02, S03) complete; full API surface delivered in both languages; spec chapter written

## Requirements Validated

- R056 — `DryRunWithPolicy` + policy-accepting `PolicySensitivity` (.NET, S01), `Policy` + `PolicyBuilder` + `dry_run_with_policy` (Rust, S02), `policy_sensitivity` + `policy_sensitivity_from_pipelines` (Rust, S03), and spec chapter — all verified with passing tests in both languages and a TBD-free spec chapter

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

None. The dummy-pipeline approach for `run_policy` was anticipated in the T02 plan description; implementation matched the plan exactly.

## Known Limitations

- `CupelPolicy` (.NET) cannot express `CountQuotaSlice` pipelines — documented in spec chapter and in the gap note. Callers needing count-quota fork diagnostics must use the pipeline-based `policy_sensitivity_from_pipelines` overload.
- `diffs` order within `PolicySensitivityReport` is unspecified (insertion order from HashMap iteration in Rust). Documented in spec as "not guaranteed."

## Follow-ups

- none (M007 is complete; R056 validated)

## Files Created/Modified

- `crates/cupel/src/analytics.rs` — `policy_sensitivity_from_pipelines` renamed, `policy_sensitivity` added, imports updated
- `crates/cupel/src/lib.rs` — both functions exported
- `crates/cupel/src/pipeline/mod.rs` — `pub(crate) fn run_policy` bridge added
- `crates/cupel/tests/policy_sensitivity.rs` — import and call sites updated to new name
- `crates/cupel/tests/policy_sensitivity_from_policies.rs` — 3 passing integration tests
- `spec/src/analytics/policy-sensitivity.md` — new TBD-free normative spec chapter
- `spec/src/SUMMARY.md` — Policy Sensitivity link added under Analytics
- `CHANGELOG.md` — Unreleased section with 7 entries added
- `.kata/REQUIREMENTS.md` — R056 validated, Coverage Summary updated

## Forward Intelligence

### What the next slice should know
- M007 is complete; all slices done; no active milestone remains. Next work should start a new milestone.
- `pub(crate) fn run_policy` in `pipeline/mod.rs` is available as a bridge for any future analytics function needing per-policy execution without a `Pipeline` receiver — reuse it rather than duplicating the dummy-pipeline pattern.
- `policy_sensitivity` and `policy_sensitivity_from_pipelines` are both public; the naming convention distinguishes policy-based (no pipeline required) from pipeline-based (Pipeline instances required).

### What's fragile
- `diffs` order is HashMap-insertion-order in Rust (unspecified) — test assertions that check diff order (rather than membership) will be brittle. Current tests assert `len()` and content membership, which is correct.
- The dummy-pipeline in `run_policy` constructs `GreedySlice::new()` — if `GreedySlice::new()` ever starts requiring arguments, `run_policy` will break. Low risk given that `GreedySlice` is a ZST-like type.

### Authoritative diagnostics
- `cargo test --test policy_sensitivity_from_policies` — named test failures identify which behavioral contract broke (all-swing / no-swing / partial-swing)
- `report.diffs.len()` — primary assertion surface; 0 = no swing, N = N swing items
- `CupelError::PipelineConfig(msg)` — minimum-variants guard; message contains "at least 2 variants"

### What assumptions changed
- Original T02 plan considered Arc→Box complexity and `pub(crate) fn policy_run` as a method — actual implementation used a free function named `run_policy` (slightly different from plan's suggested `policy_run`), which is cleaner and equally correct.
