---
id: T01
parent: S06
milestone: M003
provides:
  - BudgetSimulationTests.cs with 11 tests covering GetMarginalItems and FindMinBudgetFor
  - Stub BudgetSimulationExtensions.cs with NotImplementedException stubs for both APIs
  - PublicAPI.Unshipped.txt entries for BudgetSimulationExtensions
  - .NET GreedySlice deterministic tie-break regression tests (4 new tests)
  - Rust GreedySlice deterministic tie-break regression tests (4 new tests)
key_files:
  - tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs
  - src/Wollax.Cupel/BudgetSimulationExtensions.cs
  - src/Wollax.Cupel/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs
  - crates/cupel/src/slicer/greedy.rs
key_decisions:
  - "Added NotImplementedException stubs so tests compile but fail at runtime — T02/T03 will replace stubs with real implementation"
  - "Budget simulation methods are extension methods on CupelPipeline per spec"
patterns_established:
  - "Reference-equality assertions for diff semantics using ReferenceEquals loops"
  - "Guard message assertions via Throws<T>() then checking ex.Message separately (TUnit pattern)"
observability_surfaces:
  - "Test failure messages expose exact missing API names and unimplemented stubs"
duration: 12min
verification_result: passed
completed_at: 2026-03-23T12:00:00Z
blocker_discovered: false
---

# T01: Write failing-first verification for budget simulation and deterministic ties

**Created 11 budget-simulation tests (all failing with NotImplementedException stubs) plus 8 deterministic tie-break regression tests across .NET and Rust (all passing)**

## What Happened

Created `BudgetSimulationTests.cs` with 11 TUnit tests covering both `GetMarginalItems` and `FindMinBudgetFor` APIs: 3 happy-path tests per method, 1 QuotaSlice guard for GetMarginalItems, 2 argument guards for FindMinBudgetFor, and 2 monotonicity guards (QuotaSlice + CountQuotaSlice) for FindMinBudgetFor. Tests assert reference-equality diff semantics, exact guard exception messages, and binary-search first-hit semantics.

Added `BudgetSimulationExtensions.cs` with `NotImplementedException` stubs so the test file compiles. All 11 budget-simulation tests fail with `NotImplementedException` as the expected initial state. Added the public API entries to `PublicAPI.Unshipped.txt`.

Extended `GreedySliceTests.cs` with 4 deterministic tie-break regression tests: equal density with varying score/token ratios, zero-token tie ordering (score must not affect order), budget-constrained equal-density drop order, and idempotency check (10 runs). All 4 pass immediately — the existing GreedySlice implementation already preserves input order via stable sort with index tiebreak.

Added matching Rust tests in `greedy.rs` with the same 4 scenarios. All pass.

## Verification

- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` — 622 total: 611 passed, 11 failed (all BudgetSimulation NotImplementedException)
- `cargo test greedy -- --nocapture` (in crates/cupel) — 5 unit tests + 6 conformance tests all pass
- `cargo test --all-targets` (in crates/cupel) — 128 tests pass across all suites
- Build: 0 errors, 0 warnings after adding PublicAPI entries

## Diagnostics

Run the budget-simulation focused tests to see exact remaining implementation gaps:
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` and look for `NotImplementedException` failures
- Test names directly map to API contracts: `GetMarginalItems_*` and `FindMinBudgetFor_*`

## Deviations

- Added `BudgetSimulationExtensions.cs` stub file and `PublicAPI.Unshipped.txt` entries — not in the original plan but required for tests to compile. T02/T03 will replace the stubs with real implementation.

## Known Issues

None.

## Files Created/Modified

- `tests/Wollax.Cupel.Tests/Pipeline/BudgetSimulationTests.cs` — 11 new tests for budget-simulation API
- `src/Wollax.Cupel/BudgetSimulationExtensions.cs` — Stub extension methods (NotImplementedException)
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — Added BudgetSimulationExtensions API entries
- `tests/Wollax.Cupel.Tests/Slicing/GreedySliceTests.cs` — 4 new deterministic tie-break regression tests
- `crates/cupel/src/slicer/greedy.rs` — 4 new Rust deterministic tie-break regression tests
