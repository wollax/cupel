# Phase 4: Composite Scoring — Research

**Completed:** 2026-03-13
**Confidence:** HIGH overall (pure .NET, no external deps, well-understood algorithms)

---

## Standard Stack

No external dependencies. Everything is built with:
- **System.Collections.Frozen** — `FrozenDictionary` for immutable lookup tables (existing pattern from KindScorer, TagScorer)
- **System.Runtime.InteropServices** — `CollectionsMarshal.AsSpan()` for zero-copy access to `List<T>` backing arrays when needed
- **Array.Sort / List.Sort** with index-augmented comparer for stable-sort emulation (see Architecture Patterns)

---

## Architecture Patterns

### 1. CompositeScorer: Weighted Average with Relative Normalization

**Pattern:** Pre-compute normalized weights at construction time. Store as a `double[]` parallel to the `IScorer[]` array. The `Score()` hot path is a simple multiply-accumulate loop.

```csharp
public sealed class CompositeScorer : IScorer
{
    private readonly IScorer[] _scorers;
    private readonly double[] _normalizedWeights;

    public CompositeScorer(IReadOnlyList<(IScorer Scorer, double Weight)> entries)
    {
        // 1. Validate: non-null, non-empty, weights positive+finite
        // 2. Cycle detection (see below)
        // 3. Compute sum, normalize each weight: w[i] / sum
        // 4. Store as arrays (not List<T>) for zero-allocation iteration
    }

    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        var result = 0.0;
        for (var i = 0; i < _scorers.Length; i++)
            result += _scorers[i].Score(item, allItems) * _normalizedWeights[i];
        return result;
    }
}
```

**Weight normalization formula:** `normalizedWeight[i] = weight[i] / totalWeight`
where `totalWeight = sum(weight[0..n])`. This makes weights relative — `(2, 1)` is identical to `(0.6, 0.3)`. The formula is `sum(score_i * w_i) / sum(w_i)`, which simplifies to `sum(score_i * normalizedW_i)` when pre-normalized.

**Confidence:** HIGH — standard weighted average, used identically by TagScorer already.

### 2. Cycle Detection via DFS at Construction Time

**Pattern:** DFS with three-color marking (White/Gray/Black). Walk the scorer DAG at construction time. If a `CompositeScorer` is encountered during traversal and is already Gray (in current path), throw `ArgumentException`.

```csharp
private static void DetectCycles(IScorer root)
{
    var visited = new HashSet<IScorer>(ReferenceEqualityComparer.Instance);
    var inPath = new HashSet<IScorer>(ReferenceEqualityComparer.Instance);
    DetectCyclesCore(root, visited, inPath);
}

private static void DetectCyclesCore(IScorer node, HashSet<IScorer> visited, HashSet<IScorer> inPath)
{
    if (!visited.Add(node)) // Already fully processed
    {
        if (inPath.Contains(node))
            throw new ArgumentException($"Cycle detected: scorer {node.GetType().Name} appears in its own dependency graph.");
        return;
    }
    inPath.Add(node);

    if (node is CompositeScorer composite)
    {
        for (var i = 0; i < composite._scorers.Length; i++)
            DetectCyclesCore(composite._scorers[i], visited, inPath);
    }
    // ScaledScorer also wraps an IScorer — must be traversed
    if (node is ScaledScorer scaled)
        DetectCyclesCore(scaled.Inner, visited, inPath);

    inPath.Remove(node);
}
```

**Key decisions:**
- Use `ReferenceEqualityComparer.Instance` — identity-based, not value-based. Two different `RecencyScorer` instances should not be considered the same node.
- Time complexity: O(V + E) where V = number of unique scorer instances, E = parent-child edges. Trivially fast for realistic scorer trees (< 100 nodes).
- Only `CompositeScorer` and `ScaledScorer` have children to traverse. Leaf scorers (RecencyScorer, etc.) terminate recursion.

**Confidence:** HIGH — textbook DFS cycle detection, well-understood algorithm.

### 3. ScaledScorer: Min-Max Normalization Wrapper

**Pattern:** ScaledScorer wraps any `IScorer` and rescales its output to [0, 1] using min-max normalization across the candidate set. This requires a **two-pass approach** within the `Score()` method: first pass computes min/max across all items, second pass (implicit — just the formula) normalizes the current item's score.

```csharp
public sealed class ScaledScorer : IScorer
{
    private readonly IScorer _inner;

    public ScaledScorer(IScorer inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        // Cycle detection if inner is CompositeScorer containing this
        _inner = inner;
    }

    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        var rawScore = _inner.Score(item, allItems);

        // Find min and max across all items
        var min = double.MaxValue;
        var max = double.MinValue;
        for (var i = 0; i < allItems.Count; i++)
        {
            var s = _inner.Score(allItems[i], allItems);
            if (s < min) min = s;
            if (s > max) max = s;
        }

        // Degenerate case: all scores identical
        if (max - min < double.Epsilon)
            return 0.5; // or 1.0 — see Pitfalls

        return (rawScore - min) / (max - min);
    }
}
```

**Confidence:** HIGH for the algorithm. MEDIUM for degenerate case behavior — must decide: return 0.5 (neutral) or 1.0 (max). Recommend **0.5** as it is the midpoint of [0, 1] and does not artificially inflate scores.

### 4. Stable Sort via Index-Augmented Comparison

**Problem:** .NET's `Array.Sort` and `List<T>.Sort` are **unstable** (introsort). LINQ's `OrderBy` is stable but allocates (closures, iterators, `ToList()`). There is no `MemoryExtensions.StableSort` in .NET 10 (proposal #60982 is still open/Future milestone).

**Solution:** Decorate-sort-undecorate pattern. Augment each element with its original index, sort by (score DESC, index ASC), then strip the index. This converts an unstable sort into a stable one with zero semantic ambiguity.

```csharp
// At score time, build (score, originalIndex) pairs
var scored = new (double Score, int Index)[items.Count];
for (var i = 0; i < items.Count; i++)
    scored[i] = (ComputeScore(items[i]), i);

// Sort: primary by score descending, secondary by index ascending (preserves insertion order)
Array.Sort(scored, static (a, b) =>
{
    var cmp = b.Score.CompareTo(a.Score); // descending
    return cmp != 0 ? cmp : a.Index.CompareTo(b.Index); // ascending index for stability
});
```

**Key insight:** The tiebreaker is insertion index (position in the input list), not timestamp. This provides:
- **Within-run determinism:** Guaranteed — same input always produces same output.
- **Cross-run determinism:** Guaranteed if input order is consistent.
- No dependency on `ContextItem.Timestamp` which may be null.

**Where this lives:** This is pipeline-level sorting, not CompositeScorer's responsibility. CompositeScorer only computes a scalar score. The pipeline (Phase 5) will sort scored items. However, Phase 4 success criteria require verifying stable sort behavior, so the test infrastructure needs to demonstrate it works.

**Confidence:** HIGH — standard technique, no allocations beyond the scored array (which the pipeline needs anyway).

### 5. Zero-Allocation Discipline in Score() Methods

**Rules (carried forward from Phase 3):**
- No LINQ in `Score()` — for-loops only
- No closures / lambda captures
- No boxing (value types through `object`)
- No `string` allocations
- Store child scorers as `IScorer[]` (array), not `List<IScorer>` — avoids `List<T>` enumerator struct boxing through `IEnumerable<T>`
- Store weights as `double[]` parallel array, not `Dictionary` or tuples

**CompositeScorer.Score() allocation profile:** Zero. It's a for-loop multiplying and accumulating doubles.

**ScaledScorer.Score() allocation profile:** Zero heap allocations. But it calls `_inner.Score()` N+1 times per item (once for min/max scan, once for the actual score). This is O(N) per item, O(N^2) total when scoring all items. This is acceptable for typical candidate set sizes (< 1000 items) but worth documenting.

**Confidence:** HIGH — follows established patterns from Phase 3 scorers.

---

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---------|-------------|-----|
| Stable sorting | Index-augmented `Array.Sort` | .NET has no built-in stable sort without allocation; the decorate-sort pattern is O(1) extra work per comparison |
| Weight normalization | Simple division by sum | Don't build a "normalization engine" — it's one division per weight at construction time |
| Cycle detection | DFS with `HashSet<IScorer>` + `ReferenceEqualityComparer.Instance` | Don't pull in a graph library — the scorer tree is trivially small |
| Immutable scorer arrays | `IScorer[]` stored in readonly field | Don't use `ImmutableArray<T>` — it's a struct wrapper that adds complexity without benefit here since the array is private and never exposed |
| Frozen lookups | `FrozenDictionary` (existing pattern) | Continue using for any dictionary that's built once at construction |

---

## Common Pitfalls

### P1: Weight Sum of Zero
If all weights are zero, `totalWeight` is 0 and normalization divides by zero. **Guard:** Reject all-zero weights at construction time with `ArgumentException`. At least one weight must be positive.

### P2: ScaledScorer Degenerate Case (All Identical Scores)
When `max == min`, the normalization formula divides by zero. **Guard:** Check `max - min` and return a constant (0.5 recommended). Do NOT use `double.Epsilon` as the threshold — use a relative comparison or simply `max == min` since these are exact doubles from the same scorer.

### P3: Floating-Point Comparison in Stable Sort
When comparing scores for sort ordering, use `double.CompareTo()` (not `==` or `<`). `CompareTo` handles NaN correctly (NaN sorts before all other values), though scorers should never return NaN per the IScorer contract.

### P4: ScaledScorer Performance — O(N^2) Total
`ScaledScorer.Score()` must scan all items to find min/max. When the pipeline scores N items, each call to `ScaledScorer.Score()` does N inner scorer calls. Total: O(N^2) inner scorer invocations. For N=500 this is 250K calls — fast for simple scorers but worth benchmarking if ScaledScorer wraps a CompositeScorer wrapping expensive scorers.

### P5: Cycle Detection Must Cover ScaledScorer
ScaledScorer wraps an IScorer. If someone does `new CompositeScorer([(new ScaledScorer(compositeRef), 1.0)])` where `compositeRef` is the composite being constructed, the cycle goes through ScaledScorer. Cycle detection must walk into ScaledScorer's inner scorer, not just CompositeScorer children.

### P6: NaN/Infinity Propagation
If a child scorer returns NaN or Infinity (violating the IScorer contract), the weighted sum propagates the corruption. **Guard options:**
- Defensive: clamp/filter child scores in CompositeScorer (adds per-call overhead)
- Offensive: trust the contract, document that child scorers must return finite values
- **Recommendation:** Offensive approach — match existing pattern. No existing scorer validates its peers' output. Add a debug-only assertion if desired.

### P7: Single-Scorer CompositeScorer
A CompositeScorer with one child is valid and useful (it's the identity function for that scorer). Don't reject it — minimum child count should be 1, not 2.

### P8: Constructor Self-Reference
The most likely cycle is passing the CompositeScorer being constructed to itself. Since C# doesn't allow passing `this` before the constructor completes, this requires a two-step pattern:
```csharp
var a = new CompositeScorer([(recency, 1.0)]);
var b = new CompositeScorer([(a, 1.0)]);
// Now try to create a cycle — impossible without mutation
```
Since CompositeScorer is immutable (sealed, readonly fields), **direct self-cycles are impossible in C#**. Cycles can only occur if someone casts or uses reflection. The cycle detection is still valuable for detecting indirect cycles through ScaledScorer wrappers or deep nesting, and for defense-in-depth.

---

## Code Examples

### Constructor Validation Pattern (matches ContextBudget, KindScorer, TagScorer)

```csharp
public CompositeScorer(IReadOnlyList<(IScorer Scorer, double Weight)> entries)
{
    ArgumentNullException.ThrowIfNull(entries);

    if (entries.Count == 0)
        throw new ArgumentException("At least one scorer entry is required.", nameof(entries));

    var scorers = new IScorer[entries.Count];
    var weights = new double[entries.Count];
    var totalWeight = 0.0;

    for (var i = 0; i < entries.Count; i++)
    {
        var (scorer, weight) = entries[i];
        ArgumentNullException.ThrowIfNull(scorer, $"entries[{i}].Scorer");
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight, $"entries[{i}].Weight");
        if (!double.IsFinite(weight))
            throw new ArgumentOutOfRangeException($"entries[{i}].Weight", weight, "Weight must be finite.");

        scorers[i] = scorer;
        weights[i] = weight;
        totalWeight += weight;
    }

    // Normalize weights
    for (var i = 0; i < weights.Length; i++)
        weights[i] /= totalWeight;

    // Cycle detection
    DetectCycles(this, scorers);

    _scorers = scorers;
    _normalizedWeights = weights;
}
```

**Note on zero vs positive weights:** The example above uses `ThrowIfNegativeOrZero` to reject zero weights. This is the recommended approach — a zero-weighted scorer has no effect and is likely a configuration error. If the user wants to "disable" a scorer, they should remove it from the list.

### TUnit Test Pattern (matches existing scorer tests)

```csharp
[Test]
public async Task WeightedAverage_ProducesExpectedOrdinal()
{
    // Arrange: two scorers, one heavily weighted
    var composite = new CompositeScorer([
        (new ReflexiveScorer(), 3.0),
        (new PriorityScorer(), 1.0)
    ]);

    var highRelevance = CreateItem(futureRelevanceHint: 0.9, priority: 1);
    var highPriority = CreateItem(futureRelevanceHint: 0.1, priority: 100);
    var allItems = new List<ContextItem> { highRelevance, highPriority };

    // Act
    var relevanceScore = composite.Score(highRelevance, allItems);
    var priorityScore = composite.Score(highPriority, allItems);

    // Assert: relevance dominates because weight is 3x
    await Assert.That(relevanceScore).IsGreaterThan(priorityScore);
}
```

### Stable Sort Tiebreaking Test

```csharp
[Test]
public async Task IdenticalScores_PreserveInsertionOrder()
{
    // All items get identical composite scores
    var items = Enumerable.Range(0, 5)
        .Select(i => new ContextItem { Content = $"item-{i}", Tokens = 10 })
        .ToList();

    var scorer = new CompositeScorer([(new ReflexiveScorer(), 1.0)]);
    // All FutureRelevanceHint is null → all score 0.0

    var scored = new (double Score, int Index)[items.Count];
    for (var i = 0; i < items.Count; i++)
        scored[i] = (scorer.Score(items[i], items), i);

    Array.Sort(scored, static (a, b) =>
    {
        var cmp = b.Score.CompareTo(a.Score);
        return cmp != 0 ? cmp : a.Index.CompareTo(b.Index);
    });

    // Verify insertion order preserved
    for (var i = 0; i < scored.Length; i++)
        await Assert.That(scored[i].Index).IsEqualTo(i);
}
```

---

## Open Questions (for Planner to Decide)

### Q1: Should zero weights be allowed?
**Recommendation:** No. Reject with `ArgumentOutOfRangeException`. A zero-weighted scorer is dead code. If the user wants optional scorers, they should conditionally include them in the list.

### Q2: ScaledScorer degenerate case return value?
**Recommendation:** Return 0.5 (midpoint). Alternatives: 0.0 (conservative), 1.0 (generous). Midpoint is most neutral and doesn't bias the composite score.

### Q3: Maximum nesting depth?
**Recommendation:** No artificial cap. The cycle detection prevents infinite recursion. Deeply nested composites are unusual but valid. The performance cost is linear in tree depth, which is negligible.

### Q4: Where does stable sort live?
**Recommendation:** The sort itself belongs in the pipeline (Phase 5). Phase 4 provides the scoring. But Phase 4 tests should verify that the index-augmented sort technique works with CompositeScorer output, establishing the pattern for Phase 5 to adopt.

---

*Phase: 04-composite-scoring*
*Research completed: 2026-03-13*
