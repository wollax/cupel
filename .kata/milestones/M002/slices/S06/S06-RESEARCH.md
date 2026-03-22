# S06: Future Features Spec Chapters — Research

**Date:** 2026-03-21

## Summary

S06 produces three spec chapters: `spec/src/scorers/decay.md` (DecayScorer), `spec/src/integrations/opentelemetry.md` (OTel verbosity), and `spec/src/analytics/budget-simulation.md` (budget simulation API contracts). All deliverables are spec-only (no code); the chapters must be reachable from `spec/src/SUMMARY.md`.

The brainstorm (S01 `future-features-report.md`) pre-resolved nearly every open design question in these three chapters, producing explicit "S06 must specify" lists for all three feature areas. The main remaining work is spec authoring — organizing the settled decisions into well-structured chapters with pseudocode, algorithm text, conformance vector outlines, and no TBD fields. The one unresolved design gap is the **budget-override mechanism for GetMarginalItems/FindMinBudgetFor** (see Open Risks).

S01's brainstorm forward-intel identified a **DryRun determinism invariant** as a spec gap — same inputs → same outputs is currently implied but not stated. This must appear as normative text in the budget simulation chapter.

## Recommendation

Write the three chapters in dependency order: DecayScorer first (self-contained), then OTel (references stage names), then budget simulation (references DryRun and QuotaSlice guard from S03). Before authoring, read `spec/src/scorers/metadata-trust.md` (S04 output) for the established chapter style (Overview table, Algorithm pseudocode in `text` fenced blocks, Conformance Notes, Edge Cases sections). Read `spec/src/slicers/quota.md` for the DISTRIBUTE-BUDGET pseudocode naming conventions.

SUMMARY.md needs two new top-level sections (`# Integrations` and `# Analytics`) and a new entry under `# Scorers`. Create `spec/src/integrations/` and `spec/src/analytics/` directories as part of writing the chapters.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Chapter style | `spec/src/scorers/metadata-trust.md` (S04) | Established template: Overview + Fields table + Conventions + Algorithm pseudocode + Edge Cases + Conformance Notes |
| Pseudocode naming conventions | `spec/src/slicers/quota.md` DISTRIBUTE-BUDGET | COUNT-DISTRIBUTE-BUDGET pseudocode in S03 follows the same style; DecayScorer pseudocode should match |
| OTel stage name set | `spec/src/pipeline.md` + pipeline chapter structure | Stage names are canonical: Classify, Score, Deduplicate, Sort (not in spec pipeline structure — see pipeline chapters), Slice, Place |
| QuotaSlice guard language for non-monotonicity | `.planning/design/count-quota-design.md` Section 5 + `spec/src/slicers/quota.md` | S06 budget simulation chapter cites the same guard pattern established in S03; reuse the language |

## Existing Code and Patterns

- `spec/src/scorers/recency.md` — RecencyScorer: the immediate sibling scorer; establishes the contrast between rank-based (RecencyScorer) and absolute-decay (DecayScorer); read the Fields Used table and Algorithm section for style
- `spec/src/scorers/metadata-trust.md` — MetadataTrustScorer (S04 output): authoritative chapter style template; use its section ordering (Overview → Conventions → Algorithm → Configuration → Edge Cases → Conformance Notes)
- `spec/src/slicers/quota.md` — QuotaSlice: establishes the DISTRIBUTE-BUDGET pseudocode style and the non-monotonicity behavior that blocks `FindMinBudgetFor`; the budget simulation chapter must reference this slicer by name
- `.planning/design/count-quota-design.md` Section 5 — KnapsackSlice guard language from S03; S06 budget simulation chapter should mirror this pattern for the `QuotaSlice` guard on `FindMinBudgetFor`
- `spec/src/diagnostics/selection-report.md` — SelectionReport fields; budget simulation chapter must reference `included`, `total_candidates`, and the DryRun flow
- `crates/cupel/src/scorer/recency.rs` — Rust chrono usage: `DateTime<Utc>` is the established timestamp type in this codebase; `DecayScorer` TimeProvider should return `DateTime<Utc>` in Rust
- `src/Wollax.Cupel/CupelPipeline.cs:86` — `.NET` `DryRun(IReadOnlyList<ContextItem> items)` uses the pipeline's stored `_budget` (set at construction); budget simulation methods that need different budget values must explicitly address this (see Open Risks)
- `src/Wollax.Cupel/ContextItem.cs:69` — `DateTimeOffset? Timestamp` in .NET; `chrono::DateTime<Utc>` equivalent in Rust; DecayScorer consumes this field

## Constraints

- **Net10.0 target** — `System.TimeProvider` is in the BCL since .NET 8; `net10.0` projects can use it without a NuGet dependency. DecayScorer's `.NET` spec should reference `System.TimeProvider` directly.
- **Rust chrono crate is already present** — `DateTime<Utc>` is the correct return type for the Rust `TimeProvider` trait's `now()` method. No new crate dependency needed.
- **OTel is a companion package only** — D039 prohibits code changes in M002; but more importantly, the OTel chapter specifies a companion package `Wollax.Cupel.Diagnostics.OpenTelemetry`, not additions to core. The spec chapter must state this zero-dep guarantee explicitly.
- **No code changes** — S06 is spec authoring only. If the spec reveals an API gap (e.g., budget-override mechanism), the gap is noted in the chapter as a "spec precondition for implementation" — it does not block the spec chapter from being complete.
- **Spec style** — pseudocode in `text` fenced blocks with CAPS-WITH-HYPHENS procedure names; algorithm steps as numbered or sequential indented lines; no code-language fences for pseudocode
- **D043 locked** — all OTel attribute names begin with `cupel.`; spec marks namespace as pre-stable; no `gen_ai.*` attributes
- **D042 locked** — `TimeProvider` is mandatory; no overload that defaults to system time

## Key Decisions Already Settled (lock — do not re-debate)

These come from the brainstorm challenger report, locked decisions register, and S01/S03 outputs:

### DecayScorer
- **D042**: TimeProvider is mandatory at construction; no silent default
- **D047**: Rust TimeProvider: `trait TimeProvider: Send + Sync { fn now(&self) -> DateTime<Utc>; }` with `SystemTimeProvider` ZST; stored as `Box<dyn TimeProvider + Send + Sync>`
- **Age clamping**: `age = max(duration_zero, reference_time - item.timestamp)` — negative age (future-dated items) clamps to zero; items are maximally fresh
- **Zero half-life**: `halfLife = 0` (or `Duration::ZERO`) throws `ArgumentException`/`Err` at construction
- **`Window(maxAge)` is binary** (1.0 within, 0.0 at/outside boundary); linear falloff within a window is achieved via `Step`, not a `Falloff` parameter
- **`nullTimestampScore`** is per-scorer (not per-curve), defaults to 0.5; configurable
- **TimestampCoverage()** — no `TraceEvent::ScorerWarning` variant; spec chapter notes zero-coverage behavior and recommends checking the `TimestampCoverage()` extension method (fourth analytics method in core — D045)

### OpenTelemetry
- **D043**: `cupel.*` attribute namespace; pre-stable disclaimer required
- Activity hierarchy: root `cupel.pipeline` Activity; 5 child `cupel.stage.{name}` Activities (one per stage: classify, score, deduplicate, sort, slice, place — note: 6 pipeline stages per spec)
- **StageOnly attributes** (exact): `cupel.budget.max_tokens` (int) + `cupel.verbosity` (string) on root; `cupel.stage.name` (string) + `cupel.stage.item_count_in` (int) + `cupel.stage.item_count_out` (int) on each stage Activity; no `duration_ms`
- **StageAndExclusions additions**: `cupel.exclusion` Event per excluded item with `cupel.exclusion.reason` (string, matches ExclusionReason variant names), `cupel.exclusion.item_kind` (string), `cupel.exclusion.item_tokens` (int); `cupel.exclusion.count` (int) summary attribute on the stage Activity
- **Full tier additions**: `cupul.item.included` Event per included item with `cupel.item.kind` (string), `cupel.item.tokens` (int), `cupel.item.score` (float64); no placement attribute
- **Cardinality table mandatory** (StageOnly ~10 events/call — Production; StageAndExclusions ~10+0-300 — Staging; Full ~10+0-1000 — Development only)
- ActivitySource name: `"Wollax.Cupel"`
- `cupel.verbosity` on root Activity
- `cupel.exclusion.reason` values are open-ended (new ExclusionReason variants arrive without a schema change)

### Budget Simulation
- **D044**: No multi-budget `DryRun` variant; `FindMinBudgetFor` calls single-budget `DryRun` ~10-15 times
- **D048**: `FindMinBudgetFor` return type is `int?`/`Option<i32>`; null/None = not found within searchCeiling
- **`GetMarginalItems` diff direction**: `Primary \ Margin` (items in full-budget run not in reduced-budget run); assumes monotonic inclusion
- **`FindMinBudgetFor` lower bound**: `targetItem.Tokens`; preconditions: (a) `targetItem` must be an element of `items`, (b) `searchCeiling >= targetItem.Tokens`
- **Monotonicity guard**: guard `QuotaSlice` specifically with `InvalidOperationException`; document general precondition; defer `ISlicer.IsMonotonic`
- **DryRun determinism invariant**: must appear as normative text — "DryRun is deterministic for identical inputs; tie-breaking must be stable across calls"
- **Rust parity decision**: the brainstorm flagged this as an "S06 decides" open question; `dry_run` is now live in Rust, but budget simulation methods are scoped to .NET API in the March 15 spec; S06 must make an explicit "Rust parity: deferred to M003+" decision rather than leaving it unaddressed
- **SweepBudget**: explicitly out of scope for Cupel (moved to Smelt); spec chapter should note this to prevent future confusion

## Common Pitfalls

- **Spec says 5 pipeline stages, but there are 6** — The pipeline chapter shows: Classify → Score → Deduplicate → Sort (4 at score stage) → Slice → Place. Sort is listed separately in the pipeline spec (`Stage 4: Sort`). The OTel chapter must cover all 6 stages: classify, score, deduplicate, sort, slice, place. The brainstorm report says "5 child Activities" — check `spec/src/pipeline.md` for the authoritative stage list before writing.
- **`GetMarginalItems` signature includes explicit `budget` parameter in the roadmap** — the roadmap shows `GetMarginalItems(items, budget, slackTokens)`. This is because the current `.NET` `DryRun(items)` uses the pipeline's stored budget. Understand the intended interaction before writing the spec: the budget param may be needed explicitly, or the spec may define a budget-override overload. Resolve this before writing pseudocode.
- **SUMMARY.md new sections** — Two new top-level sections needed (`# Integrations` and `# Analytics`); `spec/src/scorers/decay.md` fits under the existing `# Scorers` section. Ensure the sections and files are created; `mdBook` will fail to build if a listed file doesn't exist.
- **`Window(maxAge)` boundary behavior** — "age exactly at window boundary" is an edge case requiring explicit specification: is `age == maxAge` inside or outside the window? Spec must commit (recommend: 0.0 at boundary, consistent with half-open interval `[0, maxAge)`).
- **Step curve zero-width windows** — `Step` windows where a boundary is `TimeSpan.Zero` / `Duration::ZERO` creates a zero-width window. Should throw at construction (same pattern as `halfLife = 0` for Exponential). Flag this in the preconditions section.
- **`CountQuotaSlice` non-monotonicity** — S03 established that `CountQuotaSlice` also has non-monotonic inclusion risk (build-time guard for KnapsackSlice). The `FindMinBudgetFor` monotonicity guard should mention `CountQuotaSlice` alongside `QuotaSlice`, or the chapter should state the general principle: any slicer with count-based constraints is non-monotonic.

## Open Risks

### HIGH: Budget-override mechanism for GetMarginalItems/FindMinBudgetFor

**Risk**: The current `.NET` `CupelPipeline.DryRun(IReadOnlyList<ContextItem> items)` uses the pipeline's fixed `_budget` set at construction. `GetMarginalItems` and `FindMinBudgetFor` must run `DryRun` at different token budgets. The spec chapter must define how this works.

**Options**:
1. Both methods accept an explicit `ContextBudget` parameter that overrides the pipeline budget for internal runs (cleanest; budget simulation methods manage their own budget arithmetic)
2. Methods accept token count parameters and construct temporary `ContextBudget` overrides internally (hides budget cloning complexity)
3. The spec defines the methods as operating on the pipeline's own budget, with `slackTokens` as a delta from `pipeline.Budget.MaxTokens`

The brainstorm report shows `GetMarginalItems(items, budget, slackTokens)` with an explicit `budget` param, which implies option 1 or 2. **Resolve this before writing pseudocode** — the pseudocode will need to show how the "reduced budget" run is constructed.

**Mitigation**: Read `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-ideas.md` section 3 for the original intent; the signature `GetMarginalItems(items, budget, slackTokens)` in the milestone roadmap strongly implies the budget is passed explicitly as a `ContextBudget` or token count. The spec chapter can define the signature without waiting for implementation; note in the chapter: "The implementation constructs a temporary pipeline run at `budget.MaxTokens - slackTokens`."

### MEDIUM: Pipeline stage count (5 vs 6)

**Risk**: The brainstorm report says "root Activity + 5 child stage Activities." But the pipeline spec has 6 stages (Classify, Score, Deduplicate, Sort, Slice, Place). The Sort stage may or may not get its own Activity. Check `spec/src/pipeline.md` and `spec/src/pipeline/sort.md` to confirm the canonical stage count before writing.

**Mitigation**: If Sort is a distinct stage with its own TraceEvent (check `spec/src/diagnostics/events.md`), it gets its own Activity. If it's folded into the Sort step of the Slice stage, the Activity may be omitted. Authoritative answer in `events.md`.

### LOW: Step curve specification precision

**Risk**: `Step(windows)` needs a precise window structure definition — what is the type of `windows`? Is it a list of `(TimeSpan boundary, double score)` tuples? Is it ordered high-to-low or low-to-high? The brainstorm left "window structures" as "S06 decides." An ambiguous `Step` spec is as bad as no `Step` spec.

**Mitigation**: Model after `RecencyScorer` which defines "N timestamped items" precisely. For `Step`, define: `windows` is a list of `(maxAge: Duration, score: double)` pairs ordered from youngest to oldest boundary; the scorer walks the list and returns the `score` for the first `window.maxAge >= age`; a final catch-all (optional) defines behavior when `age` exceeds all window boundaries.

### LOW: Rust parity for budget simulation explicit decision

**Risk**: If S06 doesn't explicitly say "Rust budget simulation deferred to M003+," future implementors may assume it was accidentally omitted.

**Mitigation**: Add a one-sentence "Language Parity Note" to the budget simulation chapter: "The budget simulation API is scoped to the .NET implementation in v1. Rust parity is deferred to M003+."

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| mdBook (spec authoring) | — | No relevant skill; standard markdown authoring |
| OpenTelemetry (.NET ActivitySource API) | — | No installed skill; standard knowledge |

## Sources

- `S06 must specify` lists from `future-features-report.md` — authoritative per-feature mandate lists (source: `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md`)
- S01 downstream inputs DI-1 through DI-3 in `future-features-report.md` — TimestampCoverage(), OTel Activity hierarchy, Rust TimeProvider trait (source: `.planning/brainstorms/2026-03-21T09-00-brainstorm/future-features-report.md`)
- S03 KnapsackSlice guard language — non-monotonicity precedent for budget simulation guard (source: `.planning/design/count-quota-design.md` Section 5)
- M002 Decisions D042–D048 — locked API decisions for all three feature areas (source: `.kata/DECISIONS.md`)
- `.NET` `DryRun` implementation — confirms pipeline-fixed budget; budget simulation needs explicit budget parameter (source: `src/Wollax.Cupel/CupelPipeline.cs:86`)
- `System.TimeProvider` BCL availability — confirmed available in `net10.0` (source: `Directory.Build.props`, Microsoft .NET 8 release notes)
- Pipeline stage count — 6 stages in spec: Classify, Score, Deduplicate, Sort, Slice, Place (source: `spec/src/SUMMARY.md`)
