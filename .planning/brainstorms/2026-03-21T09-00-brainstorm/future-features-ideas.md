# Future Features — Explorer Mode

*Explorer: uncensored idea generation | Date: 2026-03-21*
*Targeting: S06 spec chapters — DecayScorer, OpenTelemetry verbosity, Budget Simulation*
*Post-v1.2 context: `dry_run` live in both languages, `SelectionReport` stable, `KnapsackSlice` OOM guard concrete, `ContextItem.metadata` and `.timestamp` stable and tested.*

---

## Grounding Note

`RecencyScorer` (Rust) is rank-based: it computes ordinal position among timestamped items.
A single item scores 1.0; items without timestamps score 0.0; ties broken by relative rank.
`DecayScorer` is categorically different: it computes absolute age-based relevance using a
reference time and a decay curve. The two scorers answer different questions:
- `RecencyScorer`: "Among these candidates, which are newest relative to each other?"
- `DecayScorer`: "Regardless of peers, is this item still temporally relevant right now?"

---

## Section 1 — DecayScorer Angles

### Proposal 1.1 — Rust `TimeProvider` Trait Design

The .NET side uses `System.Threading.TimeProvider` (BCL). In Rust, there is no stdlib equivalent.
The idiomatic Rust approach: define a local trait.

Candidate A — minimal trait:
```rust
pub trait TimeProvider: Send + Sync {
    fn now(&self) -> DateTime<Utc>;
}
```
Then a zero-sized system default:
```rust
#[derive(Debug, Clone, Copy)]
pub struct SystemTimeProvider;

impl TimeProvider for SystemTimeProvider {
    fn now(&self) -> DateTime<Utc> {
        Utc::now()
    }
}
```
This is a zero-sized type (ZST) — no allocation, no indirection when used as the concrete type.
In tests, a struct holding a `DateTime<Utc>` field serves as `FakeTimeProvider`.

Candidate B — using `Arc<dyn TimeProvider>` at the `DecayScorer` construction site:
```rust
pub struct DecayScorer {
    time_provider: Arc<dyn TimeProvider>,
    curve: Box<dyn DecayCurve>,
    null_timestamp_score: f64,
}
```
Allows runtime swapping (hot-reload?) but adds one `Arc` per scorer instance.

Candidate C — generic `DecayScorer<T: TimeProvider>`:
```rust
pub struct DecayScorer<T: TimeProvider> {
    time_provider: T,
    curve: ...,
}
```
Monomorphization means no vtable overhead, but `DecayScorer<SystemTimeProvider>` and
`DecayScorer<FakeTimeProvider>` are different types — harder to store in a `Vec<Box<dyn Scorer>>`.

Candidate D — function pointer approach:
```rust
pub struct DecayScorer {
    time_fn: fn() -> DateTime<Utc>,
    ...
}
```
Simpler than a trait but not mockable with closures; also `fn` pointers can't capture state
(a fake-time provider needs to hold the fake instant somewhere).

The Send + Sync bounds matter: `DecayScorer` must be usable across async tasks. Any
`TimeProvider` impl must be `Send + Sync`.

### Proposal 1.2 — `Window(maxAge)` Binary vs Linear Falloff

`DecayCurve.Window(maxAge: TimeSpan)` as currently specified returns 1.0 for items within
`maxAge` of reference time and 0.0 for items older than `maxAge`.

Proposal: extend the signature to `Window(maxAge, falloff: Falloff = Falloff::Binary)`:
```
enum Falloff {
    Binary,           // existing behavior: 1.0 / 0.0
    Linear,           // linear interpolation from 1.0 at 0 to 0.0 at maxAge, then 0.0
    LinearTail(f64),  // linear down to a floor value instead of 0.0
}
```
This lets users say "items within 1 hour get full weight, items between 1 and 6 hours
decay linearly, items older than 6 hours get zero." Without this, you need a `Step` curve
with multiple entries to approximate the same shape.

Counter-thought: adding `Falloff` to `Window` makes `Window` a near-duplicate of
`Exponential` with different math. The three existing curves (Exponential, Step, Window)
may already cover the 95% case. Linear falloff is arguably just `Step` with 2 steps.

### Proposal 1.3 — Negative Age (Future-Dated Items)

When `item.timestamp > referenceTime`, the age is negative. Three choices:
- **Clamp to zero age**: treat it as age=0 → score = 1.0 for Exponential, 1.0 for Window.
  Clean, predictable, no special case needed in score computation.
- **Score = 1.0 explicit**: same result for most curves but explicit in spec as its own rule.
  Easier to document: "future-dated items are treated as maximally fresh."
- **Score = `nullTimestampScore`**: treat future timestamps as suspect (clock skew?) and
  apply the same neutral score as items with no timestamp. This is conservative.
- **Configurable**: `FutureDatedPolicy { TreatAsZeroAge, TreatAsMaxFresh, TreatAsNoTimestamp }`.
  Maximum flexibility, maximum spec surface.

The simplest coherent rule: clamp to zero age. Future-dated items are temporally fresh by
definition; clock skew is the caller's problem.

### Proposal 1.4 — Timestamp Coverage Note and Runtime Warning

With `ContextItem.timestamp` live and tested, the spec chapter can address what happens
when no included items have timestamps at all:
- `RecencyScorer` returns 0.0 for all items with no timestamp → scoring pass is meaningless.
- `DecayScorer` returns `nullTimestampScore` for all items with no timestamp → all items
  score identically (no differentiation), which defeats the scorer's purpose.

Question: should this be a runtime warning via `ITraceCollector` (or the Rust equivalent)?

Option A — `TraceEvent::ScorerWarning { scorer: "DecayScorer", message: "..." }`:
A new event variant. Potentially useful for diagnostics but adds a new `TraceEvent` variant
that all pattern-match arms must handle (even if they ignore it).

Option B — spec note only, no runtime signal:
"When all items have `null` timestamps, `DecayScorer` scores all items equally. This is not
an error; it is equivalent to uniform scoring." Let callers detect via `SelectionReport`.

Option C — emit only once via a `has_warned` flag on the scorer:
Thread-safety risk (mutating the scorer during `score()` calls). Unattractive.

Option D — integrate into `SelectionReport` as a coverage metric:
`SelectionReport.timestamp_coverage` = fraction of included items with non-null timestamps.
Pure metric, no warnings, callers decide what to do with the signal. This overlaps with
the "timestamp coverage metric" new idea below.

### Proposal 1.5 — Zero Half-Life in `Exponential(halfLife)`

`Exponential(halfLife: TimeSpan.Zero)` (or `Duration::ZERO` in Rust). What should happen?

Option A — throw at construction time:
`ArgumentException` (or Rust `Result::Err`) at `DecayCurve.Exponential(halfLife: Zero)`.
Clean fail-fast: zero half-life is mathematically undefined (log(2)/0).

Option B — return 0.0 for all non-zero ages at score time:
Treat it as "anything older than zero age scores 0.0." Only items at exactly `age==0`
(future-dated? same instant?) score 1.0. Mathematically it's the limit as half-life → 0.

Option C — return 1.0 for all ages:
The opposite limit. Counterintuitive.

Option D — clamp to a minimum half-life (e.g., 1 second):
Silent clamping is bad API design — hides user mistakes.

Best answer: throw at construction. Zero half-life is a caller error, not a runtime condition.
The spec should document this as a precondition: `halfLife > TimeSpan.Zero`.

### Proposal 1.6 — `nullTimestampScore` as a `DecayCurve`-level concern

Currently `nullTimestampScore` is proposed as a `DecayScorer` constructor parameter.
Alternative: make it a per-curve property, so `Exponential` and `Window` can have
different null policies:
```csharp
DecayCurve.Exponential(halfLife: TimeSpan.FromHours(6), nullScore: 0.5)
DecayCurve.Window(maxAge: TimeSpan.FromHours(1), nullScore: 0.0)
```
This gives finer control but makes the API surface larger. Most users will want one
null policy for the whole scorer, not per-curve. Keep it on `DecayScorer`.

### Proposal 1.7 — Timestamp Coverage Metric in `SelectionReport`

Now that `ContextItem.timestamp` is live, a metric is concrete:
`timestamp_coverage = included_items_with_timestamp / total_included_items`

Belongs in `SelectionReport` as a derived field, or as an extension method on the report,
rather than in the DecayScorer spec chapter. The DecayScorer chapter can _reference_ it
(what happens when coverage is 0.0), but the metric itself is a reporting concern.

Three placement options:
1. New field `SelectionReport.timestamp_coverage: f64` (or `Option<f64>` if total==0).
2. Extension method `report.TimestampCoverage()` in `Wollax.Cupel.Analytics` / `cupel` crate.
3. Document it as a formula callers can compute: `report.Included.Count(i => i.Timestamp != null) / (float)report.Included.Count`.

Option 3 is simplest. Option 1 adds API surface. Option 2 is the established pattern
(alongside `BudgetUtilization`, `KindDiversity`). Option 2 is probably right.

---

## Section 2 — OpenTelemetry Verbosity Angles

### Proposal 2.1 — Exact `StageOnly` Tier Attribute Names

The March 15 baseline established the tier structure. Now specify exact attribute names
for `StageOnly`:

Per-stage Activity / Span:
- `cupel.stage.name` (string): "Classify" | "Score" | "Deduplicate" | "Slice" | "Place"
  (Note: "Sort" stage was used in March 15 TraceEvent enum — confirm the stage name set)
- `cupel.stage.duration_ms` (float64): wall-clock milliseconds for this stage
- `cupel.stage.item_count_in` (int): items entering the stage
- `cupel.stage.item_count_out` (int): items exiting the stage

Budget attribute at StageOnly tier:
- `cupel.budget.max_tokens` (int): the budget ceiling

These appear on the root Activity for the whole pipeline run, not per-stage. Or on the
Slice stage Activity where the budget is actually enforced? Both could make sense.

### Proposal 2.2 — Exact `StageAndExclusions` Tier Additions

Per-excluded-item Event on the stage Activity:
- Event name: `"cupel.exclusion"`
- `cupel.exclusion.reason` (string): e.g. "BudgetExceeded", "NegativeScore", "Duplicate"
- `cupel.exclusion.item_kind` (string): `ContextKind` value
- `cupel.exclusion.item_tokens` (int): token count of excluded item

Per-stage summary attribute:
- `cupel.exclusion.count` (int): total excluded items in this stage

Alternative: emit exclusions as child spans (Activities) rather than Events. Events are
cheaper and don't consume trace tree depth. For exclusions (potentially 100+), events
are the right shape. Activities are for work units with measurable duration.

### Proposal 2.3 — Exact `Full` Tier Additions

Per-included-item Event:
- Event name: `"cupel.item.included"`
- `cupel.item.kind` (string): `ContextKind` value
- `cupel.item.tokens` (int): token count
- `cupel.item.score` (float64): final score after scoring stage
- `cupel.item.placement` (int or string): final position or "pinned"/"placed"

Cardinality consideration: 500 included items = 500 `cupel.item.included` events per
pipeline call. At 100 calls/sec, that is 50,000 events/sec. This is a cardinality risk.
The spec must document this explicitly and recommend `Full` only for development/debugging.

### Proposal 2.4 — Cardinality Warning Documentation

At which tier does cardinality become a risk?
- `StageOnly`: 5 Activities per pipeline call. Safe at any throughput.
- `StageAndExclusions`: 5 Activities + up to N exclusion events. Exclusion rate is bounded
  by item count. At 500 items, 300 excluded = 300 events/call. Medium risk.
- `Full`: 5 Activities + N exclusion events + M inclusion events. At 500 items, up to 500
  `cupel.item.included` events. High risk in production.

Recommendation: spec should include a cardinality guidance table:
| Tier | Events per call (typical) | Recommended environment |
|------|--------------------------|------------------------|
| StageOnly | 5–10 | Production |
| StageAndExclusions | 5 + 0–200 | Staging/debug |
| Full | 5 + 0–1000 | Development only |

### Proposal 2.5 — `cupel.budget.max_tokens` Placement

Where does the budget attribute appear?
Option A — root Activity only: one place, easy to query. Callers who want budget context
alongside stage timing can look at the root Activity.
Option B — Slice stage Activity only: this is where the budget constraint is applied.
Semantically cleaner but requires drilling into a child Activity.
Option C — all stage Activities: maximum redundancy, simplest queries, but 5x the data.

Recommendation: root Activity is the right home for pipeline-level config like budget.
Stage Activities carry per-stage metrics, not pipeline config.

### Proposal 2.6 — Attribute Naming: Stage Duration vs. Activity Duration

`cupel.stage.duration_ms` as an attribute duplicates information already in Activity's
start/end timestamps. OTEL best practice: use Activity's built-in duration rather than
recording it as an attribute. Attributes should carry semantic meaning that isn't already
captured in OTEL primitives.

Alternative: omit `cupel.stage.duration_ms` entirely and rely on Activity duration.
The spec should call this out explicitly so implementers don't double-record duration.

### Proposal 2.7 — Activity Name Convention

What is the Activity name for each stage?
Option A — `cupel.stage.{name}`: `cupel.stage.classify`, `cupel.stage.score`, etc.
Option B — `Cupel/{StageName}`: `Cupel/Classify`, `Cupel/Score`, etc. (REST-style)
Option C — The stage name directly: `"Classify"`, `"Score"`, etc.

`cupel.stage.{name}` is consistent with the attribute namespace and makes it easy to
filter all Cupel Activities in a trace backend. Recommended.

### Proposal 2.8 — Verbosity Tier on the Root Activity

Should the root Activity record which verbosity tier is active?
`cupel.verbosity` = "StageOnly" | "StageAndExclusions" | "Full"

Useful for post-hoc debugging: "why does this trace not have exclusion data?" → look at
`cupel.verbosity`. Small API surface, high diagnostic value.

---

## Section 3 — Budget Simulation Angles

### Proposal 3.1 — Multi-Budget `DryRun` Variant

`FindMinBudgetFor` as currently scoped calls `DryRun(items, budget)` ~10-15 times (binary
search). Each call runs the full pipeline DP. The question: is there value in specifying
`DryRun(items, budgets: int[])` that runs the pipeline once per budget value but potentially
shares setup work?

Shared work analysis: what does each `DryRun` call actually do?
1. Classify: O(N), budget-independent.
2. Score: O(N), budget-independent.
3. Deduplicate: O(N log N), budget-independent.
4. Sort: O(N log N), budget-independent.
5. Slice (KnapsackSlice): O(N × budget), budget-dependent.
6. Place: O(N), budget-independent.

Stages 1–4 and 6 are budget-independent. Only the Slice stage changes per budget.
A multi-budget variant could run Classify→Score→Deduplicate→Sort once, then run Slice
N times for N budgets. This is a meaningful optimization.

But: `DryRun` is a public API method. A multi-budget variant is a new method signature.
Does it warrant API surface for a use case (`FindMinBudgetFor`) that is itself an API
method? Or is this internal plumbing that `FindMinBudgetFor` can implement without
exposing a multi-budget `DryRun`?

Answer: `FindMinBudgetFor` as an extension method on `CupelPipeline` already has access
to the internals it needs (or can call `DryRun` 15 times, which is fine — 15 DP passes
on ≤500 items is still sub-millisecond). Over-engineering.

### Proposal 3.2 — `GetMarginalItems` Diff Direction

The March 15 spec says: "items in primary result but not in `DryRun(items, budget - slackTokens)` result."

Is this the right direction?
- Primary result: items selected at `budget.MaxTokens`.
- Margin result: items selected at `budget.MaxTokens - slackTokens`.
- Diff (Primary \ Margin): items that are included at full budget but would be dropped at
  reduced budget. These are the "marginal" items — items most at risk if budget shrinks.

This is the correct direction for the use case: "what would I lose if my budget shrunk by
`slackTokens` tokens?"

The reverse diff (Margin \ Primary) would be items in the smaller budget that are not in
the full budget — which makes no sense if inclusion is monotonic (more budget = at least
as many items).

Monotonicity caveat: with `QuotaSlice`, inclusion is NOT monotonic. More budget changes
quota percentages and can drop items that fit at smaller budgets. The spec must state that
`GetMarginalItems` assumes monotonic inclusion and guard/document accordingly.

### Proposal 3.3 — `FindMinBudgetFor` Search Bounds

What are the lower and upper bounds of the binary search?

Lower bound options:
- 1 token: technically valid but meaningless (no real item fits in 1 token)
- `targetItem.Tokens`: the minimum budget that could possibly include this item at all
- `targetItem.Tokens + overhead`: if other items must be selected first (pinned items consume
  budget), then `targetItem.Tokens` alone is insufficient

Recommendation: `targetItem.Tokens` as the lower bound is a clean precondition. The spec
should document: "SearchCeiling must be ≥ targetItem.Tokens; otherwise returns immediately
with the not-found result."

Upper bound: `searchCeiling` parameter, caller-supplied. Matches the March 15 spec.

Precondition for `targetItem`: must be in `items`. An item not in the input set can
never be selected regardless of budget. The spec should guard and throw on this.

### Proposal 3.4 — Non-Monotonicity Beyond `QuotaSlice`

The March 15 spec guards `FindMinBudgetFor` against `QuotaSlice`. But are there other
non-monotonic cases?

Custom slicers: a caller-implemented `ISlicer` / `Slicer` could be non-monotonic. The guard
checks `if (pipeline.Slicer is QuotaSlice)`, which doesn't cover custom slicers.

Options:
A — Guard only `QuotaSlice` (current spec): covers the built-in case, trusts the caller
    to know their custom slicer is monotonic. Practical.
B — General monotonicity contract: add a `bool IsMonotonic` property to `ISlicer` that
    implementations must declare. Then `FindMinBudgetFor` checks `if (!pipeline.Slicer.IsMonotonic)`.
    More principled but adds API surface that all existing and future slicers must implement.
C — No guard, pure documentation: state the monotonicity precondition in docs only.
    Fails silently if the caller's slicer is non-monotonic. Bad for production debugging.

Recommendation: guard `QuotaSlice` specifically with a clear error message, document
the general monotonicity contract, and defer `ISlicer.IsMonotonic` to v1.3.

`KnapsackSlice` is monotonic (greedy DP with more budget can only add items, never remove).
`GreedySlice` is also monotonic (greedy fill stops early but doesn't shuffle). So the only
built-in non-monotonic case is `QuotaSlice`.

### Proposal 3.5 — `FindMinBudgetFor` Return Type for "Not Found"

When no budget in `[lowerBound, searchCeiling]` includes `targetItem`, what should be returned?

Option A — throw `InvalidOperationException` with `"targetItem not found within searchCeiling"`:
Fail-fast, but forces callers to use try/catch for an arguably non-exceptional case.
"I asked for a budget and the answer is 'not achievable'" is a valid business outcome.

Option B — return `null` / `Option<int>`:
`int?` in C#, `Option<i32>` in Rust. Callers must handle the not-found case explicitly.
Idiomatic for "search that might not find anything."

Option C — return `int.MaxValue` / `i32::MAX`:
Sentinel value. Bad practice — callers who forget to check will get wildly wrong results.

Option D — return a `BudgetSearchResult { Found: bool, MinBudget: int }`:
Wrapper type. Explicit, no sentinel. Heavier than `int?`.

Recommendation: `int?` (C#) / `Option<i32>` (Rust). Nullable/option types are exactly
the right tool for "search that might not find a result." The spec should document what
"not found" means precisely: targetItem not selected at any budget ≤ searchCeiling.

### Proposal 3.6 — `FindMinBudgetFor` Binary Search Precision

Binary search converges but needs a stopping condition. Options:
A — Stop when `high - low <= 1`: precise but takes max log2(searchCeiling) iterations.
B — Stop when `high - low <= tokenGranularity`: allow caller to configure resolution.
    Makes sense if budgets are always multiples of 256 (typical GPU context windows).
C — Stop when `high == low`: exact.

For simplicity: stop when `high - low <= 1`, return the minimum budget where targetItem
is included. Document that the result is the exact minimum, not an approximation.

### Proposal 3.7 — `GetMarginalItems` Return Type

Currently: `IReadOnlyList<ScoredItem>`. But `ScoredItem` is a pair (ContextItem, score).
Does the marginal items result need to include scores?

Callers want to know: "these items are marginal." The score is the item's score from the
full-budget run. Including it allows callers to prioritize which marginal items to protect.
Keep `ScoredItem`.

But: should the return type also include the "dropped at margin budget" items that were
NOT in the primary result (i.e., items that only appear at the smaller budget but not
the full budget — which makes no sense for monotonic inclusion)? No, this case doesn't arise.

---

## Cross-Feature Angles

### Proposal X.1 — `SelectionReport.timestamp_coverage` as fourth analytics method

`BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` are established (Decision 4 + T02).
A fourth: `TimestampCoverage()` — fraction of included items with non-null timestamps.
Useful for DecayScorer adoption diagnostics. Belongs in the same analytics module.

### Proposal X.2 — OTel `cupel.scorer.type` attribute

At `StageOnly` tier, the Score stage Activity doesn't say which scorer was used.
Adding `cupel.scorer.type` = "RecencyScorer" | "DecayScorer" | "custom" helps correlate
score-stage latency with scorer complexity. Small addition to the `StageOnly` attribute set.

### Proposal X.3 — `DryRun` result stability guarantee

`FindMinBudgetFor` and `GetMarginalItems` both assume that calling `DryRun` twice with the
same budget produces the same result (determinism). Is this currently guaranteed?
The spec should state whether `DryRun` is deterministic. If tie-breaking in sorting or
deduplication is non-deterministic, binary search results are unreliable. This is a spec
invariant that must be stated.
