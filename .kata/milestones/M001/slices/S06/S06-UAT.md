# S06: .NET Quality Hardening — UAT

**Milestone:** M001
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All behaviors are machine-verifiable via `dotnet test` and `dotnet build`. No runtime service, no UI, no human experience to assess. The KnapsackSlice guard is verified by exception-throwing tests; the epsilon fix is verified by quota validation tests; all other changes are covered by build and existing test suite.

## Preconditions

- .NET 10 SDK installed
- `dotnet build` succeeds from repo root
- All test projects build without errors

## Smoke Test

Run `dotnet test` from the repo root. All 658 tests must pass with 0 failures.

## Test Cases

### 1. KnapsackSlice DP guard — at-limit passes

1. Run `dotnet test` (guard boundary tests are included in the full suite)
2. Verify `DpTableGuard_AtExactLimit_Passes` passes: 5000 items × targetTokens=9999 → 5000×10000=50M cells → no exception
3. **Expected:** test passes; `Slice()` returns normally

### 2. KnapsackSlice DP guard — above-limit throws

1. Verify `DpTableGuard_OneAboveLimit_Throws` passes: 5000 items × targetTokens=10000 → 50,005,000 cells → `InvalidOperationException`
2. Verify `DpTableGuard_ClearlyOverLimit_Throws` passes: 10000 items × targetTokens=5000 → 50,010,000 cells → `InvalidOperationException`
3. **Expected:** both tests pass; exception message includes `candidates=`, `capacity=`, `cells=` fields

### 3. Equal-share quota acceptance

1. Confirm `QuotaBuilder` accepts three quotas each at 33.333...% (floating-point sum ≈ 100.0 but may exceed 100.0 by sub-epsilon)
2. **Expected:** no `ArgumentException` thrown; pipeline builds successfully

### 4. API surface: no OverflowStrategyValue references

1. Run `rg "OverflowStrategyValue" src/ tests/`
2. **Expected:** zero matches

### 5. Full test suite green

1. Run `dotnet test` from repo root
2. **Expected:** total ≥ 649, 0 failed, 0 skipped

### 6. Build clean

1. Run `dotnet build` from repo root
2. **Expected:** 0 errors, 0 warnings

## Edge Cases

### KnapsackSlice — negative-token items silently skipped

1. `NegativeTokenItems_SilentlyExcluded` test: items with negative token counts are excluded from candidates before guard check and do not crash
2. **Expected:** test passes; result list omits negative-token items; no exception thrown

### QuotaSlice — null constructor args

1. `QuotaSlice_NullSlicer_ThrowsArgumentNull`: passing `null` for slicer → `ArgumentNullException`
2. `QuotaSlice_NullQuotas_ThrowsArgumentNull`: passing `null` for quotas → `ArgumentNullException`
3. **Expected:** both throw `ArgumentNullException` exactly (not a subtype)

### TagScorer — case-insensitive matching

1. `TagScorer_CaseInsensitiveMatch`: scorer key `"important"` (lowercase), item tag `"IMPORTANT"` (uppercase) → score > 0
2. **Expected:** test passes; no case-sensitive miss

### TagScorer — zero total weight

1. `TagScorer_ZeroTotalWeight_ReturnsZeroScore`: scorer with `"important"` weight `0.0` → score = 0.0 without division-by-zero
2. **Expected:** test passes; score is exactly 0.0

## Failure Signals

- Any test failure in `dotnet test` output names the failing assertion and method
- `InvalidOperationException.Message` from the KnapsackSlice guard states `candidates=N, capacity=C, cells=K` — if message format is wrong, the diagnostic-message tests fail
- `rg "OverflowStrategyValue"` returning results means the rename is incomplete
- `dotnet build` warnings indicate doc comment or compiler issues introduced

## Requirements Proved By This UAT

- R002 (partial, .NET half) — `KnapsackSlice.Slice()` throws `InvalidOperationException` when candidateCount × (capacity+1) > 50M cells; guard boundary tests confirm exact threshold behavior; exception message is diagnostic and actionable
- R004 — all 20 .NET triage items resolved; `dotnet build` clean; `dotnet test` 658 tests with zero regressions; scoring tests (PriorityScorer range, TagScorer case-insensitive + zero-weight), slicer tests (QuotaSlice null-args, KnapsackSlice negative-token), pipeline tests (CupelPolicy stream-batch validation) all added

## Not Proven By This UAT

- R002 Rust half — `CupelError::TableTooLarge` and the Rust KnapsackSlice guard are not implemented; that half is owned by S07
- Runtime behavior under actual LLM agent workloads — library is unit-tested only; no integration/e2e harness
- Performance of the KnapsackSlice at the 50M-cell boundary — guard prevents the allocation entirely, so actual DP table performance at the boundary is untested
- Documentation rendering — XML doc comments are authored but not rendered via `dotnet doc` output; correctness is visual/textual only

## Notes for Tester

- The `dotnet test --filter FullyQualifiedName~KnapsackSlice` scoped filter returns exit code 5 with this test runner version — use `dotnet test` (full suite) to confirm guard tests pass; they are included and counted in the 658 total
- The `PipelineBuilderTests.cs:688` assertion was already `IsEqualTo(3)` before S06; no change was made to that file
- T04 summary reported "663 tests" due to a transcription error in the narrative; the authoritative count is 658 (verified by `dotnet test` run)
