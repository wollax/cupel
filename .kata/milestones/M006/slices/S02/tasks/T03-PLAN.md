---
estimated_steps: 5
estimated_files: 0
---

# T03: Final build verification and quota_utilization check

**Slice:** S02 — .NET CountQuotaSlice — audit, complete, and test
**Milestone:** M006

## Description

Full-solution verification pass: confirm `dotnet build` (all 14 projects) is 0 warnings, `dotnet test` (all test projects) passes with 0 failures, `quota_utilization` tests with `CountQuotaSlice` pass (confirming `IQuotaPolicy` implementation is unbroken by T01 changes), and `PublicAPI.Unshipped.txt` requires no updates (all T01 changes are internal). This is the milestone's S02 acceptance gate.

**Constraints:**
- Research confirmed `CountQuotaSlice_ImplementsIQuotaPolicy_GetConstraints_ReturnsCorrectEntries` and `QuotaUtilization_WithCountQuotaSlice` tests already exist and pass — T01 changes must not break them
- No new public API surface was introduced in T01/T02 (all wiring changes are in `internal sealed` `ReportBuilder` and the non-public `Execute()` body); `PublicAPI.Unshipped.txt` should not need updates
- If any warning is found, fix it before marking T03 done

## Steps

1. **Run full solution build**: `dotnet build cupel.sln 2>&1 | grep -E "error|warning"` — must produce 0 lines output. If warnings appear, identify source and fix (likely missing XML doc or namespace issue from T01/T02).

2. **Run full test suite for `Wollax.Cupel.Tests`**: `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | tail -10` — confirm 669 passed, 0 failed, 0 skipped.

3. **Verify `quota_utilization` tests pass**: `grep -rn "CountQuotaSlice\|QuotaUtilization.*Count\|WithCountQuotaSlice" tests/Wollax.Cupel.Tests/ --include="*.cs" -l` to find the relevant test file; confirm those tests appear in the 669 total and passed.

4. **Confirm `PublicAPI.Unshipped.txt` unchanged**: `git diff src/Wollax.Cupel/PublicAPI.Unshipped.txt` — must show no changes. If the file changed, investigate — T01 must not have introduced new public members accidentally.

5. **Run all test projects for full-solution regression**: `dotnet test cupel.sln 2>&1 | tail -15` — confirm 0 failed across `Wollax.Cupel.Tests`, `Wollax.Cupel.Testing.Tests`, `Wollax.Cupel.Diagnostics.OpenTelemetry.Tests`, and the consumption test project.

## Must-Haves

- [ ] `dotnet build cupel.sln` exits 0 errors, 0 warnings
- [ ] `dotnet test --project tests/Wollax.Cupel.Tests/` reports 669 passed, 0 failed
- [ ] `quota_utilization` tests with `CountQuotaSlice` pass (R052 not regressed)
- [ ] `PublicAPI.Unshipped.txt` unchanged from before S02 (no accidental public API additions)
- [ ] All test projects in the solution pass (no regressions in `Wollax.Cupel.Testing.Tests` or OTel tests)

## Verification

- `dotnet build cupel.sln 2>&1 | grep -c "error\|warning"` — output is 0
- `dotnet test --project tests/Wollax.Cupel.Tests/ 2>&1 | grep "total:"` — shows 669
- `git diff src/Wollax.Cupel/PublicAPI.Unshipped.txt` — no output

## Observability Impact

- Signals added/changed: none — verification task only
- How a future agent inspects this: the clean `dotnet build` + `dotnet test` outputs are the signal; if either fails, the error message pinpoints the issue
- Failure state exposed: build warnings flag missing XML docs; test failures identify the specific assertion and test name

## Inputs

- T01 and T02 completed
- `dotnet build` and `dotnet test` toolchain available
- `git diff` to confirm no unintended file changes

## Expected Output

- No files modified — all pass/fail verification
- S02 completion confirmed: all 5 conformance integration tests pass; both pipeline wiring gaps closed; `dotnet build` 0 warnings; R061 (.NET) implementation complete
