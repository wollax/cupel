---
estimated_steps: 4
estimated_files: 4
---

# T02: Update REQUIREMENTS.md, write summaries, update STATE.md

**Slice:** S03 — Integration tests + publish readiness
**Milestone:** M005

## Description

Close the planning record for S03 and M005. Mark R060 validated in `REQUIREMENTS.md`, write the S03 slice summary, update the M005 milestone summary, and set `STATE.md` to reflect the completed milestone. This task has no code changes — it is purely artifact maintenance.

## Steps

1. Update `.kata/REQUIREMENTS.md`:
   - Change R060 `Status:` from `active` to `validated`
   - Update R060 `Validation:` field to: "validated — all 13 spec assertion patterns implemented on `SelectionReportAssertionChain` with 26+1 integration tests (26 per-pattern + 1 chained end-to-end); `cargo package` exits 0 for `cupel-testing`; both crates `cargo test --all-targets` + clippy clean; M005/S03 complete"
   - Update the traceability table row for R060: change `active` to `validated` and update Proof cell
   - Update Coverage Summary: change "Active requirements: 1 (R060)" to "Active requirements: 0"; add R060 to the Validated count
2. Write `.kata/milestones/M005/slices/S03/S03-SUMMARY.md` using the standard task-summary frontmatter format
3. Update `.kata/milestones/M005/M005-SUMMARY.md` with S03's contributions (create the file if it doesn't exist)
4. Update `.kata/STATE.md`: set milestone M005 complete, no active slice/task, Phase=Complete, Next Action="M005 complete — all success criteria met"

## Must-Haves

- [ ] R060 `Status` field reads `validated` in `REQUIREMENTS.md`
- [ ] R060 `Validation` field describes M005/S03 completion evidence
- [ ] Traceability table R060 row updated to `validated`
- [ ] Coverage Summary updated (0 active requirements)
- [ ] `S03-SUMMARY.md` written with frontmatter (id, parent, milestone, provides, key_files, verification_result=pass)
- [ ] `M005-SUMMARY.md` exists and includes S03 contributions
- [ ] `STATE.md` reflects M005 complete, no active slice/task

## Verification

- `grep -A2 "R060" .kata/REQUIREMENTS.md | grep "validated"` → matches
- `ls .kata/milestones/M005/slices/S03/S03-SUMMARY.md` → file exists
- `ls .kata/milestones/M005/M005-SUMMARY.md` → file exists
- `grep "Complete\|complete" .kata/STATE.md` → matches

## Observability Impact

- Signals added/changed: None (artifact-only task)
- How a future agent inspects this: read `.kata/STATE.md` for current milestone status; read `REQUIREMENTS.md` to confirm R060 is validated
- Failure state exposed: None

## Inputs

- T01 verification results (all must pass before this task runs)
- `.kata/REQUIREMENTS.md` — current R060 entry to update
- `.kata/milestones/M005/slices/S02/S02-SUMMARY.md` — prior slice summary for milestone rollup
- `.kata/milestones/M005/M005-ROADMAP.md` — success criteria to confirm as met in M005-SUMMARY.md

## Expected Output

- `.kata/REQUIREMENTS.md` — R060 marked validated with evidence
- `.kata/milestones/M005/slices/S03/S03-SUMMARY.md` — slice completion record
- `.kata/milestones/M005/M005-SUMMARY.md` — milestone rollup with all three slices
- `.kata/STATE.md` — M005 complete, no next action pending
