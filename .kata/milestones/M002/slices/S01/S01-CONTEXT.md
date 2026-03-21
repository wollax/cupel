---
id: S01
milestone: M002
status: ready
---

# S01: Post-v1.2 Brainstorm Sprint — Context

## Goal

Run four fully agent-driven explorer/challenger brainstorm pairs against the post-v1.2 codebase, producing a committed brainstorm directory with eight pair files and a SUMMARY.md that catalogues fresh ideas and delivers clear verdicts on all five deferred items.

## Why this Slice

S03 (count-quota design) and S05 (Cupel.Testing vocabulary) cannot start without fresh brainstorm inputs — their dependency is listed explicitly in the roadmap. The last brainstorm (March 15) predates `SelectionReport`, `run_traced`, `dry_run`, and the KnapsackSlice OOM guard. With those shipped, the design landscape has shifted enough to warrant a new session before locking down S03 and S05 designs.

## Scope

### In Scope

- Four explorer/challenger pairs, each producing `ideas.md` + `report.md`:
  1. `count-quota-angles` — surfaces fresh angles on the 5 open count-quota design questions for S03
  2. `testing-vocabulary-angles` — surfaces assertion pattern candidates for S05's ≥10 named patterns
  3. `future-features-angles` — surfaces angles on DecayScorer curves, OTel verbosity, and budget simulation for S06
  4. `post-v1-2-ideas` — new ideas only possible after SelectionReport and dry_run shipped; not covered by the other pairs
- One `SUMMARY.md` aggregating all pair outputs, with a "New backlog candidates" section for M003+ ideas
- Clear verdict + rationale for each of the five deferred ideas:
  - `ForkDiagnosticReport` — M003 scope or still deferred?
  - `SelectionReport` extension methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`) — S05 vocabulary, standalone analytics, or M003?
  - `IntentSpec` as preset selector — still not enough signal?
  - `ProfiledPlacer` companion package — confirm still deferred to v1.3+?
  - Snapshot testing in `Cupel.Testing` — confirm still blocked on ordering stability?
- Commit the brainstorm directory to `.planning/brainstorms/2026-03-21T<time>-brainstorm/`

### Out of Scope

- Implementation plans, pseudocode, or code decisions — those belong in S03/S05/S06
- Expanding M002 scope — all new ideas go to SUMMARY.md as M003+ backlog candidates, even if they are small and design-only (D039 is locked)
- Quality issue triage — S01 is not a quality session; do not surface quality issues as brainstorm output
- Re-litigating closed decisions: D040 (count-quota conflict detection), D041 (no FluentAssertions, no snapshots), D042 (TimeProvider mandatory), D043 (cupel.* OTel namespace) are locked
- Smelt/Assay territory: `SweepBudget`, Context Protocol, and Inverse Synthesis — already rejected in March 15 brainstorm; do not re-litigate

## Constraints

- Agent-driven: the agent plays both explorer and challenger voices autonomously; no collaborative dialogue required
- Deferred items must each receive a clear verdict (M003 scope / still deferred / confirmed dead) with reasoning — not just surfaced angles
- New ideas from the `post-v1-2-ideas` pair and other pairs stay as backlog candidates only — no M002 scope expansion
- Follow the established brainstorm file format from `.planning/brainstorms/2026-03-15T12-23-brainstorm/` — parsers downstream may depend on the structure
- Output directory name: today's UTC datetime prefix, e.g. `2026-03-21T<HH-MM>-brainstorm/`
- 2 debate rounds per pair (same as prior sessions)

## Integration Points

### Consumes

- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decision 6 (count-quota 5 open questions, "explicitly deferred" framing)
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/radical-report.md` — ForkDiagnosticReport (Surviving Proposal 3) and metadata convention proposal
- `spec/src/diagnostics/selection-report.md` — precise field semantics for SelectionReport; feeds testing vocabulary precision
- `spec/src/slicers/quota.md` — DISTRIBUTE-BUDGET pseudocode; baseline for count-quota pair
- `crates/cupel/src/scorer/recency.rs` — RecencyScorer rank-based implementation; contrast point for DecayScorer pair
- `crates/cupel/src/diagnostics/mod.rs` — live Rust SelectionReport and ExclusionReason variants
- `.kata/DECISIONS.md` — D039–D043 lock scope and key API decisions; brainstorm treats these as given

### Produces

- `.planning/brainstorms/2026-03-21T<time>-brainstorm/count-quota-angles-ideas.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/count-quota-angles-report.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/testing-vocabulary-angles-ideas.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/testing-vocabulary-angles-report.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/future-features-angles-ideas.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/future-features-angles-report.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/post-v1-2-ideas-ideas.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/post-v1-2-ideas-report.md`
- `.planning/brainstorms/2026-03-21T<time>-brainstorm/SUMMARY.md` — aggregated summary with "New backlog candidates" section and deferred-item verdicts

## Open Questions

- **ForkDiagnosticReport verdict** — With `dry_run` live in both languages, the implementation path is clearer. Working assumption: promote to M003 scope since the building blocks now exist. To be confirmed in the `post-v1-2-ideas` pair.
- **SelectionReport extension methods placement** — `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` were reshaped from `ContextPressure` to extension methods in March 15 but not slotted into any M002 slice. Working assumption: surface as candidates for S05 vocabulary (testing-focused) and separately as M003 analytics candidates. The `testing-vocabulary-angles` pair should weigh in.
- **Rust TimeProvider pattern concreteness** — The research proposes `trait TimeProvider { fn now(&self) -> DateTime<Utc>; }` with a `SystemTimeProvider` struct wrapping `chrono::Utc::now()`. This is a design question for the `future-features-angles` pair and feeds S06's Rust design note.
