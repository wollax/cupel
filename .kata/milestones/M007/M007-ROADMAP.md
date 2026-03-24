# M007: DryRunWithPolicy

**Vision:** Add `DryRunWithPolicy` as a first-class public API in both .NET and Rust — letting callers run fork diagnostics or adaptive selection by passing a policy object rather than constructing a new pipeline. In .NET this also adds a policy-accepting `PolicySensitivity` overload. In Rust this introduces the first `Policy` struct and the first `policy_sensitivity` function, achieving parity with .NET's fork-diagnostic surface.

## Success Criteria

- `pipeline.DryRunWithPolicy(items, budget, policy)` is callable in .NET and returns a `ContextResult` that reflects the policy's scorer, slicer, placer, and flags — not the pipeline's own configuration
- `CupelPipeline.PolicySensitivity(items, budget, ("a", policyA), ("b", policyB))` overload exists and produces the same diff result as passing equivalent pipelines to the existing overload
- Rust `Pipeline::dry_run_with_policy(items, budget, &policy)` returns a `SelectionReport` driven by the given `Policy`
- Rust `policy_sensitivity(items, budget, &[("a", &policy_a), ("b", &policy_b)])` returns a `PolicySensitivityReport` with correct swing-item diffs
- All existing tests continue to pass (no regressions)
- `cargo clippy --all-targets -- -D warnings` clean; `dotnet build` 0 warnings; PublicAPI surface files updated

## Key Risks / Unknowns

- **Rust trait-object ownership** — `Policy` holds `Box<dyn Scorer/Slicer/Placer>`; `policy_sensitivity` needs multiple runs. Must decide between `Arc<dyn Trait>` (shared ownership, multi-run) vs. consuming/rebuilding. This shapes the entire Rust API surface.
- **`.NET` `CupelPolicy` gap for `CountQuotaSlice`** — `SlicerType` enum has no `CountQuota` variant. `DryRunWithPolicy` via `CupelPolicy` cannot express count-quota pipelines. Must document the gap explicitly.
- **Budget semantics** — `DryRunWithPolicy` must take an explicit budget parameter (not inherit from pipeline), since the policy does not carry a budget. Spec and implementation must align.

## Proof Strategy

- **Rust trait-object ownership** → retire in S02 by implementing `Policy` with `Arc<dyn Trait>` and running `policy_sensitivity` with shared-scorer variants in tests
- **`CupelPolicy` gap** → retire in S01 by documenting the limitation in XML doc and spec; no code workaround needed
- **Budget semantics** → retire in S01 by implementing `DryRunWithPolicy` with an explicit `budget` parameter and writing a test that uses a budget different from the pipeline's own

## Verification Classes

- Contract verification: unit tests (scorer/slicer/placer respected, deduplication flag, overflow strategy) + integration tests comparing policy-based and pipeline-based `PolicySensitivity` outputs
- Integration verification: policy-based `PolicySensitivity` overload must produce identical diffs to the pipeline-based overload when given equivalent configurations — tested with shared items/budget
- Operational verification: none (library)
- UAT / human verification: none

## Milestone Definition of Done

This milestone is complete only when all are true:

- `DryRunWithPolicy` is public in .NET with full XML docs and PublicAPI surface updated
- Policy-accepting `PolicySensitivity` overload is public in .NET
- Rust `Policy` + `PolicyBuilder` are published (not hidden behind a feature flag)
- Rust `dry_run_with_policy` and `policy_sensitivity` are public
- Spec chapter at `spec/src/analytics/policy-sensitivity.md` covers both languages
- `cargo test --all-targets` passes; `dotnet test` passes
- `cargo clippy --all-targets -- -D warnings` clean; `dotnet build` 0 errors/warnings
- R056 marked validated in `.kata/REQUIREMENTS.md`

## Requirement Coverage

- Covers: R056
- Partially covers: none
- Leaves for later: R055 (ProfiledPlacer), R057 (TimestampCoverage split)
- Orphan risks: none

## Slices

- [x] **S01: .NET DryRunWithPolicy and policy-accepting PolicySensitivity** `risk:low` `depends:[]`
  > After this: `.NET` callers can call `pipeline.DryRunWithPolicy(items, budget, policy)` and `CupelPipeline.PolicySensitivity(items, budget, (label, policy)[])` — both verified by passing tests.

- [x] **S02: Rust Policy struct and dry_run_with_policy** `risk:medium` `depends:[]`
  > After this: Rust callers can build a `Policy` via `PolicyBuilder` and call `pipeline.dry_run_with_policy(items, budget, &policy)` — returning a `SelectionReport` driven by the policy's components — verified by unit tests.

- [x] **S03: Rust policy_sensitivity and spec chapter** `risk:low` `depends:[S01,S02]`
  > After this: Rust `policy_sensitivity(items, budget, &[(label, &policy)])` returns a `PolicySensitivityReport`; spec chapter at `spec/src/analytics/policy-sensitivity.md` documents both languages; R056 validated.

## Boundary Map

### S01 → S03

Produces:
- `.NET` `DryRunWithPolicy(items, budget, policy)` method on `CupelPipeline` (public)
- `.NET` `PolicySensitivity(items, budget, (label, policy)[])` overload in `PolicySensitivityExtensions`
- Spec API contract for `.NET` side (to be written in S03's spec chapter)

Consumes:
- nothing (first slice; builds on existing `DryRunWithBudget` internal method and `CupelPolicy` enum mapping)

### S02 → S03

Produces:
- Rust `Policy` struct: `Arc<dyn Scorer>`, `Arc<dyn Slicer>`, `Arc<dyn Placer>`, `deduplication: bool`, `overflow_strategy: OverflowStrategy`
- Rust `PolicyBuilder` fluent builder
- Rust `Pipeline::dry_run_with_policy(&self, items, budget, policy)` method
- Spec API contract for Rust side (to be written in S03's spec chapter)

Consumes:
- nothing (independent of S01; builds on existing `run_traced` internal machinery)

### S01 + S02 → S03

Produces:
- Rust `policy_sensitivity(items, budget, &[(label, &Policy)])` free function returning `PolicySensitivityReport`
- Rust `PolicySensitivityReport` and `PolicySensitivityDiffEntry` types
- `spec/src/analytics/policy-sensitivity.md` spec chapter (both languages)
- R056 marked validated; changelog updated

Consumes from S01:
- `.NET` `DryRunWithPolicy` and `PolicySensitivity` (policy-based overload) — API shapes locked

Consumes from S02:
- Rust `Policy`, `PolicyBuilder`, `dry_run_with_policy` — API shapes locked
