# Phase 5: Pipeline Assembly & Basic Execution - Research

**Researched:** 2026-03-13
**Confidence:** HIGH (primary source is existing codebase with 4 completed phases)

---

## Standard Stack

No new external libraries required. Phase 5 uses only existing project infrastructure:

| Component | Source | Notes |
|-----------|--------|-------|
| TUnit | Existing | Test framework (Phase 1) |
| BenchmarkDotNet | Existing | `[MemoryDiagnoser]` for zero-alloc verification (Phase 1) |
| PublicApiAnalyzers | Existing | All new public types must update `PublicAPI.Unshipped.txt` |
| System.Diagnostics | BCL | `Stopwatch` for stage timing in pipeline execution |

**Confidence: HIGH** -- No new dependencies needed.

---

## Architecture Patterns

### 1. Interface Signatures (Exact, From Codebase)

The pipeline must wire these existing interfaces in fixed order. Exact signatures from the codebase:

```csharp
// IScorer: item + full candidate set -> score
double Score(ContextItem item, IReadOnlyList<ContextItem> allItems);

// ISlicer: scored items (score-descending) + budget + trace -> selected items
IReadOnlyList<ContextItem> Slice(
    IReadOnlyList<ScoredItem> scoredItems,
    ContextBudget budget,
    ITraceCollector traceCollector);

// IPlacer: scored items + trace -> ordered items
IReadOnlyList<ContextItem> Place(
    IReadOnlyList<ScoredItem> items,
    ITraceCollector traceCollector);
```

**Critical wiring detail:** `ISlicer.Slice` returns `IReadOnlyList<ContextItem>` (scores stripped), but `IPlacer.Place` requires `IReadOnlyList<ScoredItem>`. The pipeline must re-associate scores with slicer output. Two approaches:

- **Option A:** Build a `Dictionary<ContextItem, double>` lookup from the pre-slice scored items, then reconstruct `ScoredItem[]` for the placer using reference equality lookup. Cost: one dictionary allocation per execution.
- **Option B:** Use the sorted `ScoredItem[]` from before slicing and filter it to only include items the slicer returned (via `HashSet<ContextItem>` with `ReferenceEqualityComparer`). Cost: one hashset allocation per execution.

**Recommendation: Option B** -- The `HashSet` approach preserves the original score values exactly and avoids dictionary overhead. Use `ReferenceEqualityComparer.Instance` (already used in `CompositeScorer` cycle detection) for O(1) lookups.

**Confidence: HIGH** -- Signatures read directly from source files.

### 2. Pipeline Data Flow

```
Input: IReadOnlyList<ContextItem> or IContextSource
  |
  v
[Classify] --> (pinned: ContextItem[], scoreable: ContextItem[])
  |                                                    |
  v                                                    |
[Score] --> ScoredItem[] (from scoreable only)         |
  |                                                    |
  v                                                    |
[Deduplicate] --> ScoredItem[] (highest-score wins)    |
  |                                                    |
  v                                                    |
[Sort] --> ScoredItem[] (score descending, stable)     |
  |                                                    |
  v                                                    |
[Slice] --> ContextItem[] (within budget)              |
  |                                                    |
  v                                                    |
[Re-associate scores] --> ScoredItem[]                 |
  |                                                    |
  v                                                    |
[Merge pinned] --> ScoredItem[] (pinned @ score 1.0) <-+
  |
  v
[Place] --> ContextItem[] (final order)
  |
  v
Output: ContextResult { Items, TotalTokens, Report? }
```

### 3. Stable Sort Pattern (From Phase 4 State)

Confirmed in STATE.md: `(double Score, int Index)` tuple array with `Array.Sort` and static comparison delegate. Phase 5 adopts this for score-descending sorting before Slice:

```csharp
// Pre-allocate array of (score, originalIndex) tuples
var sortKeys = new (double Score, int Index)[scoredItems.Length];
for (var i = 0; i < scoredItems.Length; i++)
    sortKeys[i] = (scoredItems[i].Score, i);

// Sort descending by score, stable via index tiebreaker
Array.Sort(sortKeys, static (a, b) =>
{
    var cmp = b.Score.CompareTo(a.Score); // descending
    return cmp != 0 ? cmp : a.Index.CompareTo(b.Index); // stable
});
```

**Confidence: HIGH** -- Pattern established in Phase 4 tests, STATE.md explicitly says "Phase 5 pipeline will adopt."

### 4. Zero-Allocation Discipline

Established patterns from Phases 3-4 that Phase 5 must follow:

- **For loops with indexer access only** in hot paths (no LINQ, no foreach, no closures)
- **`if (traceCollector.IsEnabled)`** guard before constructing `TraceEvent` structs (from `TraceGatingBenchmark.cs`)
- **Pre-allocated arrays** for intermediate results (not `List<T>`)
- **`ReferenceEqualityComparer.Instance`** for identity-based collections (no `GetHashCode` overhead)
- **Static comparison delegates** for `Array.Sort` (no closure capture)

The pipeline's Execute method will allocate arrays for intermediate stages. The zero-alloc target applies to the tracing-disabled path where `NullTraceCollector.Instance` is used -- the benchmark must show zero Gen0 allocations on that path.

**Confidence: HIGH** -- Patterns observed across `CompositeScorer`, `ScaledScorer`, `RecencyScorer`, `TraceGatingBenchmark`.

### 5. Builder Pattern Shape

```csharp
public sealed class CupelPipeline
{
    public static PipelineBuilder CreateBuilder() => new();

    // Execute overloads
    public ContextResult Execute(IReadOnlyList<ContextItem> items);
    public async Task<ContextResult> ExecuteAsync(IContextSource source,
        CancellationToken cancellationToken = default);
}

public sealed class PipelineBuilder
{
    // Required
    public PipelineBuilder WithBudget(ContextBudget budget);

    // Mutually exclusive scoring paths
    public PipelineBuilder WithScorer(IScorer scorer);
    public PipelineBuilder AddScorer(IScorer scorer, double weight);

    // Optional with defaults
    public PipelineBuilder WithSlicer(ISlicer slicer);       // default: GreedySlice
    public PipelineBuilder WithPlacer(IPlacer placer);       // default: ChronologicalPlacer
    public PipelineBuilder WithDeduplication(bool enabled);   // default: true
    public PipelineBuilder WithTraceCollector(ITraceCollector collector);

    // Terminal
    public CupelPipeline Build();
}
```

`Build()` validates:
1. Budget is set (throw `InvalidOperationException`)
2. At least one scorer configured (throw `InvalidOperationException`)
3. `WithScorer` and `AddScorer` not mixed (throw `InvalidOperationException`)
4. If `AddScorer` used, constructs `CompositeScorer` internally

**Confidence: HIGH** -- Shape locked in CONTEXT.md decisions.

### 6. Namespace and File Organization

Following existing conventions:
- `CupelPipeline.cs` and `PipelineBuilder.cs` in root namespace `Wollax.Cupel`
- `GreedySlice.cs` in root namespace (matches `ISlicer` location)
- `UShapedPlacer.cs` and `ChronologicalPlacer.cs` in root namespace (matches `IPlacer` location)
- Internal pipeline stages (classify, dedup) as private methods on `CupelPipeline` or internal helper classes

**Confidence: HIGH** -- Existing scorers are in `Wollax.Cupel.Scoring` because they form a cohesive group. Slicers and placers are first-class pipeline components like interfaces, belonging in the root namespace.

---

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---------|-------------|-----|
| Weighted scorer composition | `CompositeScorer` (existing) | Builder's `AddScorer` path creates a `CompositeScorer` internally |
| String hashing for dedup | `StringComparer.Ordinal` via `Dictionary<string, ScoredItem>` | BCL optimized, zero-allocation key comparison |
| Timing pipeline stages | `Stopwatch.GetTimestamp()` / `Stopwatch.GetElapsedTime()` | .NET 7+ static methods, no Stopwatch allocation |
| Null trace collection | `NullTraceCollector.Instance` (existing singleton) | Already established pattern |
| Trace event construction | `TraceEvent` readonly record struct with required init | Stack-allocated, existing pattern |

**Confidence: HIGH**

---

## Common Pitfalls

### 1. ISlicer Returns ContextItem, IPlacer Needs ScoredItem
The interface contract gap between `ISlicer.Slice` (returns `IReadOnlyList<ContextItem>`) and `IPlacer.Place` (takes `IReadOnlyList<ScoredItem>`) requires score re-association. Forgetting this produces a compile error, but the *efficient* solution (HashSet with ReferenceEqualityComparer) is non-obvious.

### 2. Pinned Items Must Not Enter Scorer
`ContextItem.Pinned` is `bool` (not `IsPinned` -- confirmed from source). Pinned items bypass Score, Dedup, and Slice stages entirely. They merge back at Placer with effective score 1.0. If pinned items are accidentally scored, the scorer's `allItems` parameter would include them, skewing relative rankings.

**Key decision:** The `allItems` parameter passed to `IScorer.Score` should contain only scoreable items (not pinned). Pinned items are not candidates for scoring, so including them would distort rank-based scorers like `RecencyScorer` and `PriorityScorer` that count relative positions.

### 3. Dedup Must Happen After Scoring
Dedup keeps the highest-scored duplicate. If dedup ran before scoring, there would be no scores to compare. The pipeline order is: Score -> Dedup -> Sort -> Slice.

### 4. ContextBudget.TargetTokens vs MaxTokens for Slicing
`ContextBudget` has both `TargetTokens` (soft goal) and `MaxTokens` (hard limit). `GreedySlice` should fill to `TargetTokens`, not `MaxTokens`. The `MaxTokens` limit applies as a hard ceiling. The `OutputReserve` must be subtracted from `MaxTokens` for the effective ceiling.

### 5. Benchmark Must Use [Params(100, 250, 500)]
Success criteria specifies "100/250/500 items completes under 1ms". The existing `EmptyPipelineBenchmark` already uses `[Params(100, 250, 500)]` as the template.

### 6. Array.Sort Stability
`Array.Sort` is not stable in .NET. The `(Score, Index)` tuple pattern from Phase 4 is required for deterministic output. Without the index tiebreaker, equal-scored items produce non-deterministic ordering, failing tests.

### 7. ContextItem.Tokens Has No Validation
Per open issue `001-contextitem-tokens-negative.md`, `Tokens` can be zero or negative. `GreedySlice` must handle zero-token items (they're "free" to include) and should skip negative-token items or treat them as zero. Decide in classify validation.

### 8. IContextSource Is Async, Pipeline Is Sync
`IContextSource.GetItemsAsync` is async. The `Execute(IContextSource)` overload must be async. The `Execute(IReadOnlyList<ContextItem>)` overload is synchronous. All pipeline stages (Score, Slice, Place) are synchronous by interface contract.

### 9. PublicAPI.Unshipped.txt Must Be Updated
All new public types (CupelPipeline, PipelineBuilder, GreedySlice, UShapedPlacer, ChronologicalPlacer) must be added to `PublicAPI.Unshipped.txt` or the build will produce analyzer warnings.

**Confidence: HIGH** -- All pitfalls derived from reading actual source code.

---

## Claude's Discretion Recommendations

### 1. Tracing Configuration: Builder-level (not per-execution)

**Recommendation:** `.WithTraceCollector(ITraceCollector)` on the builder.

**Evidence:**
- `DiagnosticTraceCollector` doc says "create one collector per execution" but also "not thread-safe"
- The pipeline object itself should be reusable (build once, execute many)
- If trace collector is on the builder, the pipeline stores it as a field

**Problem:** If the pipeline is reused across executions, a single `DiagnosticTraceCollector` accumulates events from all executions. This violates the "one per execution" guidance.

**Revised recommendation:** Default to `NullTraceCollector.Instance` on the pipeline. Provide an `Execute` overload or optional parameter for per-execution trace collectors:

```csharp
public ContextResult Execute(IReadOnlyList<ContextItem> items,
    ITraceCollector? traceCollector = null);
```

This way the pipeline is reusable, tracing is opt-in per execution, and `NullTraceCollector` is the zero-cost default. The builder does NOT configure tracing -- execution does.

**Confidence: HIGH** -- Follows `DiagnosticTraceCollector`'s own documentation about per-execution lifecycle.

### 2. Dedup String Comparison: Exact Ordinal

**Recommendation:** `StringComparer.Ordinal` (exact byte-for-byte comparison).

**Evidence:**
- Zero-allocation discipline: ordinal comparison is the fastest string comparison, no Unicode normalization overhead
- `Content` is the dedup key per CONTEXT.md decisions
- Normalized comparison (case-insensitive, Unicode normalization) would introduce ambiguity: "Hello" vs "hello" are semantically different in LLM context
- `ContextKind` uses `OrdinalIgnoreCase` because kind names are identifiers; content is user data where case matters

Use `Dictionary<string, int>(StringComparer.Ordinal)` where the value is the index into the scored items array of the highest-scoring duplicate.

**Confidence: HIGH**

### 3. Pinned Items Budget Behavior: Pinned Tokens Consume Budget

**Recommendation:** Pinned items consume budget tokens. The slicer's effective budget is `TargetTokens - pinnedTokensTotal`.

**Evidence:**
- `ContextBudget` models a real constraint (model context window). Pinned items still occupy tokens in the actual context window.
- Making pinned items "free" would allow users to pin items exceeding the model's context window, which is physically impossible.
- The slicer must account for pinned tokens when filling the remaining budget.

**Implementation:** Before calling `ISlicer.Slice`, compute `pinnedTokensTotal`. Create an adjusted budget or pass the remaining token count to the slicer. Since `ContextBudget` is a sealed class (not record, no `with`), create a new `ContextBudget` with reduced `TargetTokens` and `MaxTokens`.

**Confidence: HIGH**

### 4. Pinned Overflow Handling: Throw InvalidOperationException

**Recommendation:** If pinned items alone exceed `MaxTokens - OutputReserve`, throw `InvalidOperationException` at execution time (not build time, since items aren't known at build time).

**Evidence:**
- Existing validation patterns in the codebase consistently throw (see `ContextBudget` constructor, `CompositeScorer` constructor) rather than silently degrading
- A pinned overflow means the user's configuration is fundamentally broken -- silent truncation would produce incorrect behavior
- `InvalidOperationException` is appropriate because the pipeline was built correctly but the input data is invalid for the configured budget

**Confidence: HIGH**

### 5. Classify Validation: Skip-and-Trace for Invalid Items

**Recommendation:** Skip items with `Tokens <= 0` and trace the exclusion, rather than throwing.

**Evidence:**
- The pipeline processes user-provided collections that may contain imperfect data
- Throwing on a single bad item would abort the entire pipeline, which is disproportionate
- The trace system already has `ExclusionReason` enum (Phase 2) for exactly this purpose
- Construction-time validation (ContextBudget, CompositeScorer) throws because invalid config is a programming error. Runtime data validation should be lenient.
- Items with `Tokens == 0` are arguably valid (metadata-only items) -- include them but they consume no budget
- Items with `Tokens < 0` are invalid -- skip and trace

**Refined rule:**
- `Tokens < 0`: skip, trace with a new exclusion reason or generic trace event
- `Tokens == 0`: include (free items, consume no budget)
- `Content` is `required string` so null is prevented at compile time; empty string is valid

**Confidence: MEDIUM** -- No existing precedent for runtime data validation; this is a judgment call.

---

## Code Examples

### GreedySlice Algorithm

O(N log N) greedy fill by score/token ratio. Input is already sorted by score descending (per `ISlicer` contract doc). The greedy approach re-sorts by value density (score/tokens) to maximize total score within budget.

```csharp
public sealed class GreedySlice : ISlicer
{
    public IReadOnlyList<ContextItem> Slice(
        IReadOnlyList<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector)
    {
        // Effective budget: TargetTokens is the soft goal
        var remainingTokens = budget.TargetTokens;

        if (scoredItems.Count == 0 || remainingTokens <= 0)
            return [];

        // Sort by value density (score/tokens ratio) descending
        // Use (density, originalIndex) for stable sort
        var indices = new (double Density, int Index)[scoredItems.Count];
        for (var i = 0; i < scoredItems.Count; i++)
        {
            var tokens = scoredItems[i].Item.Tokens;
            // Zero-token items have infinite density (always include)
            var density = tokens > 0
                ? scoredItems[i].Score / tokens
                : double.MaxValue;
            indices[i] = (density, i);
        }

        Array.Sort(indices, static (a, b) =>
        {
            var cmp = b.Density.CompareTo(a.Density); // descending
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index); // stable
        });

        // Greedy fill
        var selected = new List<ContextItem>();
        for (var i = 0; i < indices.Length; i++)
        {
            var idx = indices[i].Index;
            var item = scoredItems[idx].Item;
            var tokens = item.Tokens;

            if (tokens <= 0 || tokens <= remainingTokens)
            {
                selected.Add(item);
                remainingTokens -= Math.Max(0, tokens);
            }
        }

        return selected;
    }
}
```

**Note:** The `List<ContextItem>` allocation is acceptable because `Slice` runs once per execution, not in a hot inner loop. The zero-alloc discipline applies to per-item operations within the loop.

**Confidence: HIGH** -- Classic fractional knapsack greedy algorithm adapted for integer items.

### UShapedPlacer Algorithm

Places highest-scoring items at edges (start and end) to exploit primacy and recency attention effects.

```csharp
public sealed class UShapedPlacer : IPlacer
{
    public IReadOnlyList<ContextItem> Place(
        IReadOnlyList<ScoredItem> items,
        ITraceCollector traceCollector)
    {
        if (items.Count <= 2)
            return ExtractItems(items);

        // Sort by score descending (stable)
        var sorted = new (double Score, int Index)[items.Count];
        for (var i = 0; i < items.Count; i++)
            sorted[i] = (items[i].Score, i);

        Array.Sort(sorted, static (a, b) =>
        {
            var cmp = b.Score.CompareTo(a.Score);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        // Alternate placement: highest at start, next at end, etc.
        var result = new ContextItem[items.Count];
        var left = 0;
        var right = items.Count - 1;

        for (var i = 0; i < sorted.Length; i++)
        {
            var originalIdx = sorted[i].Index;
            if (i % 2 == 0)
                result[left++] = items[originalIdx].Item;
            else
                result[right--] = items[originalIdx].Item;
        }

        return result;
    }
}
```

**Confidence: MEDIUM** -- The alternating placement is the standard U-shaped attention approach. The exact interleaving strategy may need tuning during implementation.

### ChronologicalPlacer Algorithm

Orders by timestamp. Items without timestamps go to the end (preserving original order among them).

```csharp
public sealed class ChronologicalPlacer : IPlacer
{
    public IReadOnlyList<ContextItem> Place(
        IReadOnlyList<ScoredItem> items,
        ITraceCollector traceCollector)
    {
        if (items.Count <= 1)
            return ExtractItems(items);

        var sorted = new (DateTimeOffset? Timestamp, int Index)[items.Count];
        for (var i = 0; i < items.Count; i++)
            sorted[i] = (items[i].Item.Timestamp, i);

        Array.Sort(sorted, static (a, b) =>
        {
            // Null timestamps sort to end
            if (!a.Timestamp.HasValue && !b.Timestamp.HasValue)
                return a.Index.CompareTo(b.Index); // stable
            if (!a.Timestamp.HasValue) return 1;
            if (!b.Timestamp.HasValue) return -1;
            var cmp = a.Timestamp.Value.CompareTo(b.Timestamp.Value);
            return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
        });

        var result = new ContextItem[items.Count];
        for (var i = 0; i < sorted.Length; i++)
            result[i] = items[sorted[i].Index].Item;

        return result;
    }
}
```

**Confidence: HIGH** -- Straightforward timestamp sort with null handling.

### Deduplication (Internal Pipeline Stage)

```csharp
// After scoring, before sort/slice
// Key: Content string (ordinal), Value: index of highest-scoring duplicate
var bestByContent = new Dictionary<string, int>(scoredItems.Length, StringComparer.Ordinal);

for (var i = 0; i < scoredItems.Length; i++)
{
    var content = scoredItems[i].Item.Content;
    if (bestByContent.TryGetValue(content, out var existingIdx))
    {
        if (scoredItems[i].Score > scoredItems[existingIdx].Score)
            bestByContent[content] = i;
    }
    else
    {
        bestByContent[content] = i;
    }
}

// Build deduplicated array
var deduped = new ScoredItem[bestByContent.Count];
var j = 0;
foreach (var idx in bestByContent.Values)
    deduped[j++] = scoredItems[idx];
```

**Confidence: HIGH**

### Pipeline Execute (Synchronous Path)

```csharp
public ContextResult Execute(
    IReadOnlyList<ContextItem> items,
    ITraceCollector? traceCollector = null)
{
    var trace = traceCollector ?? NullTraceCollector.Instance;

    // 1. Classify: partition pinned vs scoreable
    var (pinned, scoreable) = Classify(items, trace);

    // 2. Score: only scoreable items
    var scored = Score(scoreable, trace);

    // 3. Deduplicate (if enabled)
    if (_deduplicationEnabled)
        scored = Deduplicate(scored, trace);

    // 4. Sort by score descending (stable)
    SortByScoreDescending(scored);

    // 5. Slice: select within adjusted budget
    var adjustedBudget = AdjustBudgetForPinned(pinned);
    var sliced = _slicer.Slice(scored, adjustedBudget, trace);

    // 6. Re-associate scores for placer
    var slicedWithScores = ReassociateScores(sliced, scored);

    // 7. Merge pinned items (score 1.0) with sliced items
    var merged = MergePinned(pinned, slicedWithScores);

    // 8. Place: final ordering
    var placed = _placer.Place(merged, trace);

    // 9. Build result
    return new ContextResult
    {
        Items = placed,
        Report = trace is DiagnosticTraceCollector dtc
            ? new SelectionReport { Events = dtc.Events }
            : null
    };
}
```

**Confidence: HIGH**

---

## Open Questions (Resolved)

### Q: Should the pipeline be a class or struct?
**A: Sealed class.** It holds references to scorer/slicer/placer and is created via builder. Following `ContextBudget` pattern (sealed class, not record) since it has no value-equality semantics and should not be copied via `with`.

### Q: Should `Execute(IReadOnlyList<ContextItem>)` be sync or async?
**A: Sync.** All pipeline stages (Score, Slice, Place) are synchronous. Only the `IContextSource` overload needs to be async (to await `GetItemsAsync`). This gives callers the optimal execution model for each scenario.

### Q: How does `Execute(IContextSource)` work?
**A: Async wrapper.** It awaits `source.GetItemsAsync()` to materialize the collection, then calls the sync `Execute(IReadOnlyList<ContextItem>)`. This keeps all pipeline logic in one synchronous path.

### Q: Where does budget adjustment for pinned items happen?
**A: Inside Execute, before calling Slice.** Compute total pinned tokens, create a new `ContextBudget` with reduced targets, pass to slicer. Validate that pinned tokens don't exceed the hard ceiling (`MaxTokens - OutputReserve`).

---

## Test Strategy Notes

- **Builder validation tests:** Missing budget throws, missing scorer throws, mixed scoring paths throw, valid config succeeds
- **Pipeline stage ordering:** Use a custom IScorer/ISlicer/IPlacer that records invocation order to verify Classify->Score->Dedup->Slice->Place
- **Pinned bypass:** Items with `Pinned = true` appear in output regardless of what scorer returns
- **GreedySlice correctness:** Known inputs with known optimal selection, verify selected items and total tokens
- **UShapedPlacer correctness:** Verify highest-scored items appear at edges
- **ChronologicalPlacer correctness:** Verify output is timestamp-ordered
- **Dedup:** Duplicate content items, verify only highest-scored survives
- **Benchmark:** 100/250/500 items, full pipeline, under 1ms, zero Gen0 allocs with NullTraceCollector

---

*Phase: 05-pipeline-assembly-basic-execution*
*Research completed: 2026-03-13*
