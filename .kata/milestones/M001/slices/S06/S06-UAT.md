# S06: .NET Quality Hardening — UAT

**Milestone:** M001
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All behaviors in S06 are machine-verifiable unit tests. There is no runtime service, UI, or human-experience surface. `dotnet test` is the definitive oracle; exception messages are inspectable in test output. No live runtime or human UAT is warranted.

## Preconditions

- .NET 10 SDK installed
- Repository at `kata/root/M001/S06` branch (or post-merge to main)
- `dotnet restore` has run (or `dotnet test` handles it automatically)

## Smoke Test

Run `dotnet test` from the repo root — all 658 tests must pass with zero failures. This single check confirms the entire S06 surface area: guard, API hardening, doc changes, and test hygiene.

## Test Cases

### 1. KnapsackSlice DP table guard fires above 50M cells

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/KnapsackSliceTests/*"`
2. Observe: 17 tests pass
3. **Expected:** `DpTableGuard_AtExactLimit_Passes` passes (no throw at exactly 50M cells); `DpTableGuard_OneAboveLimit_Throws` and `DpTableGuard_ClearlyOverLimit_Throws` pass (InvalidOperationException thrown); `NegativeTokenItems_SilentlyExcluded` passes

### 2. Three equal-share quotas are accepted

1. Run `dotnet test --project tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/QuotaBuilderTests/*"` (or full suite)
2. **Expected:** No quota-validation test fails due to floating-point accumulation for three 33.333...% quotas; test suite passes without the spurious "total quota exceeds 100%" rejection

### 3. Policy stream + knapsack validation

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/CupelPolicyTests/*"`
2. Observe: 21 tests pass including `Validation_StreamBatchSizeWithKnapsackSlicer_Throws`
3. **Expected:** `ArgumentException` thrown when `SlicerType.Knapsack` is combined with `streamBatchSize: 10`

### 4. QuotaSlice null constructor argument guards

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/QuotaSliceTests/*"`
2. **Expected:** `QuotaSlice_NullSlicer_ThrowsArgumentNull` and `QuotaSlice_NullQuotas_ThrowsArgumentNull` pass; both throw `ArgumentNullException` exactly

### 5. PriorityScorer and TagScorer new coverage

1. Run: `dotnet test --project tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/PriorityScorerTests/*"`
2. Run: `dotnet test --project tests/Wollax.Cupel.Tests/ --treenode-filter "/*/*/TagScorerTests/*"`
3. **Expected:** `ScoresAreInZeroToOneRange` passes (all PriorityScorer outputs between 0.0 and 1.0); `TagScorer_CaseInsensitiveMatch` passes (uppercase tag matched); `TagScorer_ZeroTotalWeight_ReturnsZeroScore` passes (no divide-by-zero, returns 0.0)

### 6. Build is clean

1. Run: `dotnet build`
2. **Expected:** `Build succeeded. 0 Warning(s). 0 Error(s).`

## Edge Cases

### KnapsackSlice guard exception message content

1. Call `KnapsackSlice.Slice()` with 10000 items and `targetTokens: 5000` (creates > 50M cells)
2. Catch `InvalidOperationException`
3. **Expected:** Exception message contains "candidates=", "capacity=", "cells=" fields for diagnosability

### OverflowStrategy rename — no stale references

1. Run: `rg "OverflowStrategyValue" src/ tests/`
2. **Expected:** Zero results — rename is complete and no old name remains

## Failure Signals

- Any test failure in `dotnet test` output names the failing assertion — investigate the specific class/method
- `InvalidOperationException` with "cells=" in message → guard is active and firing correctly
- `InvalidOperationException` without expected message prefix → guard may be misconfigured
- `dotnet build` warnings about `OverflowStrategyValue` → rename consumer not updated
- Quota-validation test failure mentioning "100" threshold → epsilon fix not applied

## Requirements Proved By This UAT

- R004 — .NET codebase quality hardening: all 20 triage items resolved; naming, error messages, enum anchoring, epsilon fix, XML docs, test gaps, and test hygiene all addressed; 658 tests pass; `dotnet build` clean. R004 is validated.
- R002 (partial) — .NET half of KnapsackSlice DP guard: `InvalidOperationException` thrown at >50M cells with diagnostic message; boundary tests pass (at-limit passes, above-limit throws).

## Not Proven By This UAT

- R002 (Rust half) — `CupelError::TableTooLarge` and Rust KnapsackSlice guard are S07's responsibility. S06 only proves the .NET side of R002.
- R001, R005 — Rust diagnostics parity and Rust quality hardening are S01–S04 and S07 respectively; not covered by S06.
- XML doc quality — `dotnet build` confirms docs compile without errors; it does not verify the prose quality of XML comments. These are human-audited on PR review.
- Runtime behavior under concurrent pipeline runs — `ITraceCollector.IsEnabled` constancy is documented as a contract but not runtime-enforced; no multi-threaded test exists.

## Notes for Tester

- TUnit's `--treenode-filter` uses tree-path syntax (`/*/*/ClassName/*`), not the traditional `--filter FullyQualifiedName~X` — use `--treenode-filter` for class-scoped runs
- The 658 total includes tests from 5 projects: `Wollax.Cupel.Tests`, `Wollax.Cupel.Extensions.DependencyInjection.Tests`, `Wollax.Cupel.Json.Tests`, `Wollax.Cupel.Tiktoken.Tests`, and one more
- T04 summary reports 663 tests; the correct count is 658 (T01-T03 left 653; T04 net +5 = 658). The "649+" slice goal is satisfied.
