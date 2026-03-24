# M007: DryRunWithPolicy — Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

## Project Description

Cupel is a dual-language (.NET + Rust) context management library for coding agents. It selects and ranks context items (messages, documents, tool outputs) within a token budget using a fixed pipeline: Classify → Score → Deduplicate → Sort → Slice → Place.

## Why This Milestone

R056 — `DryRunWithPolicy` — has been deferred since M003. The blocker was demand: we wanted to see whether callers independently reached for it. The evidence is now clear: `PolicySensitivity` in .NET requires callers to construct one full `CupelPipeline` per variant, which is noisy boilerplate for what is fundamentally a "run this same input through a different configuration" use case.

M007 closes this gap with two complementary APIs:
1. `DryRunWithPolicy` — a primitive that runs an existing pipeline against a different policy, without constructing a new pipeline.
2. A policy-accepting `PolicySensitivity` overload built on top of it — so callers running fork diagnostics can pass `(label, policy)` tuples instead of pre-built pipelines.

For Rust: neither `PolicySensitivity` nor a `Policy` struct currently exists. M007 adds both, achieving parity with .NET at the API level.

## User-Visible Outcome

### When this milestone is complete, the user can:

- (.NET) Call `pipeline.DryRunWithPolicy(items, policy)` to get a `ContextResult` using a different scorer/slicer/placer/budget than the pipeline was built with — no new `CupelPipeline` instance needed
- (.NET) Call `CupelPipeline.PolicySensitivity(items, budget, ("label", policy), ("label2", policy2))` — passing policies instead of pipelines — and get a `PolicySensitivityReport`
- (Rust) Build a `Policy` from trait objects (`scorer`, `slicer`, `placer`, flags) and call `pipeline.dry_run_with_policy(items, budget, &policy)` to get a `SelectionReport`
- (Rust) Call `policy_sensitivity(items, budget, &[("label", &policy), ("label2", &policy2)])` and get a `PolicySensitivityReport` equivalent

### Entry point / environment

- Entry point: library API (`CupelPipeline` in .NET, `Pipeline` in Rust)
- Environment: test and production code that performs fork diagnostics or adaptive context selection
- Live dependencies involved: none (pure library)

## Completion Class

- Contract complete means: all public API signatures match spec; unit and integration tests pass; `dotnet build` 0 warnings; `cargo test` all green; `cargo clippy` clean
- Integration complete means: the policy-based `PolicySensitivity` overload exercises the same content-keyed diff logic as the existing pipeline-based overload — proven by shared test cases that produce identical results from both call sites
- Operational complete means: none (library)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- A caller can run `PolicySensitivity` with two `CupelPolicy` objects (no pipeline construction) and get the same diff as running the two-pipeline overload with equivalent pipelines — test must cover both call sites with the same items/budget
- `DryRunWithPolicy` respects the policy's scorer, slicer, placer, deduplication flag, and overflow strategy — each must be independently exercised by at least one test
- Rust `policy_sensitivity` produces a `PolicySensitivityReport` with correct diff entries for a two-variant comparison — verified by at least 3 integration tests (all-swing, no-swing, partial-swing)
- `cargo test --all-targets` passes; `dotnet test` passes; `cargo clippy --all-targets -- -D warnings` passes

## Risks and Unknowns

- **CupelPolicy doesn't cover CountQuotaSlice** — `CupelPolicy` uses `SlicerType` enum which has no `CountQuota` variant. `DryRunWithPolicy` must accept `ISlicer` directly (not only enum-based policy) or document the gap. The safest path: keep `DryRunWithPolicy` accepting `CupelPolicy` (which cannot express CountQuotaSlice) but document it, and separately expose an `ISlicer`-accepting overload if needed. This is a design decision to be made in S01.
- **Rust `Policy` ownership** — trait objects are `Box<dyn Trait>`, not cloneable. `policy_sensitivity` needs to build temporary pipelines from policies for each variant run. Since `Box<dyn Scorer>` etc. are not `Clone`, the function either takes `&[(label, &Policy)]` and borrows trait objects, or policies hold `Arc<dyn Trait>`. The `Arc` approach is cleaner for multi-run use. Decide in S02.
- **Budget parameter semantics for `DryRunWithPolicy`** — `.NET` `DryRun()` ignores the pipeline's budget and runs at the stored budget. `DryRunWithPolicy` must accept a budget explicitly (since policy doesn't carry one), matching what `DryRunWithBudget` does internally. Need to ensure the spec and public API are consistent here.

## Existing Codebase / Prior Art

- `src/Wollax.Cupel/CupelPipeline.cs` — `DryRun()` (public), `DryRunWithBudget()` (internal), `ExecuteCore()` (private); budget-override pattern already exists
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityExtensions.cs` — existing `PolicySensitivity(items, budget, (label, pipeline)[])` overload; the new overload joins this file
- `src/Wollax.Cupel/Diagnostics/PolicySensitivityReport.cs` / `PolicySensitivityDiffEntry.cs` — types reused unchanged
- `src/Wollax.Cupel/CupelPolicy.cs` — existing policy struct; `PipelineBuilder.WithPolicy()` shows enum→concrete mapping; `DryRunWithPolicy` will use the same mapping
- `src/Wollax.Cupel/PipelineBuilder.cs` — `WithPolicy()` at line 198 shows how to translate `CupelPolicy` to concrete scorer/slicer/placer instances; `DryRunWithPolicy` will use extracted helper
- `crates/cupel/src/pipeline/mod.rs` — `dry_run()` at line 479; `PipelineBuilder` at line 693; both are the Rust extension points
- `crates/cupel/src/scorer/mod.rs`, `slicer/mod.rs`, `placer/mod.rs` — trait definitions; `Policy` struct holds `Box<dyn Scorer>`, `Box<dyn Slicer>`, `Box<dyn Placer>`

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R056 — this milestone fully implements it

## Scope

### In Scope

- `.NET`: `CupelPipeline.DryRunWithPolicy(items, budget, policy)` public method
- `.NET`: `PolicySensitivity(items, budget, (label, policy)[])` overload in `PolicySensitivityExtensions`
- `Rust`: `Policy` struct with `Box<dyn Scorer>`, `Box<dyn Slicer>`, `Box<dyn Placer>`, `deduplication`, `overflow_strategy`; `PolicyBuilder` fluent builder
- `Rust`: `Pipeline::dry_run_with_policy(items, budget, policy)` method
- `Rust`: `policy_sensitivity(items, budget, variants)` free function (or method) returning Rust `PolicySensitivityReport`
- Spec chapter: `spec/src/analytics/policy-sensitivity.md` (or subsection in existing budget-simulation.md) documenting the API contracts for both languages
- Conformance: at least 3 conformance-level integration tests per language for `policy_sensitivity`
- PublicAPI surface audit (`.txt` files) for .NET

### Out of Scope / Non-Goals

- `CountConstrainedKnapsackSlice` support in `CupelPolicy` (separate deferred item)
- `DryRunWithPolicy` accepting arbitrary `ISlicer`/`IScorer`/`IPlacer` directly without a policy wrapper (may revisit if demand appears)
- Rust serde support for `Policy` (not needed; `Policy` holds trait objects which cannot be serialized)
- Budget simulation methods (`GetMarginalItems`, `FindMinBudgetFor`) in Rust — separate deferred item

## Technical Constraints

- .NET `CupelPipeline` constructor is `internal` — `DryRunWithPolicy` must be implemented as a method on `CupelPipeline`, not an extension
- Rust `Pipeline` stores `Box<dyn Scorer/Slicer/Placer>` which are `!Clone` — `Policy` must use `Arc<dyn Trait>` or the `dry_run_with_policy` method must build a temporary pipeline internally via borrowing
- `.NET` `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` must be kept consistent with any new public surface

## Integration Points

- `PolicySensitivityExtensions` — new overload joins existing static class; no new types needed for .NET
- `spec/src/analytics/` — new or extended spec chapter documents the API; must align with `budget-simulation.md` conventions

## Open Questions

- **Should Rust `Policy` use `Arc<dyn Trait>` or `Box<dyn Trait>`?** Current thinking: `Arc` — allows shared scorer/slicer/placer instances across multiple `Policy` objects and across `policy_sensitivity` variant runs without cloning. Decide in S02.
- **Should `.NET` `DryRunWithPolicy` also accept an optional budget override, or always require a budget?** Current thinking: require an explicit budget (matches `DryRunWithBudget` internal pattern), since there's no "inherit from pipeline" budget that makes sense when running a different policy.
