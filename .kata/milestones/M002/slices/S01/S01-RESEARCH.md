# S01: Post-v1.2 Brainstorm Sprint — Research

**Date:** 2026-03-21

## Summary

S01 is a focused brainstorm sprint against the post-v1.2 codebase. The last brainstorm (March 15) predated four significant v1.2 deliverables: `SelectionReport` and `run_traced` / `dry_run` live in both .NET and Rust, the KnapsackSlice OOM guard, and the full .NET quality hardening pass. These are not incremental improvements — they substantially change the design landscape for three of S01's downstream slices.

The brainstorm should use the **explorer/challenger format** with three pairs targeting the three downstream slices (S03: count-quota design inputs, S05: testing vocabulary inputs, S06: future feature spec inputs). Each pair runs 2 rounds. Output lands in `.planning/brainstorms/2026-03-21T<time>-brainstorm/` as `SUMMARY.md` plus per-pair idea and report files.

**Primary recommendation:** Run pairs in this order of descending risk: count-quota angles first (feeds the hardest S03 problem), then testing vocabulary (feeds S05's precision requirements), then DecayScorer/OTel/budget-sim angles (feeds S06). A fourth wildcard pair should cover new post-v1.2 ideas not previously in the backlog.

## Recommendation

Run four explorer/challenger pairs producing eight files + one SUMMARY.md:
1. `count-quota-angles` — feeds S03's 5 open design questions
2. `testing-vocabulary-angles` — feeds S05's ≥10 named assertion patterns
3. `future-features-angles` — feeds S06's DecayScorer/OTel/budget-sim spec chapters
4. `post-v1-2-ideas` — new ideas that only became concrete after `SelectionReport` and `dry_run` shipped

Stay disciplined on scope: S01 must not grow into a planning session. Output is design inputs and catalogued ideas — not commitments.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Brainstorm format | `.planning/brainstorms/2026-03-15T12-23-brainstorm/` | Established SUMMARY.md + per-pair ideas/report pattern; parsers downstream may depend on structure |
| Count-quota context | March 15 highvalue-report.md Decision 6 | Contains the 5 open questions and the "explicitly deferred" framing; quote from it |
| Fork diagnostic concept | March 15 radical-report.md Surviving Proposal 3 | Already scoped and challenged; re-evaluate not re-derive |
| SelectionReport field reference | `spec/src/diagnostics/selection-report.md` | Precise field names, types, ordering semantics needed for vocabulary assertions |
| QuotaSlice algorithm | `spec/src/slicers/quota.md` | DISTRIBUTE-BUDGET pseudocode; understanding this is prerequisite for count-quota pair |

## Existing Code and Patterns

- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport`, `IncludedItem`, `ExcludedItem`, `TraceEvent`, `PipelineStage` all exported; Rust `dry_run` returns `SelectionReport` directly (`Result<SelectionReport, CupelError>`)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — .NET `SelectionReport` record: `Events`, `Included`, `Excluded`, `TotalCandidates`, `TotalTokensConsidered`
- `crates/cupel/src/scorer/recency.rs` — `RecencyScorer` is rank-based (ordinal), not decay-based; explicit contrast point for DecayScorer pair
- `crates/cupel/src/model/context_item.rs` — `ContextItem` has `metadata: HashMap<String, String>` and `timestamp: Option<DateTime<Utc>>`; both are live and tested
- `crates/cupel/src/slicer/quota.rs` — Percentage-based `QuotaSlice` implementation; starting point for count-quota design pair; `QuotaEntry` holds `require: f64` and `cap: f64` as percentages
- `src/Wollax.Cupel/CupelPipeline.cs` — `.DryRun(items)` calls `ExecuteCore` with a `DiagnosticTraceCollector`; returns `ContextResult` with `.Report` property
- `crates/cupel/src/pipeline/mod.rs` — Rust `.dry_run(items, budget)` returns `Result<SelectionReport, CupelError>` directly; discards `Vec<ContextItem>` (D024)

## Constraints

- S01 output is **design inputs** — idea catalogues and refined questions. No implementation plans, no pseudocode, no code decisions. Those belong in the downstream slices they feed.
- Explorer/challenger format: each pair generates an `ideas.md` (raw proposals) and a `report.md` (debated, scoped, rejected/accepted decisions). Both files must be committed.
- Scope guard: any idea that is purely an implementation concern (no design question, no spec ambiguity, no API shape question) does not belong in S01 output. File it as a future issue instead.
- No new milestone scope: S01 must not surface ideas that expand M002's deliverables. New ideas go into the brainstorm SUMMARY.md under a "New backlog candidates" section for M003+ consideration.
- `.planning/brainstorms/<date>-brainstorm/` is the output directory. Use today's UTC datetime as the prefix.

## Common Pitfalls

- **Conflating design questions with implementation questions** — "What data structure should count-quota use internally?" is implementation. "Does `RequireCount(2)` on a tag count toward all matching tags or only the first?" is design. S01 output should be the latter.
- **Over-specifying DecayScorer during brainstorm** — S06 will write the spec chapter; S01 only needs to surface fresh angles (e.g. "should `Window(maxAge)` have a linear falloff option?") not fully specify the curve API.
- **Re-debating already-resolved decisions** — D040 (count-quota build-time vs run-time conflict detection), D041 (no FluentAssertions, no snapshots), D042 (TimeProvider mandatory), D043 (cupel.* OTel namespace) are locked. The brainstorm should treat these as given and explore what's still open.
- **Scope creep into Smelt territory** — `SweepBudget`, Context Protocol, and Inverse Synthesis were already rejected in March 15 as Smelt/Assay concerns. Do not re-litigate.

## Open Risks

- **Count-quota tag non-exclusivity may require multiple debate rounds** — The March 15 brainstorm identified this as the hardest sub-problem. Even within S01's brainstorm scope (surfacing angles, not resolving), the pair may generate more questions than answers. That is a valid S01 output — document the questions precisely.
- **Cupel.Testing vocabulary precision depends on SelectionReport semantics** — `Excluded` is sorted score-descending with insertion-order tiebreak. `Included` is in final placed order. Assertions that depend on ordering (e.g. `PlaceItemAtEdge`) need to be specified against these exact semantics, not against intuition.
- **Fork diagnostic re-evaluation** — The March 15 radical report approved `ForkDiagnosticReport` as a `small-medium` dev-time tool but it was not included in any M002 slice. With `dry_run` now live in both languages, the implementation path is clearer. S01 should explicitly re-evaluate whether this belongs in M003 or stays deferred.
- **Post-v1.2 SelectionReport extension methods** — `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` were reshaped in the March 15 debate from `ContextPressure` type to extension methods, but not slotted into any M002 slice. They may belong in the Cupel.Testing vocabulary (S05) or as standalone analytics (M003). S01 should clarify which.

## Pre-brainstorm Context: What Changed Since March 15

The following table captures what v1.2 shipped that directly affects brainstorm scope:

| Area | Pre-v1.2 state | Post-v1.2 state | Impact on brainstorm |
|------|----------------|-----------------|----------------------|
| Rust diagnostics | Not shipped | `SelectionReport`, `run_traced`, `dry_run` live | DecayScorer Rust `TimeProvider` is now a concrete design question (not hypothetical) |
| `.DryRun()` (.NET) | Shipped in v1.1 | Stable | Budget simulation pair can assume DryRun stability |
| `KnapsackSlice` OOM guard | Not shipped | `CupelError::TableTooLarge` live in both | Count-quota + KnapsackSlice compatibility pair starts from a complete knapsack baseline |
| `SelectionReport` fields | .NET only | Both languages | Testing vocabulary pair can now propose Rust assertions |
| `ContextItem.metadata` | Live | Live | Metadata convention system (S04) is concrete; no M002 brainstorm work needed |
| Quality hardening | ~90 open issues | ~90 still open (new batch added) | S01 is NOT a quality triage session; do not surface quality issues as brainstorm output |

## Deferred Ideas to Re-evaluate in S01

These were explicitly deferred in prior brainstorms and should be re-evaluated with fresh eyes:

| Idea | Where deferred | Re-evaluation question |
|------|---------------|------------------------|
| Fork diagnostic (`ForkDiagnosticReport`) | March 15 radical-report.md surviving proposal 3 | With `dry_run` live, is this M003 scope or still later? What spec chapter does it need? |
| `SelectionReport` extension methods (`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`) | March 15 highvalue-report.md Decision 4 | Do these belong in Cupel.Testing vocabulary (S05), as standalone core analytics, or in M003? |
| `IntentSpec` as preset selector | March 15 radical-report.md rejected section | "20 lines of code" — still not enough signal to act on? |
| `ProfiledPlacer` companion package | March 15 radical-report.md surviving proposal 2 | No new signal; confirm still deferred to v1.3+. |
| Snapshot testing in `Cupel.Testing` | March 15 highvalue-report.md Decision 2 | `SelectionReport` ordering stability for ties still not guaranteed — confirm snapshot still blocked |

## New Ideas to Explore (Post-v1.2 Specific)

These angles were not possible to consider before v1.2 shipped:

- **Count-quota exclusion diagnostics** — What should count-quota exclusions look like in `SelectionReport`? Likely new `ExclusionReason` variants: `CountCapExceeded { kind, cap, count }` and `CountRequireUnmet { kind, require, actual }`. This is a direct feed for S03's algorithm design and has no equivalent in the March 15 brainstorm.
- **Rust `TimeProvider` pattern** — Rust has no BCL `TimeProvider`. The Rust crate already uses `chrono::DateTime<Utc>`. A minimal `trait TimeProvider { fn now(&self) -> DateTime<Utc>; }` would be the idiomatic equivalent. What's the concrete design? Struct that wraps `chrono::Utc::now()` as the system default, explicitly provided at construction.
- **`dry_run` in multi-budget contexts** — Now that `dry_run` exists in both languages and budget simulation (`GetMarginalItems`, `FindMinBudgetFor`) is planned for S06: are there design angles specific to how `dry_run` interacts with budget stepping that the March 15 brainstorm didn't address? (e.g. `DryRun(items, budget)` vs `DryRun(items, budgets[])` — does the multi-budget variant have a specification need?)
- **`QuotaUtilization` as a `SelectionReport` metric** — Post-v1.2, callers using `QuotaSlice` often want to know how much of each kind's quota was actually used (required 20%, got 15%). This is not in `SelectionReport` today. Does it belong in `SelectionReport`, in a Cupel.Testing assertion pattern, or as a `SelectionReport` extension method?
- **Timestamp coverage as a context health metric** — With `ContextItem.timestamp` live, a post-run metric "what fraction of included items have timestamps?" is a simple signal for whether time-based scoring is well-supported by the caller's data. Relevant to DecayScorer spec chapter: the spec should mention what happens when no items have timestamps.

## Sources

- `.planning/brainstorms/2026-03-15T12-23-brainstorm/SUMMARY.md` — Master summary of March 15 session; 18 proposals → 13 survived
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` — 6 high-value decisions; count-quota and testing vocabulary design inputs
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/radical-report.md` — Fork diagnostic and metadata convention proposals; both still active
- `spec/src/diagnostics/selection-report.md` — Exact field semantics for `SelectionReport`; feeds testing vocabulary precision
- `spec/src/slicers/quota.md` — `DISTRIBUTE-BUDGET` pseudocode; baseline for count-quota algorithm design pair
- `crates/cupel/src/scorer/recency.rs` — `RecencyScorer` rank-based implementation; explicit contrast for DecayScorer pair
- `crates/cupel/src/diagnostics/mod.rs` — Rust `SelectionReport`, `ExclusionReason` variants live
- `.kata/DECISIONS.md` — D039–D043 lock M002 scope and key API decisions; brainstorm must treat as given
