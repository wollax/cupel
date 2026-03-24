---
estimated_steps: 5
estimated_files: 5
---

# T03: PublicAPI audit, R061 validation, and M006 summaries

**Slice:** S03 — Integration proof + summaries
**Milestone:** M006

## Description

Close all documentation and requirement gates for M006: confirm the .NET public API surface is complete (build-verified), mark R061 validated with proof notes, write the missing S01 summary (S01 completed before this planning session with no summary written), write the M006 milestone summary, and update STATE.md. No production code is written.

## Steps

1. **PublicAPI audit**: Run `dotnet build Cupel.slnx 2>&1 | grep -E "error|warning"`. If output is empty, the PublicAPI analyzer is satisfied. Cross-check that `src/Wollax.Cupel/PublicAPI.Unshipped.txt` contains all eight M006 entries: `CountQuotaSlice`, `CountQuotaEntry`, `ScarcityBehavior`, `CountRequirementShortfall`, `ExclusionReason.CountCapExceeded`, `ExclusionReason.CountRequireCandidatesExhausted`, `SelectionReport.CountRequirementShortfalls`, `CountQuotaSlice.GetConstraints()`. The research confirmed all are present — this step is confirmation only, not editing. If `dotnet build` produces warnings, investigate and fix before proceeding.

2. **R061 validation in REQUIREMENTS.md**: In `.kata/REQUIREMENTS.md`, find the R061 block. Change `Status: active` to `Status: validated`. Replace the existing Validation line with:
   ```
   - Validation: validated — Rust: 5 conformance integration tests in crates/cupel/tests/conformance.rs; CountCapExceeded in report.excluded + count_requirement_shortfalls in report via dry_run() proven in S01; CountQuotaSlice+QuotaSlice composition proven in crates/cupel/tests/count_quota_composition.rs; cargo test --all-targets passes. .NET: 5 conformance integration tests in CountQuotaIntegrationTests.cs; CountCapExceeded + CountRequirementShortfalls in DryRun() report proven in S02; CountQuotaSlice+QuotaSlice composition proven in CountQuotaCompositionTests.cs; dotnet test --solution Cupel.slnx passes; PublicAPI.Unshipped.txt complete; dotnet build 0 warnings
   ```
   Also update the traceability table row for R061: change `active — design settled, implementation in progress` to `validated`.

3. **S01 summary**: Write `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` as a compact Kata summary capturing what S01 produced. S01 delivered the full Rust `CountQuotaSlice` implementation. Use the standard YAML frontmatter + prose format. Key content: full `count_quota.rs` implementation, `ExclusionReason::CountCapExceeded`, `count_requirement_shortfalls` in `SelectionReport`, `QuotaPolicy` implementation, 5 conformance TOML vectors passing, `cargo test --all-targets` + clippy clean.

4. **M006 milestone summary**: Write `.kata/milestones/M006/M006-SUMMARY.md` covering all three slices. Follow the milestone summary format (YAML frontmatter with provides/key_files/key_decisions, prose What Happened section, Requirements Validated section, Files Created/Modified). Key provides: `CountQuotaSlice` in both languages, `ExclusionReason::CountCapExceeded`, `count_requirement_shortfalls` / `CountRequirementShortfalls`, `QuotaPolicy` / `IQuotaPolicy` implemented, 10+ integration tests across both languages, composition with `QuotaSlice` proven.

5. **STATE.md update**: Update `.kata/STATE.md` to reflect M006 complete. Set Active Milestone and Active Slice to `none`. Set Phase to `milestone complete`. Set Next Action to `Start M007 or queue next milestone`.

## Must-Haves

- [ ] `dotnet build Cupel.slnx` exits 0 with 0 warnings (PublicAPI audit passed)
- [ ] All eight M006 types confirmed present in `PublicAPI.Unshipped.txt`
- [ ] R061 `Status` is `validated` in `.kata/REQUIREMENTS.md`
- [ ] R061 traceability table entry updated from `active` to `validated`
- [ ] `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` exists and covers key provides from S01
- [ ] `.kata/milestones/M006/M006-SUMMARY.md` exists with all three slices summarised
- [ ] `.kata/STATE.md` reflects M006 complete

## Verification

- `grep -A5 "R061" .kata/REQUIREMENTS.md | grep "Status: validated"` → returns a match
- `dotnet build Cupel.slnx 2>&1 | grep -E "error|warning" | wc -l` → returns 0
- `ls .kata/milestones/M006/slices/S01/S01-SUMMARY.md` → file exists
- `ls .kata/milestones/M006/M006-SUMMARY.md` → file exists

## Observability Impact

- Signals added/changed: None — documentation only; no production code changed
- How a future agent inspects this: `cat .kata/STATE.md` shows M006 complete; `cat .kata/REQUIREMENTS.md | grep -A20 "R061"` shows full validated status with proof
- Failure state exposed: If PublicAPI audit fails, `dotnet build` output names the missing declaration with RS0016 error code

## Inputs

- T01 and T02 must be complete — their test files are cited in the R061 validation proof note
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — read to confirm M006 entries
- `.kata/milestones/M006/slices/S02/S02-SUMMARY.md` — reference for S01 summary format and key_files list
- S03 research: S01 provided `count_quota.rs`, 5 conformance vectors, `ExclusionReason::CountCapExceeded`, `count_requirement_shortfalls`, `QuotaPolicy`

## Expected Output

- `.kata/REQUIREMENTS.md` — R061 status changed to validated with proof note
- `.kata/milestones/M006/slices/S01/S01-SUMMARY.md` — new compact summary of S01 Rust implementation
- `.kata/milestones/M006/M006-SUMMARY.md` — new milestone summary covering S01+S02+S03
- `.kata/STATE.md` — updated to M006 complete
