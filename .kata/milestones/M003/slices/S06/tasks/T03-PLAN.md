---
estimated_steps: 4
estimated_files: 2
---

# T03: Full verification + decision register + summary

**Slice:** S06 — Budget simulation + tiebreaker + spec alignment
**Milestone:** M003

## Description

Final integration gate for S06 and M003. Run the full test suites in both languages to confirm no regressions. Verify all spec alignment changes via grep checks. Append S06 planning decisions to `DECISIONS.md`. Write `S06-SUMMARY.md` capturing what was done, what was verified, and forward intelligence for any future work.

## Steps

1. **Run full test suites**: `rtk dotnet test` (all projects) and `rtk cargo test --all-targets`. Both must pass with zero failures. If any test fails, investigate and fix before proceeding.

2. **Run all grep verification checks**: Confirm spec alignment is complete:
   - `grep -q "count-quota" spec/src/SUMMARY.md`
   - `grep -q "CountQuotaSlice" spec/src/slicers.md`
   - `grep -q "1.3.0" spec/src/changelog.md`
   - `grep -q "GetMarginalItems\|FindMinBudgetFor" src/Wollax.Cupel/PublicAPI.Unshipped.txt`
   - `dotnet build src/Wollax.Cupel/ 2>&1 | grep RS0016 | wc -l` → 0

3. **Append decisions to `.kata/DECISIONS.md`**: Record S06 planning/execution decisions:
   - D107: Budget-override seam via `DryRunWithBudget` internal method (not public API)
   - D108: Tiebreaker rule formalized as stable-index ascending (not id ascending) — no `Id` field exists
   - D109: S06 verification strategy — contract + integration (budget simulation TUnit tests + spec grep checks + Rust tiebreaker test)

4. **Write `S06-SUMMARY.md`**: Follow established summary template from S04-SUMMARY.md. Include: provides, requires, affects, key_files, key_decisions, patterns_established, verification_result, forward intelligence (what the next milestone should know), files created/modified, requirements advanced (none — S06 has no Active requirements; it closes milestone-level acceptance gaps).

## Must-Haves

- [ ] `dotnet test` full solution passes with zero failures
- [ ] `cargo test --all-targets` passes with zero failures
- [ ] All 7 grep verification checks pass
- [ ] DECISIONS.md updated with S06 decisions
- [ ] S06-SUMMARY.md written with all required sections

## Verification

- `rtk dotnet test` → all pass
- `rtk cargo test --all-targets` → all pass
- All grep checks from slice-level verification pass
- `test -f .kata/milestones/M003/slices/S06/S06-SUMMARY.md` → exists

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: S06-SUMMARY.md provides forward intelligence; DECISIONS.md provides decision audit trail
- Failure state exposed: None

## Inputs

- T01 output: `BudgetSimulationExtensions.cs`, `BudgetSimulationTests.cs`, updated `CupelPipeline.cs`
- T02 output: all spec files, `greedy_tiebreaker.rs`
- `.kata/DECISIONS.md` — append-only register
- S04-SUMMARY.md — template reference for summary format

## Expected Output

- `.kata/DECISIONS.md` — modified (3 new decision rows appended)
- `.kata/milestones/M003/slices/S06/S06-SUMMARY.md` — new file with full summary
