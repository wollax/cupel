---
estimated_steps: 6
estimated_files: 3
---

# T04: Post-v1.2 ideas pair + deferred re-evaluation + SUMMARY.md + regression verify

**Slice:** S01 — Post-v1.2 Brainstorm Sprint
**Milestone:** M002

## Description

Close the brainstorm session with three deliverables:

1. **`post-v1-2-ideas.md` + `post-v1-2-report.md`** — The fourth explorer/challenger pair, covering ideas that only became concrete after v1.2 shipped (`SelectionReport` and `dry_run` live in both languages, `KnapsackSlice` OOM guard available). Also re-evaluates the five ideas explicitly deferred from the March 15 brainstorm with fresh context.

2. **`SUMMARY.md`** — Session-level synthesis following the March 15 SUMMARY.md format. Cross-references all four pairs, provides downstream-input subsections for S03/S05/S06 (the slices that depend on this brainstorm's output), lists updated verdicts for the five deferred items, and catalogs new M003+ backlog candidates.

3. **Regression verification** — Confirms that no accidental file changes introduced test regressions. This slice is documentation-only, but the verify step is mandatory per the milestone DoD.

The five deferred items to re-evaluate (from `S01-RESEARCH.md` "Deferred Ideas to Re-evaluate"):
1. Fork diagnostic (`ForkDiagnosticReport`) — with `dry_run` now live in both languages, is this M003 scope or still later?
2. `SelectionReport` extension methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`) — placement decision informed by T02 debate
3. `IntentSpec` as preset selector — "20 lines of code" — still not enough signal to act on?
4. `ProfiledPlacer` companion package — confirm still deferred to v1.3+; any new signal?
5. Snapshot testing in `Cupel.Testing` — `SelectionReport` ordering stability for ties still not guaranteed — confirm snapshot still blocked

## Steps

1. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md` in **explorer mode**:

   **Section A — Deferred items re-evaluation (one sub-section per item):**
   For each of the five deferred items, open with "What changed since March 15:" then generate fresh angles.
   - Fork diagnostic: `dry_run` is now concrete in both languages. Running N policy variants is ~N `dry_run` calls. What is the implementation path? Is this M003?
   - Extension methods: T02 produced a placement verdict. Echo it here and generate angles on whether they belong in `Wollax.Cupel` core, in `Wollax.Cupel.Analytics`, or in the Cupel.Testing vocabulary.
   - `IntentSpec` as preset selector: Still "20 lines of code"? What concrete use case would make it worth the API surface? Is there any post-v1.2 signal?
   - `ProfiledPlacer`: No new signal expected. Confirm still deferred and explain why v1.2 didn't change the analysis.
   - Snapshot testing: Is there any path to making `SelectionReport` ordering stable for ties? What would it take to unblock snapshots?

   **Section B — New post-v1.2 ideas (≥5 items):**
   Ideas only possible after v1.2 shipped. Cover at minimum:
   - `QuotaUtilization` metric: callers using `QuotaSlice` want to know how much of each kind's quota was actually used (required 20%, got 15%). Does this belong in `SelectionReport`, in extension methods, or in a Cupel.Testing assertion pattern?
   - Timestamp coverage as a context health signal: fraction of included items with timestamps. Where does it belong?
   - Count-quota exclusion diagnostic variants: `CountCapExceeded { kind, cap, count }` and `CountRequireUnmet { kind, require, actual }` as new `ExclusionReason` variants. (Feeds directly into S03's algorithm design.) Explorer: are there other count-quota exclusion reason variants needed?
   - Multi-budget `dry_run` API question: now that single-budget `dry_run` is stable, is there a case for `DryRun(items, budgets: int[])` as a batch variant?
   - Any other post-v1.2 angle the explorer believes deserves discussion.

2. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md` in **challenger mode**:
   - For deferred items: produce one of four verdicts per item: "M003 — ready to plan", "M003+ — defer further", "Drop — no longer viable", "Reassigned — belongs in [other slice]." Include a one-paragraph rationale for each.
   - For new post-v1.2 ideas: accept/reject with placement recommendation (which M002 slice feeds, or M003 backlog, or drop).
   - Close with a "New backlog candidates" section listing all items that should be tracked for M003+ with a one-line description of each.
   - Note any scope-creep risks (ideas that would expand M002's deliverables if acted on now — they must go only in "New backlog candidates," not in M002 slices).

3. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` following the March 15 SUMMARY.md structure:

   ```
   # Brainstorm Summary: Post-v1.2 Sprint

   **Session:** 2026-03-21T09-00
   **Pairs:** 4 (count-quota-angles, testing-vocabulary-angles, future-features-angles, post-v1-2-ideas)
   **Rounds:** 2 each
   **Total proposals:** N entered → M survived debate

   ---

   ## Downstream Inputs (for M002 slices)

   ### S03: Count-Based Quota Design
   [Summary of count-quota-report.md inputs — top 5 design questions]

   ### S05: Cupel.Testing Vocabulary Design
   [Summary of testing-vocabulary-report.md candidates — top 10 assertion patterns]

   ### S06: Future Features Spec Chapters
   [Summary of future-features-report.md "must specify" lists per feature]

   ---

   ## Deferred Items Status

   | Idea | March 15 Verdict | Post-v1.2 Verdict | Notes |
   ...

   ---

   ## New Backlog Candidates

   [Items from post-v1-2-report.md "New backlog candidates" section]

   ---

   ## Cross-Cutting Themes

   [3-5 themes that emerged across the four pairs]
   ```

   The SUMMARY.md must not introduce new content not present in the four pair reports — it is a synthesis and cross-reference, not a fifth brainstorm.

4. Run regression verification:
   ```
   rtk cargo test --manifest-path crates/cupel/Cargo.toml
   rtk dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
   ```
   Both must exit 0. If either fails, diagnose before committing — this slice makes no code changes, so any failure means a pre-existing issue or environment problem, not a regression introduced here.

5. Stage and commit all 9 brainstorm files:
   ```
   git add .planning/brainstorms/2026-03-21T09-00-brainstorm/
   git commit -m "docs(S01): add brainstorm 2026-03-21T09-00"
   ```

6. Update `.kata/STATE.md` to reflect S01 tasks completing.

## Must-Haves

- [ ] `post-v1-2-ideas.md` re-evaluates all five deferred items with "What changed since March 15" framing
- [ ] `post-v1-2-ideas.md` generates ≥5 new post-v1.2-specific ideas
- [ ] `post-v1-2-report.md` produces a verdict (M003/M003+/Drop/Reassigned) for each deferred item
- [ ] `post-v1-2-report.md` contains a "New backlog candidates" section
- [ ] `SUMMARY.md` contains all five sections: session metadata, pairs table, downstream inputs (S03/S05/S06), deferred-items status, new backlog candidates
- [ ] `SUMMARY.md` downstream-inputs sections are derived from the pair reports (not new content)
- [ ] `cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- [ ] `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0
- [ ] All 9 files committed in a single commit

## Verification

- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/ | wc -l` returns 9
- `grep -c "Downstream Inputs" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` ≥ 1
- `grep -c "New backlog candidates\|New Backlog Candidates" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` ≥ 1
- `grep -c "Deferred Items" .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` ≥ 1
- `rtk cargo test --manifest-path crates/cupel/Cargo.toml` exits 0
- `rtk dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` exits 0
- `git log --oneline -1` shows `docs(S01): add brainstorm 2026-03-21T09-00`

## Observability Impact

- Signals added/changed: None (documentation only; test-suite pass is a regression guard)
- How a future agent inspects this: `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` for the full session synthesis; individual report files for per-pair detail
- Failure state exposed: If `SUMMARY.md` is missing a downstream-inputs section, the S03/S05/S06 planning agents will not find their intended inputs in a predictable location. If either test suite fails, something environmental changed — diagnose before committing.

## Inputs

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — from T01 (completed)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/testing-vocabulary-report.md` — from T02 (completed)
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — from T03 (completed)
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/SUMMARY.md` — format template
- `S01-RESEARCH.md` — "Deferred Ideas to Re-evaluate" and "New Ideas to Explore" sections
- `.kata/DECISIONS.md` — for scope-guard: no new decisions introduced in SUMMARY.md

## Expected Output

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-ideas.md` — fifth-pair explorer document with deferred-item re-evaluation and ≥5 new post-v1.2 ideas
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/post-v1-2-report.md` — fifth-pair challenger report with verdicts and "New backlog candidates" section
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/SUMMARY.md` — session synthesis with all five required sections
- `git log` — one commit `docs(S01): add brainstorm 2026-03-21T09-00` containing all 9 files
