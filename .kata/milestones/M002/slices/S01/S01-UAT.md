# S01: Post-v1.2 Brainstorm Sprint — UAT

**Milestone:** M002
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S01 is documentation-only — all deliverables are Markdown files committed to the repository. There are no runtime components, API surfaces, or UI workflows to exercise. Artifact-driven UAT (read the files, verify structure and content quality, confirm test suites are unbroken) is the complete verification strategy.

## Preconditions

- All 9 brainstorm files are committed: `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` returns 9 files
- `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0
- `git log --oneline -3` shows both the brainstorm commit and the S01 completion commit

## Smoke Test

Open `SUMMARY.md` and confirm:
1. It has a pairs table listing all four explorer/challenger pairs
2. It has a "Downstream Inputs" section with S03, S05, and S06 subsections
3. It has a "Deferred Items Status" table with 5 rows (one per deferred item from March 15)
4. It has a "New Backlog Candidates" section with ≥10 entries

**Expected:** All four confirmed in a single file read.

## Test Cases

### 1. Count-quota downstream inputs are actionable for S03

1. `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md`
2. Locate the "Downstream inputs for S03" section
3. Read DI-1 through DI-6 and confirm each is a concrete, scoped design question that S03 can act on directly (not a vague observation)
4. Confirm DI-2 (tag non-exclusivity) is marked as the hardest open question with explicit reasoning
5. Confirm D040 is applied (Section 4 notes D040 locked and redirects rather than debating cross-kind conflict detection)
6. **Expected:** 6 downstream inputs present; each is a specific design question with framing; D040 applied without re-debate.

### 2. Testing vocabulary candidates are precise enough for S05

1. `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md`
2. Locate the "Vocabulary candidates for S05" table
3. Confirm ≥10 candidates are listed with ready/needs-work status
4. For any pattern previously described with "high-scoring" or ordering-dependent language, confirm the report either rejected it or replaced it with a parameterized, precision-specified form
5. Confirm the extension-methods placement verdict appears (BudgetUtilization and KindDiversity → core; ExcludedItemCount → testing-only)
6. **Expected:** ≥15 candidates in table; no unresolved "high-scoring" language; placement verdicts present.

### 3. Future features mandate lists cover all three S06 chapters

1. `grep "S06 must specify" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md`
2. Confirm three separate "S06 must specify" sections appear — one for DecayScorer, one for OTel, one for Budget Simulation
3. Read the DecayScorer section and confirm Rust TimeProvider trait shape is specified (minimal trait, fn now() → DateTime<Utc>)
4. Read the Budget Simulation section and confirm multi-budget DryRun is explicitly rejected
5. Confirm D042 and D043 are applied (TimeProvider mandatory, cupel.* namespace) without re-debate
6. **Expected:** 3 "S06 must specify" sections; ≥5 items each; locked decisions applied.

### 4. Deferred items have clear verdicts

1. `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md`
2. Locate each of the five deferred items: fork diagnostic, extension methods, IntentSpec, ProfiledPlacer, snapshot testing
3. Confirm each has a verdict from: M003 ready / M003+ defer / Drop / Reassigned
4. Confirm fork diagnostic is marked M003-ready with rationale (dry_run now live)
5. Confirm snapshot testing is M003+ defer with the concrete path described (tiebreak spec rule + GreedySlice default)
6. **Expected:** All 5 items have verdicts; fork diagnostic is M003-ready; no items are left as "maybe" or "needs more thought".

### 5. SUMMARY.md is a complete session synthesis

1. `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md`
2. Confirm session metadata section (date, pairs count, scope)
3. Confirm pairs table with all four pairs listed
4. Confirm Downstream Inputs section with S03/S05/S06 subsections — each subsection should match the pair reports, not introduce new content
5. Confirm Deferred Items Status table with 5 rows and March 15 → post-v1.2 verdict columns
6. Confirm New Backlog Candidates section with ≥10 M003+ items, each with a placement recommendation
7. **Expected:** All five sections present; downstream inputs are cross-references from pair reports (no new content); backlog candidates have actionable placements.

### 6. Regression guard: test suites pass

1. `cargo test --manifest-path crates/cupel/Cargo.toml`
2. `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`
3. **Expected:** cargo test exits 0 with 113 passed; dotnet test exits 0 with 583 passed.

## Edge Cases

### No TBD fields in any report file

1. `grep -r "TBD" .planning/brainstorms/2026-03-21T09-00-brainstorm/*.md`
2. **Expected:** No output (or any TBD occurrence is inside a quoted excerpt from a prior document, not an unresolved field in the report).

### Locked decisions not re-opened

1. `grep "D040\|D041\|D042\|D043" .planning/brainstorms/2026-03-21T09-00-brainstorm/*.md | grep -v "applied\|locked\|per\|confirms\|is"`
2. **Expected:** Any references to D040-D043 note them as locked/applied rather than debating them.

## Failure Signals

- Any brainstorm file missing from the directory (should be exactly 9)
- A report file that lacks a "Downstream inputs" section (downstream slice will reference non-existent design inputs)
- "high-scoring" or other undefined comparative terms surviving unchallenged in testing-vocabulary-report.md
- A deferred item from March 15 with no verdict or a "needs more thought" outcome
- Either test suite exiting non-zero
- `SUMMARY.md` introducing new design content rather than synthesizing the four pair reports

## Requirements Proved By This UAT

- **R045** — Proved in full. Fresh brainstorm session committed with all required artifacts: 9 files, 4 explorer/challenger pairs, SUMMARY.md with all five required sections, 5 deferred items re-evaluated with verdicts, new backlog candidates enumerated, test suites passing. The post-v1.2 perspective is captured: dry_run availability, diagnostics parity reality, and concrete ideas that only became feasible after v1.2 shipped.

## Not Proven By This UAT

- **R040 (count-quota design)** — S01 frames the design space and provides downstream inputs. R040 is not proved until S03 produces a design decision record with no remaining TBD fields.
- **R043 (Cupel.Testing vocabulary)** — S01 produces vocabulary candidates with ready/needs-work status. R043 is not proved until S05 produces the final spec chapter with ≥10 patterns fully specified.
- **R044 (future features spec chapters)** — S01 produces mandate lists for S06. R044 is not proved until S06 produces all three spec chapters (DecayScorer, OTel, budget simulation) and links them from SUMMARY.md.
- **Implementation correctness** — S01 is design documentation only. No runtime behavior is introduced or modified. No integration or operational verification applies.

## Notes for Tester

- The primary value of this UAT is assessing whether the downstream design inputs are **specific enough to act on**. A vague DI ("consider tag semantics") is a failure; a precise DI ("DI-2: specify whether an item matching two tags counts toward both tag quotas or only the first matched") is a pass.
- The SUMMARY.md is a synthesis document — it should not introduce new positions. If it contradicts a pair report without explanation, that is a failure signal.
- The BudgetUtilization/KindDiversity placement revision (core vs separate package) appears in SUMMARY.md S05 DI-5 and overrides the T02 pair report's earlier recommendation. This revision is intentional and should be noted during review.
- `rtk dotnet test` does not work with TUnit — use raw `dotnet test --project`. This is a pre-existing environmental issue, not a regression.
