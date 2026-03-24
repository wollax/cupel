# M005: cupel-testing crate — Context

**Gathered:** 2026-03-24
**Status:** Ready for planning

## Project Description

Implement a separate `cupel-testing` Rust crate that provides the 13 spec assertion patterns from `spec/src/testing/vocabulary.md` as a fluent chain API over `SelectionReport`. This is the Rust equivalent of the .NET `Wollax.Cupel.Testing` package (minus snapshot support — Rust callers use `insta`).

## Why This Milestone

.NET has `Wollax.Cupel.Testing` with 13 named assertions + snapshot testing. Rust callers currently hand-roll `assert!()` calls over `SelectionReport` fields — verbose, error-prone, and inconsistent. A dedicated `cupel-testing` crate closes the testing DX parity gap.

## User-Visible Outcome

### When this milestone is complete, the user can:

- Add `cupel-testing` as a `dev-dependency` in their Rust project and write `report.should().include_item_with_kind(kind).have_kind_coverage_count(3)` fluent assertions
- Get structured panic messages on failure (assertion name, expected, actual) — matching the .NET `SelectionReportAssertionException` contract

### Entry point / environment

- Entry point: `use cupel_testing::SelectionReportAssertions;` trait import
- Environment: Rust test harness (`cargo test`)
- Live dependencies involved: none

## Completion Class

- Contract complete means: all 13 assertion patterns compile, pass positive and negative tests, panic messages match spec format
- Integration complete means: assertions work on `SelectionReport` produced by real `Pipeline::run_traced()` calls
- Operational complete means: none (library crate, no runtime)

## Final Integrated Acceptance

To call this milestone complete, we must prove:

- All 13 spec assertion patterns are implemented and tested (positive + negative cases)
- Fluent chaining works: `report.should().include_item_with_kind(k).have_at_least_n_exclusions(2)` compiles and runs
- Panic messages include assertion name, expected value, and actual value
- `cargo test --all-targets` passes across both `cupel` and `cupel-testing` crates
- `cargo clippy --all-targets -- -D warnings` is clean
- Crate is publishable (`cargo package` succeeds)

## Risks and Unknowns

- Fluent chain returning `&mut self` with panic-on-failure is straightforward — no major unknowns
- Pattern 6 (`ExcludeItemWithBudgetDetails`) has a .NET degenerate form (flat enum, no token fields on `ExclusionReason`). Rust `ExclusionReason::BudgetExceeded` carries `{ tokens, available }` fields — the Rust version can be fully faithful to the spec. Need to verify the exact Rust enum variant fields.
- Pattern 13 (`PlaceTopNScoredAtEdges`) has the most complex logic — score sorting + edge position mapping + tie handling

## Existing Codebase / Prior Art

- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — .NET reference implementation (13 patterns + snapshot)
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — .NET exception type
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — .NET `Should()` extension method
- `spec/src/testing/vocabulary.md` — spec-level vocabulary definition (13 patterns, PD-1 through PD-4)
- `crates/cupel/src/diagnostics.rs` (or `diagnostics/`) — `SelectionReport`, `IncludedItem`, `ExcludedItem`, `ExclusionReason`
- `crates/cupel/src/analytics.rs` — `budget_utilization`, `kind_diversity` (used by Budget and Coverage patterns)

> See `.kata/DECISIONS.md` for all architectural and pattern decisions — it is an append-only register; read it during planning, append to it during execution.

## Relevant Requirements

- R060 — this milestone implements it fully

## Scope

### In Scope

- Separate `crates/cupel-testing/` crate with `cupel` as a dependency
- `SelectionReportAssertions` trait providing `should()` method on `SelectionReport`
- `SelectionReportAssertionChain` struct with 13 assertion methods
- Structured panic messages matching spec error message contract
- Positive and negative tests for all 13 patterns
- Integration tests using real `Pipeline::run_traced()` output
- `cargo package` readiness (README, license, Cargo.toml metadata)

### Out of Scope / Non-Goals

- Snapshot testing (D107 — Rust callers use `insta`)
- Publishing to crates.io (manual step after milestone, like prior releases)
- Conformance vectors (the spec vocabulary is not a conformance-vector chapter)
- Any changes to the `cupel` core crate

## Technical Constraints

- Rust Edition 2024, MSRV 1.85 (matching `cupel` crate)
- No external dependencies beyond `cupel` itself
- Must not pull in `serde` or any optional feature of `cupel` — testing crate works with default features
- Panic on assertion failure (not `Result`), matching Rust test convention

## Integration Points

- `cupel` crate — depends on `SelectionReport`, `IncludedItem`, `ExcludedItem`, `ExclusionReason`, `ContextKind`, `ContextBudget`
- `cupel::analytics` — may call `budget_utilization()` and `kind_diversity()` internally for Budget and Coverage patterns (or recompute inline)

## Open Questions

- None — all decisions locked during discussion (D126–D128)
