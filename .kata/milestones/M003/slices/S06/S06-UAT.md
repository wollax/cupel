# S06: Budget simulation + tiebreaker + spec alignment — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S06 ships library APIs (no UI/service) and spec documentation; all behavioral contracts are mechanically verifiable through tests and grep; no live runtime or human-experience aspects

## Preconditions

- .NET 10 SDK installed
- Rust toolchain installed (1.85+)
- Repository checked out at S06 completion commit

## Smoke Test

Run `dotnet test` and `cargo test --all-targets` — both should exit 0 with no failures.

## Test Cases

### 1. Budget simulation API exists and works

1. Run `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj -- --treenode-filter "/*/*/BudgetSimulationTests/*"`
2. **Expected:** 11 tests pass — covering GetMarginalItems happy path, FindMinBudgetFor binary search, guard messages, and edge cases

### 2. Deterministic tie-break in .NET

1. Run `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj -- --treenode-filter "/*/*/GreedySliceTests/*"`
2. **Expected:** 14 tests pass — including 4 explicit equal-density, zero-token, budget-constrained, and idempotency tie-break regressions

### 3. Deterministic tie-break in Rust

1. Run `cd crates/cupel && cargo test greedy`
2. **Expected:** All greedy tests pass including 4 tie-break regression tests

### 4. PublicAPI surface is complete

1. Run `grep "GetMarginalItems\|FindMinBudgetFor" src/Wollax.Cupel/PublicAPI.Unshipped.txt`
2. **Expected:** Both API entries present

### 5. Spec navigation and changelog alignment

1. Run `grep "CountQuotaSlice" spec/src/SUMMARY.md spec/src/slicers.md spec/src/slicers/count-quota.md`
2. Run `grep "DecayScorer" spec/src/scorers.md`
3. Run `grep "1.3.0" spec/src/changelog.md`
4. **Expected:** All references resolve — CountQuotaSlice in nav/index/page, DecayScorer in scorer table, v1.3.0 in changelog

### 6. Build produces no errors

1. Run `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`
2. **Expected:** 0 errors, 0 warnings (including no RS0016/PublicAPI failures)

## Edge Cases

### FindMinBudgetFor with single item at exact lower bound

1. The test `FindMinBudgetFor_ReturnsMinimumBudget_WhenItemFitsExactly` in BudgetSimulationTests.cs covers this
2. **Expected:** Returns exactly `targetItem.Tokens`, not `targetItem.Tokens + 1`

### QuotaSlice monotonicity guard

1. The test `GetMarginalItems_ThrowsForQuotaSlice` and `FindMinBudgetFor_ThrowsForQuotaSlice` cover this
2. **Expected:** InvalidOperationException with the exact spec-defined message

## Failure Signals

- Any test failure in BudgetSimulationTests or GreedySliceTests indicates a regression in budget simulation or tie-breaking
- RS0016 build warnings indicate missing PublicAPI.Unshipped.txt entries
- Missing grep matches in spec files indicate documentation gaps

## Requirements Proved By This UAT

- No active requirements are directly owned by S06; the budget-simulation API is the implementation counterpart of R044's spec design; the tie-break contract closes a milestone-level acceptance criterion

## Not Proven By This UAT

- Rust budget-simulation parity (intentionally deferred; documented in spec)
- End-to-end OTel bridge integration with budget simulation (covered by S05 UAT)
- Cross-package consumption of budget simulation APIs (covered by core test suite, not a separate package)

## Notes for Tester

- The `--filter` syntax does NOT work with TUnit — use `-- --treenode-filter "/*/*/TestClassName/*"` instead
- Budget simulation tests create real CupelPipeline instances with GreedySlice and run actual DryRun calls — they are integration-level, not mocks
