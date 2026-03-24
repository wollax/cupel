# M006: Count-Based Quotas

**Vision:** Implement `CountQuotaSlice` in both Rust and .NET — a decorator slicer that enforces absolute item-count requirements and per-kind caps before delegating to an inner slicer. Callers get "at least N items of kind K, capped at M items of kind K" semantics composable with the existing `QuotaSlice` percentage constraints.

## Success Criteria

- `CountQuotaSlice` compiles and is exported from both `cupel` (Rust) and `Wollax.Cupel` (.NET)
- All five conformance scenarios from `.planning/design/count-quota-design.md` have passing tests in both languages
- `ExclusionReason::CountCapExceeded` / `CountCapExceeded` appears on excluded items when a kind cap is reached
- `SelectionReport.count_requirement_shortfalls` / `QuotaViolations` is populated when scarcity degrades a require constraint
- Construction-time guards reject `require > cap` and `KnapsackSlice` inner slicer
- `CountQuotaSlice` implements `QuotaPolicy` / `IQuotaPolicy`; `quota_utilization` works without regression
- `cargo test --all-targets` + `cargo clippy --all-targets -- -D warnings` clean in both Rust crates
- `dotnet test` (full solution) + `dotnet build` 0 warnings in .NET

## Key Risks / Unknowns

- Existing skeleton completeness — both files exist but their completeness is unknown; the first task audits them before any new code is written
- SelectionReport field wiring — `count_requirement_shortfalls` exists in the struct but may not be populated by the pipeline; integration test proves this is actually wired end-to-end

## Proof Strategy

- Existing skeleton completeness → retire in S01 (Rust) and S02 (.NET) by auditing and completing the implementations with real integration tests
- SelectionReport field wiring → retire in S01 (Rust) by running `dry_run()` and asserting `count_requirement_shortfalls` is non-empty in the scarcity test

## Verification Classes

- Contract verification: `cargo test --all-targets`, `cargo clippy --all-targets -- -D warnings`, `dotnet build` (0 warnings), `dotnet test` (full solution), PublicAPI analyzer
- Integration verification: real `Pipeline::builder()` + `CountQuotaSlice` + `run_traced()` / `dry_run()` proving `SelectionReport` fields are populated; `CountQuotaSlice + QuotaSlice` composition; `KnapsackSlice` guard
- Operational verification: none (library crate)
- UAT / human verification: none

## Milestone Definition of Done

This milestone is complete only when all are true:

- `CountQuotaSlice` fully implemented in Rust (`crates/cupel/src/slicer/count_quota.rs`) with no stubs
- `CountQuotaSlice` fully implemented in .NET (`src/Wollax.Cupel/Slicing/CountQuotaSlice.cs`) with no stubs
- All 5 conformance scenarios have passing tests in both languages
- `CountCapExceeded` appears in `SelectionReport` excluded items for real pipeline runs
- `count_requirement_shortfalls` / `QuotaViolations` appears for real pipeline runs with scarcity
- `quota_utilization` with `CountQuotaSlice` tested and passing
- Both `cargo test --all-targets` and `dotnet test` (full solution) pass with 0 failures
- No clippy warnings in Rust, no build warnings in .NET

## Requirement Coverage

- Covers: R061
- Partially covers: none
- Leaves for later: `CountConstrainedKnapsackSlice` (R062, deferred)
- Orphan risks: none

## Slices

- [x] **S01: Rust CountQuotaSlice — audit, complete, and test** `risk:high` `depends:[]`
  > After this: `CountQuotaSlice` is fully implemented in Rust; all 5 conformance scenarios pass in `crates/cupel` integration tests; `ExclusionReason::CountCapExceeded` and `count_requirement_shortfalls` appear in real `dry_run()` output; clippy clean.

- [x] **S02: .NET CountQuotaSlice — audit, complete, and test** `risk:medium` `depends:[S01]`
  > After this: `CountQuotaSlice` is fully implemented in .NET; all 5 conformance scenarios pass in `Wollax.Cupel.Tests`; `QuotaViolations` and `CountCapExceeded` appear in real pipeline output; `dotnet build` 0 warnings; `quota_utilization` with `CountQuotaSlice` tested.

- [x] **S03: Integration proof + summaries** `risk:low` `depends:[S01,S02]`
  > After this: `CountQuotaSlice + QuotaSlice` composition tested end-to-end in both languages; `PublicAPI.Unshipped.txt` updated in .NET; `REQUIREMENTS.md` R061 validated; all tests pass in full solution; M006 complete.

## Boundary Map

### S01 → S02

Produces:
- `crates/cupel/src/slicer/count_quota.rs` — full two-phase implementation: `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`
- `ExclusionReason::CountCapExceeded { kind, cap, count }` variant in `crates/cupel/src/diagnostics/mod.rs`
- `SelectionReport::count_requirement_shortfalls: Vec<CountRequirementShortfall>` populated by pipeline
- Integration test reference shapes for all 5 conformance scenarios (Rust test patterns for .NET to mirror)

Consumes: nothing (first slice)

### S01 → S03

Produces:
- Confirmed `CountQuotaSlice + GreedySlice` working in Rust (reference for composition test)
- Confirmed `QuotaPolicy` implementation on `CountQuotaSlice` (for `quota_utilization` integration)

### S02 → S03

Produces:
- `src/Wollax.Cupel/Slicing/CountQuotaSlice.cs` — full two-phase implementation
- `CountCapExceeded` in .NET `ExclusionReason` enum
- `SelectionReport.QuotaViolations` populated by .NET pipeline
- `PublicAPI.Unshipped.txt` updated with all new public surface

Consumes from S01:
- Conformance scenario shapes (test structure mirrors Rust)
- Design doc rulings (already settled — DI-1 through DI-6)
