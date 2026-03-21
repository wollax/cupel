---
estimated_steps: 5
estimated_files: 3
---

# T01: Count-quota angles pair

**Slice:** S01 — Post-v1.2 Brainstorm Sprint
**Milestone:** M002

## Description

Run the first explorer/challenger pair targeting count-based quota design angles. The March 15 brainstorm explicitly deferred the five open design questions (algorithm integration, tag non-exclusivity, pinned interaction, conflict detection, KnapsackSlice compatibility) because "the interaction with pinned items and token budgets not fully designed." With `KnapsackSlice` OOM guard now live in both languages and `SelectionReport` available in Rust, the design space has concrete grounding that was missing before.

The explorer generates raw, uncensored angles on all five open questions plus the new post-v1.2 specific angle: what should count-quota exclusion diagnostics look like in `SelectionReport` (new `ExclusionReason` variants). The challenger then debates each angle for precision, scopes the design question, and produces a distilled "downstream inputs for S03" section.

**Scope boundary:** S01 surfaces design questions and angles — it does not resolve them. The output of this task is a well-framed set of inputs for S03, not a decision record. If the debate surfaces more questions than answers, that is valid and expected output.

**Locked decisions (treat as given):**
- D040: Build-time vs run-time conflict detection is a hard requirement (build-time for `RequireCount(2).CapCount(1)`, run-time for candidate scarcity). Do not re-debate.

## Steps

1. Create the brainstorm session directory: `mkdir -p .planning/brainstorms/2026-03-21T09-00-brainstorm/`

2. Read the following sources before writing:
   - `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decision 6 frames the 5 open questions; quote directly in `ideas.md`
   - `spec/src/slicers/quota.md` — `DISTRIBUTE-BUDGET` pseudocode; this is the baseline the count-quota algorithm must integrate with or replace
   - `crates/cupel/src/slicer/quota.rs` — live `QuotaEntry { require: f64, cap: f64 }` implementation; understand what a count-based analog would look like

3. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md` in **explorer mode**:
   - Do not self-censor. Generate ≥10 raw proposals.
   - Structure: one section per open question (five sections from Decision 6), plus one section for post-v1.2-specific angles (exclusion diagnostic variants: `CountCapExceeded { kind, cap, count }` and `CountRequireUnmet { kind, require, actual }` as new `ExclusionReason` variants; implications for `SelectionReport.Excluded` sorting with count-quota items).
   - For tag non-exclusivity: explore both "count toward all matching tags" (non-exclusive) and "count toward only the first matching tag" (exclusive) semantics, plus "count toward all with a dedup guard" variant.
   - For KnapsackSlice compatibility: explore preprocessing-step path (satisfy count constraints first, mark required items as pinned, then run standard knapsack) vs constrained-knapsack variant (incorporate count constraints into the DP objective directly).
   - Do not filter for locked decisions — include everything in explorer mode.

4. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` in **challenger mode**:
   - For each proposal from `ideas.md`: state the challenge (what breaks, what's imprecise, what's an implementation detail not a design question), the response (how the explorer would defend it), and the verdict (accepted as design input / reshaped / rejected as implementation detail).
   - Apply D040 as locked: any proposal that re-debates build-time vs run-time conflict detection is out of scope — note this explicitly with "D040 locked" and redirect to what S03 should specify *within* that constraint.
   - Close with a "Downstream inputs for S03" section containing the ≥5 most important, precisely-framed design questions that S03 must answer.
   - Mark which question the challenger considers hardest (likely tag non-exclusivity or KnapsackSlice path — document why).

5. Do not commit yet — that happens in T04.

## Must-Haves

- [ ] Brainstorm directory `.planning/brainstorms/2026-03-21T09-00-brainstorm/` exists
- [ ] `count-quota-ideas.md` covers all five open questions from Decision 6 plus post-v1.2 exclusion diagnostic angle
- [ ] `count-quota-ideas.md` does not pre-filter by locked decisions (explorer mode is uncensored)
- [ ] `count-quota-report.md` applies D040 explicitly and does not reopen that debate
- [ ] `count-quota-report.md` contains a "Downstream inputs for S03" section with ≥5 scoped design inputs
- [ ] Neither file contains "TBD" without an explanation of what S03 must determine

## Verification

- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` shows at least `count-quota-ideas.md` and `count-quota-report.md`
- `wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md` ≥ 80
- `wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` ≥ 80
- `grep -c "Downstream inputs for S03" .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` ≥ 1

## Observability Impact

- Signals added/changed: None (documentation only)
- How a future agent inspects this: `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` to see the design inputs; grep for "Downstream inputs" to find the summary section
- Failure state exposed: If `ideas.md` covers fewer than 6 topic areas or `report.md` lacks the "Downstream inputs" section, S03 planning will start without the intended design framing

## Inputs

- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decision 6 framing of the 5 open questions (explicit quote recommended)
- `spec/src/slicers/quota.md` — `DISTRIBUTE-BUDGET` pseudocode baseline
- `crates/cupel/src/slicer/quota.rs` — live percentage-based implementation for structural contrast
- `.kata/DECISIONS.md` — D040 must be treated as locked
- `S01-RESEARCH.md` — angles catalogue for count-quota; "New Ideas to Explore" section for exclusion diagnostic variants

## Expected Output

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-ideas.md` — ≥10 raw explorer proposals across all 5 open questions plus post-v1.2 angles
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/count-quota-report.md` — debated, scoped report; "Downstream inputs for S03" section with ≥5 precisely-framed design questions; hardest question identified
