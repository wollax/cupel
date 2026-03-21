# Future Features — Challenger Report

*Explorer: future-features-ideas.md | Date: 2026-03-21*
*Targeting: S06 spec chapters — DecayScorer, OpenTelemetry verbosity, Budget Simulation*
*Locked: D042 (mandatory TimeProvider), D043 (cupel.* namespace). Neither is re-debated below.*

---

## Executive Summary

Three feature areas challenged. DecayScorer has the most critical spec gaps introduced by
post-v1.2 Rust parity: the `TimeProvider` trait shape is now a concrete decision rather than
a hypothesis. OTel verbosity needs exact attribute names and a cardinality table to be
actionable. Budget simulation needs precise semantics on three edge cases (`GetMarginalItems`
diff direction, `FindMinBudgetFor` lower bound, not-found return type) and a decision on
the multi-budget `DryRun` question. The multi-budget question has a clear answer: no.

---

## Feature 1: DecayScorer

### Challenge Round

**1.1 — Rust `TimeProvider` Trait Shape**

The four candidates (minimal trait, `Arc<dyn>`, generic `T: TimeProvider`, function pointer)
were each challenged:

*Candidate A (minimal trait) vs. B (Arc<dyn>):*
The question is whether the `DecayScorer` should own an `Arc<dyn TimeProvider>` or a
`Box<dyn TimeProvider>`. `Arc` allows shared ownership (same provider used across multiple
scorers); `Box` requires unique ownership. For `TimeProvider`, shared ownership is a
legitimate use case (one clock instance shared across a pipeline). However, for test use,
`Box` is sufficient and cheaper. The deciding factor: the `Scorer` trait must be `Send +
Sync` for async pipeline use. `Box<dyn TimeProvider + Send + Sync>` satisfies this, and
the ownership model is simpler.

*Candidate C (generic T: TimeProvider):*
Monomorphization is clean but breaks composability. A `Vec<Box<dyn Scorer>>` cannot hold
both `DecayScorer<SystemTimeProvider>` and `DecayScorer<FakeTimeProvider>`. This is a
practical problem: test utilities often construct a pipeline with a mix of scorer types.
Generic monomorphization is the right choice for `NullTraceCollector` (a pure optimization
with no behavioral variation), but `TimeProvider` has behavioral variation that matters
for tests. Reject Candidate C.

*Candidate D (function pointer):*
`fn() -> DateTime<Utc>` cannot capture state. A fake time provider must hold a `DateTime`
field. Function pointers cannot do this. Reject.

**Verdict on Rust TimeProvider:** Candidate A with `Box<dyn TimeProvider + Send + Sync>`.
The trait is minimal: `fn now(&self) -> DateTime<Utc>`. `SystemTimeProvider` is a ZST.
D042 is locked — the trait must be mandatory (no default). The implementation detail of
`Box` vs `Arc` is implementation-level, but the spec should require that the `TimeProvider`
is passed at construction and not defaulted.

**1.2 — `Window(maxAge)` Binary vs Linear Falloff**

Challenge: is a `Falloff` parameter worth the API surface?

The three curves (Exponential, Step, Window) already cover the practical space:
- Exponential: smooth continuous decay.
- Step: piecewise constant (simulates "windows" with different weights).
- Window: binary membership.

A linear falloff within a window is exactly a 2-step `Step` curve:
```
DecayCurve.Step([ (maxAge, 1.0), (TimeSpan.Zero, 0.0) ])
```
This already achieves linear-decay-within-window behavior without a new API parameter.
Adding `Falloff` to `Window` is API surface that `Step` already covers. Reject.

**Verdict:** `Window(maxAge)` stays binary (1.0 within, 0.0 outside). S06 spec chapter
must document that linear falloff within a window is achieved via `Step`.

**1.3 — Negative Age (Future-Dated Items)**

Challenge: all four options are logically defensible, but the spec can only choose one.

"Clamp to zero age" is the cleanest rule: `age = max(0, referenceTime - item.timestamp)`.
Score at zero age = 1.0 for Exponential (e^0 = 1), 1.0 for Window (within any window),
1.0 for Step (within the first step). Behavior is deterministic and consistent across
all three curve types.

"Treat as `nullTimestampScore`" (Candidate C from ideas) would penalize items from the
near-future. A message timestamped 3 seconds from now (clock skew) is clearly fresh and
should not score 0.5 by default. Reject.

Configurable future-dated policy adds API surface for an edge case. Reject.

**Verdict:** Clamp negative age to zero. Future-dated items are maximally fresh.
Spec pseudocode: `age = max(duration_zero, reference_time - item.timestamp)`.

**1.4 — Timestamp Coverage Warning via `ITraceCollector`**

Challenge: should the scorer emit a `TraceEvent::ScorerWarning` when all items have
null timestamps?

Adding a new `TraceEvent` variant forces every `match` arm in user code to handle it.
Even with `#[non_exhaustive]`, the `.NET` equivalent `switch` expression must add a
default arm. The cost of the warning signal is API surface burden on every
`ITraceCollector` implementor.

The alternative — `TimestampCoverage()` extension method on `SelectionReport` — gives
callers the same diagnostic information without emitting a novel trace event type. Zero
API contract change. Callers who care about coverage check it; callers who don't ignore it.

The warning-in-production case: `DecayScorer` on a pipeline where no items ever have
timestamps. This is a misconfiguration, but it's a misconfiguration the caller introduced.
The `DecayScorer` spec chapter can note the behavior and recommend checking
`TimestampCoverage()` in tests. That is sufficient.

**Verdict:** No new `TraceEvent::ScorerWarning` variant. Spec chapter notes zero-coverage
behavior. `TimestampCoverage()` extension method (as fourth analytics method alongside
`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount`) is the right signal surface.
**DI-1 (downstream input for S06):** Add `TimestampCoverage()` to the `SelectionReport`
analytics extension methods alongside `BudgetUtilization` and `KindDiversity`. Confirm
placement: `Wollax.Cupel.Analytics` for .NET, `cupel` crate for Rust. See X.1 in ideas.

**1.5 — Zero Half-Life in `Exponential(halfLife: TimeSpan.Zero)`**

Challenge: constructors should fail fast on invalid arguments. `halfLife = 0` is
mathematically undefined (division by zero in `age / halfLife`). Silent behavior at
construction time (Options B, C, D) delays the error until score time, making it hard
to debug.

`ArgumentException` / `Result::Err` at construction is the standard fail-fast pattern.
The precondition is clear and static: `halfLife > TimeSpan.Zero` (or `> Duration::ZERO`).

**Verdict:** Throw at construction. `halfLife` must be strictly positive. Same rule
applies to all time parameters in `Step` curve window boundaries where `TimeSpan.Zero`
in an intermediate position would create a zero-width window.

**1.6 — `nullTimestampScore` Placement**

Challenge: per-curve vs per-scorer.

Most users configure one scorer with one null policy. Per-curve configuration adds
complexity without a clear use case. The March 15 spec established `nullTimestampScore`
as a `DecayScorer` constructor parameter. Keep it there.

**Verdict:** `nullTimestampScore` stays at `DecayScorer` constructor level. Default: 0.5.
Per-curve null policy is "implementation decides" if a use case is demonstrated.

**1.7 — Timestamp Coverage as Fourth Analytics Method**

Challenge: Is `TimestampCoverage()` additive or duplicative?

`BudgetUtilization` = tokens used / budget.
`KindDiversity` = count of distinct ContextKinds in Included.
`ExcludedItemCount` = count matching predicate in Excluded.
`TimestampCoverage` = fraction of Included items with non-null timestamp.

None overlap. All are derivations from `SelectionReport` data. All belong in the same
analytics module. No new types needed. The fourth method is additive.

**Verdict:** `TimestampCoverage()` is confirmed as the fourth analytics method. S06 must
specify its signature, edge case when `Included.Count == 0` (return 0.0 or `null`?),
and placement in `Wollax.Cupel.Analytics`.

---

### S06 must specify — DecayScorer

1. **Rust `TimeProvider` trait**: `pub trait TimeProvider: Send + Sync { fn now(&self) -> DateTime<Utc>; }` with `SystemTimeProvider` as a ZST wrapping `Utc::now()`. D042 is locked: mandatory at construction, no default. Spec the Rust trait declaration as the canonical reference for S06 implementors.
2. **Negative age handling**: `age = max(Duration::ZERO, reference_time - item.timestamp)`. Future-dated items score as maximally fresh. Must appear in spec pseudocode for all three curve types.
3. **Zero half-life precondition**: `halfLife > TimeSpan.Zero` / `halfLife > Duration::ZERO`. Throw at construction with a clear error message naming the parameter and the constraint.
4. **`nullTimestampScore` default**: 0.5. Must be documented as "neutral: neither rewards nor penalizes missing timestamps." Constructor parameter is mandatory (not defaulted in the API) per D042's mandatory-injection principle (time dependency is visible; same rationale extends to null policy).
5. **`TimestampCoverage()` as fourth analytics method**: signature `double TimestampCoverage(this SelectionReport report)` / Rust `fn timestamp_coverage(report: &SelectionReport) -> f64`. Returns 0.0 when `Included` is empty.

### S06 should consider — DecayScorer

1. **`nullTimestampScore` mandatory vs defaulted**: D042 says TimeProvider is mandatory. Should `nullTimestampScore` follow the same principle (force explicit choice) or is 0.5 a sensible default that doesn't hide a configuration mistake? The time-dependency case is stronger (staleness risk in production); null policy is a preference, not a correctness issue. A default of 0.5 is probably fine, but the spec should weigh in.
2. **Step curve `TimeSpan.Zero` boundary semantics**: In a `Step` curve, can a step have `maxAge = TimeSpan.Zero`? Mathematically it's a degenerate step (only ages exactly zero would match). Spec should clarify whether zero-age steps are valid or forbidden.
3. **`OTel cupel.scorer.type` attribute**: At `StageOnly` tier, should the Score stage Activity record `cupel.scorer.type`? Small addition, high diagnostic value. Depends on whether S06 scopes OTel to include scorer identification.

---

## Feature 2: OpenTelemetry Verbosity

### Challenge Round

**2.1 — Exact `StageOnly` Attribute Names**

D043 is locked: `cupel.*` namespace. The attribute names proposed in the ideas file are
challenged for redundancy and OTEL conventions:

- `cupel.stage.duration_ms` — **reject**: redundant with Activity's built-in start/end
  timestamps. OTEL best practice: use Activity duration, not a separate attribute.
  Per-stage latency is already captured by the Activity's time range.
- `cupel.stage.name` — **keep**: the Activity's `DisplayName` should be the stage name,
  but an explicit attribute enables filtering across stages in a flat query. Worth including.
- `cupel.stage.item_count_in` and `cupel.stage.item_count_out` — **keep both**: these are
  the diagnostic signal that matter most ("why did 500 items enter Score but only 480 exit
  Deduplicate?"). Two fields, not one, because the diff is the signal.
- `cupel.budget.max_tokens` — **belongs on root Activity, not stage Activities**. Budget
  is pipeline-level configuration, not a per-stage measurement. At `StageOnly` tier, the
  root Activity (wrapping all 5 stage Activities) carries pipeline config.

The ideas file proposed both `item_count_in` and `item_count_out` as separate attributes
versus a single `item_count`. Two fields win: the diff between in and out is what reveals
stage impact without a second query join.

**Verdict on `StageOnly`:**
```
Root Activity: "cupel.pipeline"
  cupel.budget.max_tokens  (int)
  cupel.verbosity          (string: "StageOnly" | "StageAndExclusions" | "Full")

Per-stage Activity: "cupel.stage.{name}"
  cupel.stage.name         (string: "Classify" | "Score" | "Deduplicate" | "Slice" | "Place")
  cupel.stage.item_count_in  (int)
  cupel.stage.item_count_out (int)
```
No `duration_ms` attribute. Activity duration carries that signal.

**2.2 — `StageAndExclusions` Tier Additions**

Challenge: events vs. child Activities for per-exclusion data.

Activities carry duration. Exclusion data points have no measurable duration — they are
state snapshots. OTEL Activity events (tagged points in time on a parent Activity) are
the correct shape for exclusion records. Activities are for work units.

Per-exclusion Event on the relevant stage's Activity:
```
Event: "cupel.exclusion"
  cupel.exclusion.reason     (string: "BudgetExceeded" | "NegativeScore" | "Duplicate" | ...)
  cupel.exclusion.item_kind  (string: ContextKind value)
  cupel.exclusion.item_tokens (int)
```
Per-stage summary attribute added at `StageAndExclusions`:
```
  cupel.exclusion.count  (int) — on the stage Activity that produced the exclusions
```

`cupel.exclusion.reason` values must match the `ExclusionReason` enum value names exactly,
enabling trace backends to group by reason without custom parsing.

**Verdict on `StageAndExclusions`:** Events for per-exclusion records; `exclusion.count`
summary attribute on the stage Activity. Reason values match ExclusionReason names.

**2.3 — `Full` Tier Additions**

Challenge the `cupel.item.placement` attribute: "pinned"/"placed" are implementation
concepts. A caller using `NullTraceCollector` never sees placement state. Placement position
is meaningful but expressing it as a string requires parsing in trace backends. Use `int`
(final position in the output list, 0-indexed). Or omit placement from the OTel tier
(it is available in `SelectionReport.Included` for callers who need it).

Recommendation: omit `cupel.item.placement` from `Full` tier. Callers who need placement
information use `SelectionReport` directly. OTel is for aggregate pipeline observability,
not full item state replication.

Per-included-item Event at `Full` tier:
```
Event: "cupel.item.included"
  cupel.item.kind    (string: ContextKind value)
  cupel.item.tokens  (int)
  cupel.item.score   (float64)
```

**Verdict on `Full`:** Three attributes on `cupel.item.included` events. Omit placement.

**2.4 — Cardinality Warning Documentation**

Challenge: where does the warning live?

A comment buried in source code will not reach operators. The warning must appear in:
1. Package README: a "Production Recommendations" section.
2. API doc on the `TraceVerbosity.Full` enum value itself: an XML doc warning.
3. Spec chapter: a cardinality table.

The ideas file's cardinality table is correct and should be reproduced in the spec:

| Tier | Events/call (typical) | Recommended environment |
|------|-----------------------|------------------------|
| StageOnly | ~10 | Production |
| StageAndExclusions | ~10 + 0–300 | Staging / debug |
| Full | ~10 + 0–1000 | Development only |

**Verdict:** Cardinality table mandatory in spec. `Full` is explicitly flagged as
"Development only" with a recommendation to use `StageAndExclusions` in staging.

**2.5 — Activity Naming Convention**

`cupel.stage.{name}` vs `Cupel/{StageName}` vs raw stage name.

`cupel.stage.{name}` is self-describing and consistent with the attribute namespace.
A trace backend filtering on `ActivityName LIKE 'cupel.stage.%'` returns exactly the
pipeline stages and nothing else. REST-style naming (`Cupel/Classify`) is common in
HTTP instrumentation but looks odd for a selector pipeline.

**Verdict:** Activity names are `cupel.stage.{name}` (lowercase stage name).
Root Activity name: `cupel.pipeline`.

**2.6 — `cupel.verbosity` on Root Activity**

The ideas file proposed this. Challenge: who reads it?

Post-hoc debugging benefit is real: a trace that lacks exclusion events is confusing
without knowing the verbosity setting. `cupel.verbosity` on the root Activity answers
"why don't I see exclusion data?" immediately. The cost is one string attribute.

**Verdict:** Include `cupel.verbosity` on the root Activity. Small cost, clear value.

---

### S06 must specify — OpenTelemetry

1. **Activity hierarchy**: root `cupel.pipeline` Activity contains 5 child `cupel.stage.{name}` Activities. The stage name set must match the pipeline stage names exactly.
2. **`StageOnly` attribute set** (exact names and types as above): `cupel.budget.max_tokens` (int), `cupel.verbosity` (string) on root; `cupel.stage.name` (string), `cupel.stage.item_count_in` (int), `cupel.stage.item_count_out` (int) on each stage Activity. No `duration_ms` attribute.
3. **`StageAndExclusions` additions**: `cupel.exclusion` Event per excluded item with `cupel.exclusion.reason` (string), `cupel.exclusion.item_kind` (string), `cupel.exclusion.item_tokens` (int); `cupel.exclusion.count` (int) summary on stage Activity.
4. **`Full` additions**: `cupel.item.included` Event per included item with `cupel.item.kind` (string), `cupel.item.tokens` (int), `cupel.item.score` (float64). No placement attribute.
5. **Cardinality table**: reproduced verbatim in spec, README, and `Full` API doc.
6. **D043 is locked**: all attribute names begin with `cupel.`. Spec marks the namespace as pre-stable. README must include: "Attribute names are pre-stable and subject to change as OTEL LLM semantic conventions stabilize."

### S06 should consider — OpenTelemetry

1. **`cupel.scorer.type` at Score stage**: `cupel.scorer.type` = "RecencyScorer" | "DecayScorer" | "custom" on the `cupel.stage.score` Activity. Small addition; helps correlate Score stage latency with scorer type. Decision: include at `StageOnly` tier or `Full` only?
2. **Exclusion reason grouping**: `cupel.exclusion.reason` uses `ExclusionReason` enum value names. If a new reason is added (e.g., `CountQuotaUnmet`), trace dashboards will silently pick it up. This is a feature, not a bug — but the spec should document that attribute values are open-ended and trace backends must not hard-code the set.
3. **ActivitySource name**: The `ActivitySource` name in the `Wollax.Cupel.Diagnostics.OpenTelemetry` package should be `"Wollax.Cupel"` (package-level source name). This is the name callers use in `AddSource()` configuration. Spec should declare it.

---

## Feature 3: Budget Simulation

### Challenge Round

**3.1 — Multi-Budget `DryRun` Variant**

Challenge: is this worth specifying?

The ideas file's analysis is correct: Classify → Score → Deduplicate → Sort are all
budget-independent. Only the Slice stage changes per budget. A multi-budget variant
saves those four stages across 10-15 `FindMinBudgetFor` iterations.

But the concrete savings: 10-15 `DryRun` calls on ≤500 items. Each full call is
sub-millisecond on a modern CPU. The optimization saves ~microseconds total. This is
not a performance bottleneck.

More importantly: `FindMinBudgetFor` is already an API method. A multi-budget `DryRun`
variant would expose internal pipeline stage structure to the public API surface. That's
API coupling to an implementation detail.

**Verdict: Reject multi-budget `DryRun`.** `FindMinBudgetFor` calls single-budget `DryRun`
~10-15 times. The optimization is microseconds; the coupling cost is permanent.
**Implementation (S06 decides):** If `FindMinBudgetFor` profiling reveals Slice-stage
dominance, an internal pipeline method can share setup across multiple Slice calls without
changing the public API.

**3.2 — `GetMarginalItems` Diff Direction**

Challenge: is "Primary \ Margin" the correct direction?

Primary = items at `budget.MaxTokens`. Margin = items at `budget.MaxTokens - slackTokens`.

If inclusion is monotonic (more budget → at least as many items), then `Margin ⊆ Primary`.
The diff `Primary \ Margin` is: items included at full budget but not at reduced budget.
This is exactly "the marginal items that would be lost if budget shrank by `slackTokens`."

The name `GetMarginalItems` means "items at the margin of inclusion." This is precise.

**Verdict:** `Primary \ Margin` is correct. Spec must state the monotonicity precondition
explicitly and guard against `QuotaSlice` (same guard as `FindMinBudgetFor`).

**3.3 — `FindMinBudgetFor` Search Bounds**

Challenge: what is the correct lower bound?

The ideas file proposes `targetItem.Tokens` as the lower bound. This is the minimum
budget that could include the target item if it were the only item. But pinned items
consume budget ahead of the selection step — if `pinnedTokens > 0`, then a budget
equal to `targetItem.Tokens` will never include the target item because the budget
is already consumed.

Revised lower bound: `targetItem.Tokens + pinnedBudgetConsumption`. But `pinnedBudget-
Consumption` is computed at pipeline run time, not at the call site. Computing it before
the search loop requires an additional pipeline call.

Practical simplification: the binary search will find the correct answer regardless of
lower bound (it converges). The lower bound only affects how many iterations are needed.
Setting the lower bound to `targetItem.Tokens` is a fast-path optimization, not a
correctness constraint. If the item doesn't fit even at `targetItem.Tokens` budget (due
to pinned consumption), the search will determine that correctly by running `DryRun`.

**Verdict on lower bound:** `targetItem.Tokens` is the lower bound (not 1 token — that
wastes iterations on budgets that mathematically cannot work). Spec precondition:
`searchCeiling >= targetItem.Tokens`, otherwise throw immediately with a clear message.
Spec postcondition: lower bound is an optimization hint, not a correctness guarantee.

**Additional precondition:** `targetItem` must be in `items`. The spec should guard:
"if targetItem is not in items, throw ArgumentException." An item outside the input set
will never be selected regardless of budget.

**3.4 — Non-Monotonicity Beyond `QuotaSlice`**

Challenge: is the guard too narrow or too broad?

The ideas file correctly identifies that `KnapsackSlice` and `GreedySlice` are monotonic.
Only `QuotaSlice` is non-monotonic among built-in slicers. Custom slicers are unknown.

Adding `ISlicer.IsMonotonic` (Candidate B) would be the most principled approach, but
it adds API surface burden to every slicer implementor and creates a correctness risk:
a custom slicer could declare `IsMonotonic = true` incorrectly, giving false confidence.
The guard would then silently fail to protect.

A runtime monotonicity check (verifying that the binary search converges) would catch
non-monotonic behavior empirically but is non-trivial to specify.

**Verdict:** Guard `QuotaSlice` specifically at the call site with a clear `InvalidOperationException`
message naming the limitation. Document the general monotonicity precondition in XML docs
and spec. Defer `ISlicer.IsMonotonic` to v1.3 pending evidence of custom-slicer usage.
The `QuotaSlice` guard is implementation-level; the monotonicity precondition is spec-level.

**3.5 — `FindMinBudgetFor` Return Type for "Not Found"**

Challenge: all four options were proposed. The decision is between `int?` and throwing.

"Target item not selectable within searchCeiling" is a valid business outcome for an
adaptive agent: "I'd need more than 32K tokens to include this item." This is not an
error; it's a search result. Forcing callers to use try/catch for a non-exceptional
condition is poor API design.

`int.MaxValue` sentinel is a trap — callers who forget to check will allocate a
32K-token context window for no reason. Hard no.

`BudgetSearchResult` wrapper is over-engineered for a binary answer (found/not-found + value).

**Verdict:** Return `int?` (C#) / `Option<i32>` (Rust). `null` / `None` means no
budget in `[targetItem.Tokens, searchCeiling]` includes `targetItem`. Document what
"not found" means: the target item is excluded at every budget ≤ searchCeiling (possibly
because it requires more tokens than `searchCeiling` allows, or because other slicing
constraints exclude it regardless of budget).

**3.6 — `DryRun` Determinism Precondition**

Challenge: the ideas file (X.3) identifies a spec gap that directly affects `FindMinBudgetFor`
correctness. If `DryRun` is non-deterministic, binary search may oscillate.

`DryRun` calls `run_traced` internally. Non-determinism sources:
- Tie-breaking in scoring (items with equal scores).
- Tie-breaking in `KnapsackSlice` DP (items with equal tokens filling the same cell).

The spec should state: "DryRun is deterministic for a fixed set of items and budget.
Determinism requires stable tie-breaking: the pipeline must produce the same result on
repeated calls with identical inputs." This is currently implied but not stated.

**Verdict:** S06 must include a `DryRun` determinism invariant in the budget simulation
spec chapter.

---

### S06 must specify — Budget Simulation

1. **No multi-budget `DryRun` variant**: explicitly state that `FindMinBudgetFor` calls single-budget `DryRun` in a binary search loop. This is intentional (API coupling cost exceeds optimization benefit).
2. **`GetMarginalItems` diff direction**: `Primary \ Margin` (items in full-budget result not in reduced-budget result). Method assumes monotonic inclusion. Guard against `QuotaSlice` with `InvalidOperationException` matching `FindMinBudgetFor` guard.
3. **`FindMinBudgetFor` lower bound**: `targetItem.Tokens`. Preconditions: (a) `targetItem` must be an element of `items` (throw `ArgumentException` otherwise); (b) `searchCeiling >= targetItem.Tokens` (throw `ArgumentException` otherwise). Document lower bound as an iteration-count optimization, not a correctness guarantee.
4. **`FindMinBudgetFor` return type**: `int?` / `Option<i32>`. `null` / `None` means target item not selectable within `[targetItem.Tokens, searchCeiling]`.
5. **`FindMinBudgetFor` monotonicity guard**: Guard against `QuotaSlice` with `InvalidOperationException` message: "FindMinBudgetFor requires monotonic item inclusion. QuotaSlice produces non-monotonic inclusion as budget changes shift percentage allocations." Document the general monotonicity precondition for custom slicers.
6. **`DryRun` determinism invariant**: Spec must assert that `DryRun` is deterministic for identical inputs. Tie-breaking must be stable (items with equal scores or token counts must be ordered consistently across calls).

### S06 should consider — Budget Simulation

1. **`FindMinBudgetFor` convergence stopping condition**: Stop when `high - low <= 1` (exact minimum, maximum `log2(searchCeiling)` iterations). Alternatively, allow a `tolerance` parameter for cases where budgets are always multiples of a known granularity (e.g., 256 tokens). Keep simple for the spec; document the optimization hint.
2. **`GetMarginalItems` return type**: `IReadOnlyList<ScoredItem>` includes the score from the full-budget run. This allows callers to prioritize which marginal items to protect. Confirm this is the right type (vs. `IReadOnlyList<ContextItem>` without scores).
3. **Budget simulation and `dry_run` parity in Rust**: the budget simulation methods (`GetMarginalItems`, `FindMinBudgetFor`) are scoped to the .NET API in the March 15 spec. Given that `dry_run` is now live in both languages, should Rust get equivalent methods? S06 should make an explicit parity decision.
4. **`SweepBudget` placement**: confirmed moved to Smelt per March 15. S06 spec chapter should note this explicitly so future maintainers don't attempt to add it to Cupel.

---

## Cross-Feature Notes

### DI-1 (Downstream input for S06 — Analytics)
Add `TimestampCoverage()` as the fourth `SelectionReport` analytics extension method.
Signature: `double TimestampCoverage(this SelectionReport report)` (C#) / `pub fn timestamp_coverage(report: &SelectionReport) -> f64` (Rust).
Edge case: return 0.0 when `Included` is empty.
Placement: `Wollax.Cupel.Analytics` (.NET) / inline in `cupel` crate (Rust).

### DI-2 (Downstream input for S06 — OTel)
Activity hierarchy is flat (root + 5 stage children), not nested. Each stage Activity
is a direct child of `cupel.pipeline`. Root Activity carries pipeline-level config
attributes; stage Activities carry per-stage item-count attributes. No shared attributes
between tiers — each tier is additive on top of the tier below.

### DI-3 (Downstream input for S06 — Rust parity)
`TimeProvider` is a Rust-specific design decision (no BCL equivalent). The spec chapter
must include a Rust-specific section with the trait declaration and `SystemTimeProvider`
ZST. The .NET chapter uses `System.Threading.TimeProvider` (BCL). Both are mandatory
per D042 — the spec just has language-specific implementation forms.

### Implementation notes (S06 decides, not spec-level)
- Whether `Box<dyn TimeProvider + Send + Sync>` or `Arc<dyn TimeProvider + Send + Sync>`
  is used internally in `DecayScorer` (Rust).
- The internal optimization in `FindMinBudgetFor` to share Classify/Score/Deduplicate/Sort
  work across Slice calls.
- Whether `ActivitySource` is a static instance or passed at construction.
- Whether `cupel.stage.name` is also used as the `Activity.DisplayName` (it should be,
  but this is an implementation choice that the spec may leave unspecified).

---

## Verdict Summary Table

| Proposal | Verdict | S06 action |
|----------|---------|------------|
| Rust TimeProvider: minimal trait + SystemTimeProvider ZST | Accept | Must specify |
| Window(maxAge) binary vs linear falloff | Reject linear — use Step | Document in spec |
| Negative age: clamp to zero | Accept | Must specify (pseudocode) |
| Timestamp coverage warning via TraceEvent | Reject — use extension method | Must specify TimestampCoverage() |
| Zero half-life: throw at construction | Accept | Must specify as precondition |
| nullTimestampScore per-scorer (not per-curve) | Accept existing design | Confirm in spec |
| TimestampCoverage() as fourth analytics method | Accept | Must specify (DI-1) |
| StageOnly: no duration_ms attribute | Accept | Must specify exact attribute set |
| StageAndExclusions: Events (not Activities) for exclusions | Accept | Must specify |
| Full tier: three attributes, no placement | Accept | Must specify |
| Cardinality table | Accept | Must specify (mandatory in README + spec) |
| cupel.verbosity on root Activity | Accept | Must specify |
| Activity naming: cupel.stage.{name} | Accept | Must specify |
| Multi-budget DryRun | Reject — over-engineering | Explicitly note as intentionally absent |
| GetMarginalItems diff direction: Primary \ Margin | Accept | Must specify |
| FindMinBudgetFor lower bound: targetItem.Tokens | Accept | Must specify as precondition |
| FindMinBudgetFor return type: int? / Option<i32> | Accept | Must specify |
| MonotonicityGuard: QuotaSlice-specific, defer ISlicer.IsMonotonic | Accept | Must specify guard |
| DryRun determinism invariant | Accept (new gap identified) | Must specify |
