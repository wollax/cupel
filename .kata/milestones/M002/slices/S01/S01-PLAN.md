# S01: Post-v1.2 Brainstorm Sprint

**Goal:** Run four explorer/challenger brainstorm pairs against the v1.2 codebase state, re-evaluate five deferred ideas from March 15, and commit a fresh brainstorm session to `.planning/brainstorms/2026-03-21T09-00-brainstorm/`.
**Demo:** `.planning/brainstorms/2026-03-21T09-00-brainstorm/` contains 9 committed files (4 × `ideas.md` + 4 × `report.md` + `SUMMARY.md`). `SUMMARY.md` has downstream inputs for S03, S05, and S06 and a "New backlog candidates" section for M003+. `cargo test` and `dotnet test` still pass.

## Must-Haves

- All four explorer/challenger pairs written: `count-quota-angles`, `testing-vocabulary-angles`, `future-features-angles`, `post-v1-2-ideas`
- Each pair has an `ideas.md` (raw, uncensored proposals) and a `report.md` (debated, scoped, accept/reject decisions)
- `SUMMARY.md` cross-references all four pairs, provides downstream-input subsections for S03/S05/S06, lists deferred-item verdicts, and enumerates new M003+ backlog candidates
- Five deferred ideas from March 15 explicitly re-evaluated with post-v1.2 context: fork diagnostic, `SelectionReport` extension methods, `IntentSpec` preset selector, `ProfiledPlacer`, snapshot testing
- Post-v1.2-specific angles covered: count-quota exclusion diagnostic variants, Rust `TimeProvider` trait design, `dry_run` multi-budget question, `QuotaUtilization` metric, timestamp-coverage metric
- Locked decisions D039–D043 treated as given — no re-debating resolved questions
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes with no new failures
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes with no new failures

## Proof Level

- This slice proves: contract (written design artifacts committed to the repo)
- Real runtime required: no (test-suite pass is a regression guard, not behavioral verification)
- Human/UAT required: yes — human review of brainstorm output for clarity and M003+ backlog candidates before S03/S05/S06 planning begins

## Verification

- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` lists exactly 9 files (8 pair files + SUMMARY.md)
- `grep -c "Downstream inputs" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` returns ≥ 1
- `grep -c "New backlog candidates" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` returns ≥ 1
- `rtk cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- `rtk dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0
- `git log --oneline -1` shows commit `docs(S01): add brainstorm 2026-03-21T09-00`

## Observability / Diagnostics

This is a documentation-only slice — no runtime components are modified.

- Runtime signals: none
- Inspection surfaces: `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` verifies file count; `grep -r "TBD" .planning/brainstorms/2026-03-21T09-00-brainstorm/*.md` detects incomplete debates
- Failure visibility: if a report file is missing its "Downstream inputs" section, the downstream slice plan will reference non-existent design inputs; catch with grep during verification
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `.planning/brainstorms/2026-03-15T12-23-brainstorm/` (format template and prior decisions), `.kata/DECISIONS.md` (D039–D043), `S01-RESEARCH.md` (angles catalogue and deferred-items list), `spec/src/slicers/quota.md`, `spec/src/diagnostics/selection-report.md`, `crates/cupel/src/slicer/quota.rs`, `crates/cupel/src/scorer/recency.rs`
- New wiring introduced: none (pure documentation; no source files touched)
- What remains before the milestone is truly usable end-to-end: S03 must consume `count-quota-report.md` for design inputs; S05 must consume `testing-vocabulary-report.md` for vocabulary candidates; S06 must consume `future-features-report.md` for spec chapter angles

## Tasks

- [x] **T01: Count-quota angles pair** `est:1h`
  - Why: S03 carries the highest design risk in M002 — five open questions about count-quota algorithm, tag non-exclusivity, pinned interaction, conflict detection, and KnapsackSlice compatibility. The brainstorm must frame the design space precisely before S03 attempts resolution, or S03 will hit the same dead ends as the March 15 session.
  - Files: `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md`, `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md`
  - Do: Create brainstorm directory. Read `highvalue-report.md` Decision 6 (5 open questions framing) and `spec/src/slicers/quota.md` (DISTRIBUTE-BUDGET baseline) and `crates/cupel/src/slicer/quota.rs`. Write `count-quota-ideas.md` in explorer mode (≥10 raw proposals, one per open question plus post-v1.2 angles on exclusion diagnostic variants). Write `count-quota-report.md` in challenger mode (debate each proposal, scope it, accept/reject; output distilled design inputs for S03; treat D040 as locked).
  - Verify: Both files exist and exceed 80 lines each; `report.md` contains a "Downstream inputs for S03" section; no locked decision (D040) is reopened
  - Done when: Two files in the brainstorm directory; `count-quota-report.md` enumerates ≥5 scoped design inputs that S03 can act on directly

- [x] **T02: Testing vocabulary angles pair** `est:1h`
  - Why: S05 must define ≥10 assertion patterns with precise specs. Without a prior debate about vocabulary precision — ordering dependencies, tolerance, error message format — S05 will produce an API surface that requires breaking changes on first real use.
  - Files: `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md`, `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md`
  - Do: Read `spec/src/diagnostics/selection-report.md` for exact field semantics (Excluded: score-desc, insertion-order tiebreak; Included: final placed order). Read `highvalue-report.md` Decision 2 for vocabulary baseline. Write `testing-vocabulary-ideas.md` in explorer mode (≥15 assertion pattern proposals covering item inclusion/exclusion, kind coverage, budget utilization, placement, exclusion reasons, diversity; also cover `SelectionReport` extension-methods re-evaluation from Decision 4). Write `testing-vocabulary-report.md` in challenger mode (debate each pattern for precision: does it depend on unstable ordering? what tolerance is needed? what error message format?). Treat D041 (no FA, no snapshots) as locked.
  - Verify: Both files exist; `report.md` identifies ≥10 strong vocabulary candidates; no ordering-dependent pattern survives without an explicit stability note
  - Done when: Two files committed; `testing-vocabulary-report.md` contains ≥10 scoped assertion candidates with per-pattern precision notes for S05

- [x] **T03: Future features angles pair** `est:1h`
  - Why: S06 writes three spec chapters (DecayScorer, OTel, budget simulation). Without fresh debate specifically incorporating post-v1.2 changes (Rust `dry_run`, both-language `SelectionReport`), S06 will write spec chapters against a pre-v1.2 mental model.
  - Files: `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md`, `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md`
  - Do: Read `crates/cupel/src/scorer/recency.rs` (rank-based contrast for DecayScorer). Read `highvalue-report.md` Decisions 3, 4, 5 (OTel, budget sim, DecayScorer baselines). Write `future-features-ideas.md` in explorer mode — three topic sections (DecayScorer / OTel / Budget Simulation), ≥5 raw angles each; cover post-v1.2 specifics: Rust `TimeProvider` trait design, `Window(maxAge)` linear-falloff option, exact `cupel.*` attribute names per OTel tier, `DryRun(items, budgets[])` multi-budget variant question, timestamp-coverage metric. Write `future-features-report.md` in challenger mode — per feature: "S06 must specify" / "S06 should consider" / "rejected" sections. Treat D042 (TimeProvider mandatory) and D043 (`cupel.*` namespace) as locked.
  - Verify: Both files exist; `report.md` has three feature sections each with a "must specify" list; no locked decision (D042, D043) is reopened
  - Done when: Two files committed; `future-features-report.md` non-empty "must specify" lists for all three S06 chapters

- [x] **T04: Post-v1.2 ideas pair + deferred re-evaluation + SUMMARY.md + regression verify** `est:1h`
  - Why: Closes the session with ideas that only became concrete after v1.2 shipped; re-evaluates five deferred items with updated information; synthesizes all four pairs into the SUMMARY.md that downstream slices will reference; confirms no regressions from any accidental file changes.
  - Files: `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md`, `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md`, `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md`
  - Do: Write `post-v1-2-ideas.md` in explorer mode — re-evaluate 5 deferred items (fork diagnostic, extension methods, IntentSpec, ProfiledPlacer, snapshot testing) each with a "what changed since March 15" framing; generate ≥5 new post-v1.2 ideas (QuotaUtilization metric, timestamp-coverage health signal, count-quota exclusion diagnostic variants, multi-budget `dry_run` API question, cross-language assertion vocabulary parity). Write `post-v1-2-report.md` in challenger mode — for deferred items: verdict (M003 / M003+ deferred / drop) with rationale; for new ideas: accept/reject with placement recommendation. Write `SUMMARY.md` following March 15 SUMMARY.md structure: session metadata, 4-pair summary table, downstream-inputs section (S03/S05/S06 subsections), deferred-items-status table (5 items × new verdict), new-backlog-candidates section, cross-cutting themes. Run `rtk cargo test --manifest-path crates/cupel/Cargo.toml` and `rtk dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`. Commit all 9 brainstorm files: `docs(S01): add brainstorm 2026-03-21T09-00`.
  - Verify: `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` shows 9 files; both test commands exit 0; `git log --oneline -1` shows the commit
  - Done when: All 9 files committed, both test suites green, SUMMARY.md contains all five required sections (session metadata, pair table, downstream inputs, deferred-items status, new backlog candidates)

## Files Likely Touched

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-ideas.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md` (created)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` (created)
