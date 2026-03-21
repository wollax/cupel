---
estimated_steps: 4
estimated_files: 2
---

# T03: Future features angles pair

**Slice:** S01 — Post-v1.2 Brainstorm Sprint
**Milestone:** M002

## Description

Run the third explorer/challenger pair targeting fresh angles on the three S06 spec chapters: `DecayScorer`, OpenTelemetry verbosity, and budget simulation. The March 15 highvalue-report Decisions 3, 4, and 5 established the baseline for these features, but that session predated four important v1.2 completions: Rust `dry_run` and `SelectionReport` are now live in both languages, `KnapsackSlice` OOM guard is concrete, and the `ContextItem.metadata` and `.timestamp` fields are both stable and tested.

The post-v1.2 changes create concrete design questions that were hypothetical in March:
- **Rust `TimeProvider`** — the Rust crate uses `chrono::DateTime<Utc>`. A minimal `trait TimeProvider { fn now(&self) -> DateTime<Utc>; }` is the idiomatic Rust equivalent. What is the concrete design? D042 locks that TimeProvider must be mandatory (no silent default), but the Rust trait shape needs to be worked out.
- **`dry_run` multi-budget variant** — `FindMinBudgetFor` requires ~10-15 sequential `DryRun` calls. Now that `dry_run` is live in both languages, is a multi-budget variant (`DryRun(items, budgets[])`) worth specifying, or is the single-budget version sufficient for the binary search loop?
- **Timestamp coverage metric** — with `ContextItem.timestamp` live, a "what fraction of included items have timestamps?" signal is concrete. Does it belong in the DecayScorer spec chapter (as a note about what happens when no items have timestamps) or as a standalone `SelectionReport` metric?

**Locked decisions (treat as given):**
- D042: `TimeProvider` injection is mandatory; no silent default to system time. Do not re-debate.
- D043: `cupel.*` OTel namespace; do not chase `gen_ai.*`. Do not re-debate.

## Steps

1. Read the following sources before writing:
   - `crates/cupel/src/scorer/recency.rs` — `RecencyScorer` is rank-based (ordinal position, not time magnitude). Understand this contrast point — DecayScorer computes absolute age-based scores, not ordinal ranks.
   - `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decisions 3 (OTel verbosity), 4 (budget simulation), 5 (DecayScorer) for the full baseline including all scoping constraints already applied.
   - `.kata/DECISIONS.md` — D042 and D043 locked.

2. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` in **explorer mode** with three labeled sections:

   **Section 1 — DecayScorer angles (≥5 proposals):**
   - Rust `TimeProvider` trait design: `trait TimeProvider: Send + Sync { fn now(&self) -> DateTime<Utc>; }` with a `SystemTimeProvider` struct wrapping `Utc::now()`. Explore whether the system default should be a zero-sized type or a regular struct.
   - `Window(maxAge)` binary behavior vs a linear-falloff variant: does the spec need to support `Window(maxAge, falloff: Linear)` or is binary 1.0/0.0 sufficient?
   - Negative age (future-dated items, `referenceTime < item.timestamp`): clamp to 0.0 age, or treat as a score of 1.0, or configurable?
   - Timestamp coverage note: spec chapter mentions what happens when no included items have timestamps (DecayScorer scores all items equally based on `nullTimestampScore`). Does this need a runtime warning via `ITraceCollector`?
   - Zero half-life edge case in `Exponential(halfLife: TimeSpan.Zero)`: throw at construction or return 0.0 for all non-zero ages?

   **Section 2 — OpenTelemetry verbosity angles (≥5 proposals):**
   - Propose exact `cupel.*` attribute names for `StageOnly` tier: `cupel.stage.name` (string), `cupel.stage.duration_ms` (float64), `cupel.stage.item_count` (int).
   - Propose exact attributes added at `StageAndExclusions` tier: `cupel.exclusion.reason` (string, per-excluded-item event), `cupel.exclusion.item_count` (int on stage event).
   - Propose exact attributes added at `Full` tier: `cupel.item.kind` (per-included-item event), `cupel.item.tokens` (int, per-item event), `cupel.item.score` (float64).
   - Cardinality warning: at what verbosity tier does per-item event emission become a cardinality risk? How should the spec document this?
   - Budget attribute: where does `cupel.budget.max_tokens` (int) appear? On all tiers or only `StageOnly`+?

   **Section 3 — Budget simulation angles (≥5 proposals):**
   - Multi-budget `DryRun`: `FindMinBudgetFor` calls `DryRun(items, budget)` ~10-15 times. Is there value in specifying a `DryRun(items, budgets: int[])` variant that batches the DP work? Or does this over-engineer the API?
   - `GetMarginalItems` precision: what exactly is the "diff"? Items in the primary result but not in `DryRun(items, budget - slackTokens)` result? Or the reverse? Specify the direction of comparison.
   - `FindMinBudgetFor` search bounds: what is the lower bound of the binary search? 1 token? `targetItem.Tokens`? Document the precondition.
   - Monotonicity violation at runtime (not just QuotaSlice): are there other slicers that produce non-monotonic inclusion? What about a custom slicer? Should the guard be `QuotaSlice`-specific or a general monotonicity contract?
   - `FindMinBudgetFor` return type when no budget in `[1, searchCeiling]` includes the target item: throw? return null? return `int.MaxValue`?

3. Write `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` in **challenger mode** with three sections matching the ideas sections:
   - For each proposal: challenge it, debate the tradeoffs, then reach a verdict.
   - Apply D042 (mandatory TimeProvider) and D043 (cupel.* namespace) as locked — redirect to what S06 must specify *within* those constraints.
   - For each feature, close with two subsections: "S06 must specify" (items with clear consensus) and "S06 should consider" (open questions that require a judgment call but are not blocking).
   - Note any items that are implementation details rather than spec concerns — label these "implementation (S06 decides)."

4. Do not commit yet — that happens in T04.

## Must-Haves

- [ ] `ideas.md` has three labeled sections (DecayScorer, OTel, Budget Simulation) with ≥5 raw proposals each
- [ ] `ideas.md` covers Rust `TimeProvider` trait design as a distinct angle
- [ ] `ideas.md` addresses the `dry_run` multi-budget question explicitly
- [ ] `report.md` applies D042 and D043 as locked without re-debating them
- [ ] `report.md` has "S06 must specify" and "S06 should consider" subsections for each of the three features
- [ ] Neither file conflates spec concerns with implementation details

## Verification

- `ls .planning/brainstorms/2026-03-21T09-00-brainstorm/` shows `future-features-ideas.md` and `future-features-report.md`
- `wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` ≥ 80
- `wc -l .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` ≥ 100
- `grep -c "S06 must specify" .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` ≥ 3

## Observability Impact

- Signals added/changed: None (documentation only)
- How a future agent inspects this: `cat .planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md`; grep for "S06 must specify" to find each feature's mandate list
- Failure state exposed: If `report.md` does not have the three "must specify" sections, S06 planning will lack the debate outputs that distinguish non-negotiable spec requirements from optional considerations

## Inputs

- `crates/cupel/src/scorer/recency.rs` — rank-based contrast for DecayScorer framing
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — Decisions 3, 4, 5 for full baseline
- `.kata/DECISIONS.md` — D042 and D043 locked
- `S01-RESEARCH.md` — "New Ideas to Explore" section for Rust TimeProvider, multi-budget dry_run, timestamp-coverage metric

## Expected Output

- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` — three-section explorer document with ≥5 raw proposals per feature, covering post-v1.2-specific angles
- `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md` — three-section challenger report with "must specify" / "should consider" / "implementation" classification per feature
