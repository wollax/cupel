# Phase 3: Individual Scorers - Research

**Researched:** 2026-03-11
**Domain:** Zero-allocation scoring algorithms, rank-based interpolation, TUnit ordinal testing
**Confidence:** HIGH

## Summary

This phase implements six stateless scorers that each implement the existing `IScorer` interface (`double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)`). The core challenge is **not** algorithmic complexity—every scorer is simple arithmetic—but rather achieving **zero heap allocations** in every `Score()` method while keeping the code clear and testable.

The codebase already has established patterns for zero-allocation loops (see `ContextResult.TotalTokens`), `[MemoryDiagnoser]` benchmarks (see `TraceGatingBenchmark`), and TUnit async assertions. This phase extends those patterns to six new classes.

**Primary recommendation:** Implement scorers as `sealed class` types with `for` loops over `IReadOnlyList<T>` (indexed access, no enumerator allocation). Use `StringComparer.OrdinalIgnoreCase` for tag comparison. Test ordinal relationships with TUnit's `IsGreaterThan`/`IsLessThan` assertions. Verify zero allocations with a single `[MemoryDiagnoser]` benchmark class covering all six scorers.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10 BCL | net10.0 | Runtime, `IReadOnlyList<T>`, `IReadOnlyDictionary<TKey,TValue>`, `Math.Clamp` | Already targeted; no external dependencies in core library |
| `StringComparer.OrdinalIgnoreCase` | BCL | Tag comparison in FrequencyScorer | Consistent with `ContextKind.Equals` which already uses `StringComparison.OrdinalIgnoreCase` |

### Testing

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| TUnit | (current) | All scorer unit tests | Already in test project |
| BenchmarkDotNet | (current) | `[MemoryDiagnoser]` zero-allocation verification | Already in benchmark project |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `for` loop over `IReadOnlyList` | `foreach` | `foreach` on `IReadOnlyList<T>` allocates an `IEnumerator<T>` on the heap; `for` with indexer does not |
| `StringComparer.OrdinalIgnoreCase` | `string.Equals(..., OrdinalIgnoreCase)` | Equivalent for pairwise comparison, but `StringComparer` instance enables potential future dictionary key use |
| Manual rank counting | `Array.Sort` + rank lookup | Sort allocates; counting with a `for` loop does not |

**Installation:** No new packages required. All dependencies already present.

## Architecture Patterns

### Recommended Project Structure

```
src/Wollax.Cupel/
├── Scoring/                    # New folder for scorer implementations
│   ├── RecencyScorer.cs
│   ├── PriorityScorer.cs
│   ├── KindScorer.cs
│   ├── TagScorer.cs
│   ├── FrequencyScorer.cs
│   └── ReflexiveScorer.cs
├── IScorer.cs                  # Already exists in root namespace

tests/Wollax.Cupel.Tests/
├── Scoring/                    # New folder for scorer tests
│   ├── RecencyScorerTests.cs
│   ├── PriorityScorerTests.cs
│   ├── KindScorerTests.cs
│   ├── TagScorerTests.cs
│   ├── FrequencyScorerTests.cs
│   └── ReflexiveScorerTests.cs

benchmarks/Wollax.Cupel.Benchmarks/
└── ScorerBenchmark.cs          # Single benchmark class for all scorers
```

**Namespace decision:** Scorers should live in `Wollax.Cupel.Scoring` namespace (subfolder) to keep the root namespace clean, while `IScorer` stays in `Wollax.Cupel` (already shipped in PublicAPI.Unshipped.txt). This follows the existing `Diagnostics` subfolder pattern.

### Pattern 1: Rank-Based Linear Interpolation (RecencyScorer, PriorityScorer)

**What:** For relative ranking within the input set, count how many items have a lesser value, then interpolate.

**When to use:** RecencyScorer (by `Timestamp`) and PriorityScorer (by `Priority`).

**Formula:**
```
rank = count of items where value < thisItem.value  (0-based)
maxRank = countOfItemsWithValues - 1
score = (maxRank == 0) ? 1.0 : rank / (double)maxRank
```

**Example (zero-allocation):**
```csharp
public sealed class RecencyScorer : IScorer
{
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.Timestamp.HasValue)
            return 0.0;

        var thisTimestamp = item.Timestamp.Value;
        var rank = 0;
        var countWithTimestamp = 0;

        for (var i = 0; i < allItems.Count; i++)
        {
            var other = allItems[i];
            if (!other.Timestamp.HasValue)
                continue;

            countWithTimestamp++;
            if (other.Timestamp.Value < thisTimestamp)
                rank++;
        }

        // Only one item with a timestamp → it's the most recent → 1.0
        return countWithTimestamp <= 1 ? 1.0 : rank / (double)(countWithTimestamp - 1);
    }
}
```

**Key properties:**
- Items with null timestamps get 0.0 (missing data rule)
- Single item with timestamp gets 1.0 (it's trivially the "most recent")
- Tied timestamps get the same rank (natural behavior of `<` comparison)
- O(n) per call, which is fine since scorers are called once per item per scoring pass

### Pattern 2: Dictionary Lookup (KindScorer, TagScorer)

**What:** Look up the item's property in a constructor-injected dictionary.

**When to use:** KindScorer (lookup `Kind` in weight map) and TagScorer (sum matched tag weights / total weight sum).

**Example (KindScorer):**
```csharp
public sealed class KindScorer : IScorer
{
    private static readonly IReadOnlyDictionary<ContextKind, double> DefaultWeights =
        new Dictionary<ContextKind, double>
        {
            [ContextKind.SystemPrompt] = 1.0,
            [ContextKind.Memory] = 0.8,
            [ContextKind.ToolOutput] = 0.6,
            [ContextKind.Document] = 0.4,
            [ContextKind.Message] = 0.2,
        };

    private readonly IReadOnlyDictionary<ContextKind, double> _weights;

    public KindScorer() : this(DefaultWeights) { }

    public KindScorer(IReadOnlyDictionary<ContextKind, double> weights)
    {
        _weights = weights;
    }

    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        return _weights.TryGetValue(item.Kind, out var weight) ? weight : 0.0;
    }
}
```

**TagScorer normalization formula:**
```
matchedSum = sum of weights for tags present on the item
totalSum = sum of ALL configured weights
score = totalSum == 0 ? 0.0 : matchedSum / totalSum
```

### Pattern 3: Set Overlap (FrequencyScorer)

**What:** Count how many other items share at least one tag with the current item.

**Example approach (zero-allocation with indexed loops):**
```csharp
public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
{
    if (item.Tags.Count == 0 || allItems.Count <= 1)
        return 0.0;

    var matchingItems = 0;

    for (var i = 0; i < allItems.Count; i++)
    {
        var other = allItems[i];
        if (ReferenceEquals(other, item))
            continue;
        if (other.Tags.Count == 0)
            continue;
        if (SharesAnyTag(item.Tags, other.Tags))
            matchingItems++;
    }

    return matchingItems / (double)(allItems.Count - 1);
}

private static bool SharesAnyTag(IReadOnlyList<string> a, IReadOnlyList<string> b)
{
    for (var i = 0; i < a.Count; i++)
    {
        for (var j = 0; j < b.Count; j++)
        {
            if (string.Equals(a[i], b[j], StringComparison.OrdinalIgnoreCase))
                return true;
        }
    }
    return false;
}
```

**Note on O(n*m) tag comparison:** For typical use (1–5 tags per item), the nested loop is faster than building a `HashSet<string>` (which would allocate). For pathologically large tag sets, a `HashSet` would win, but that's not the expected use case.

### Pattern 4: Passthrough (ReflexiveScorer)

**What:** Return the item's `FutureRelevanceHint` directly, clamped to [0.0, 1.0].

```csharp
public sealed class ReflexiveScorer : IScorer
{
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.FutureRelevanceHint.HasValue)
            return 0.0;

        return Math.Clamp(item.FutureRelevanceHint.Value, 0.0, 1.0);
    }
}
```

### Anti-Patterns to Avoid

- **LINQ in Score():** `allItems.Where(x => ...).Count()` allocates a closure + iterator. Use `for` loops.
- **`foreach` over `IReadOnlyList<T>`:** Allocates `IEnumerator<T>`. Use indexed `for` loop.
- **Boxing nullable value types:** `item.Timestamp?.CompareTo(other.Timestamp)` — the `?.` operator on `Nullable<T>` doesn't box, but be careful with patterns that convert to `object`.
- **`new Dictionary` or `new HashSet` inside Score():** Heap allocation per call. Pre-compute or use nested loops.
- **`string.ToLower()` for case-insensitive comparison:** Allocates a new string. Use `StringComparison.OrdinalIgnoreCase`.
- **`ReferenceEquals` vs index check for FrequencyScorer:** Use `ReferenceEquals(other, item)` to skip self, not index comparison (items could appear at multiple indices if list has duplicates).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Value clamping | Manual `if` chains | `Math.Clamp(value, 0.0, 1.0)` | BCL method, JIT-intrinsic, no allocation |
| Case-insensitive string comparison | `ToLower()`/`ToUpper()` | `string.Equals(a, b, StringComparison.OrdinalIgnoreCase)` | No allocation, culture-invariant |
| Benchmark infrastructure | Custom timing code | `[MemoryDiagnoser]` + `[Benchmark]` | BenchmarkDotNet already in project |
| Public API tracking | Manual tracking | `PublicAPI.Unshipped.txt` entries | PublicApiAnalyzers already enforced |

**Key insight:** Every scorer is simple enough that the temptation is to "just write it." The risk isn't in the algorithm—it's in accidental allocations that violate the zero-GC constraint. The `for`-loop-only discipline is the main guard.

## Common Pitfalls

### Pitfall 1: IEnumerator Allocation from foreach

**What goes wrong:** `foreach (var item in allItems)` on an `IReadOnlyList<ContextItem>` allocates a boxed `IEnumerator<T>` because the interface's `GetEnumerator()` returns `IEnumerator<T>` (interface, not struct).
**Why it happens:** C# pattern-based foreach only avoids allocation when the collection type has a struct-returning `GetEnumerator()` method visible at compile time (e.g., `List<T>.Enumerator`). Through the `IReadOnlyList<T>` interface, this optimization doesn't apply.
**How to avoid:** Always use `for (var i = 0; i < allItems.Count; i++)` with indexer access.
**Warning signs:** Gen0 > 0 in MemoryDiagnoser output.

### Pitfall 2: Floating-Point Division Edge Cases

**What goes wrong:** Division by zero when all items have null values, or when `allItems.Count == 1`.
**Why it happens:** Rank-based scorers divide by `(countWithValues - 1)`. If only one item has a value, denominator is 0.
**How to avoid:** Guard clause: `if (countWithValues <= 1) return 1.0;` — a single valid item is trivially "the best."
**Warning signs:** `NaN` or `Infinity` in scorer output (violates `IScorer` contract).

### Pitfall 3: TUnit Assert.That(constant) Analyzer Error

**What goes wrong:** `await Assert.That(0.0).IsEqualTo(score)` fails to compile because TUnit's analyzer rejects constant expressions in `Assert.That()`.
**Why it happens:** TUnit has a compile-time analyzer that prevents asserting on constants (since they're always the same).
**How to avoid:** Assert on the variable: `await Assert.That(score).IsEqualTo(0.0)`. Put the computed value in `Assert.That()`, the expected value in the assertion method.
**Warning signs:** Build error from TUnit analyzer.

### Pitfall 4: PublicApiAnalyzers Missing Entries

**What goes wrong:** Build fails with RS0016 (symbol not declared in public API).
**Why it happens:** Every new public type and member must be listed in `PublicAPI.Unshipped.txt`.
**How to avoid:** After implementing each scorer, add its full API surface to `PublicAPI.Unshipped.txt`. The format is already established in the existing file. Include: class declaration, constructor(s), and `Score` method.
**Warning signs:** RS0016 build error.

### Pitfall 5: Dictionary Lookup Boxing with ContextKind

**What goes wrong:** `Dictionary<ContextKind, double>.TryGetValue` could theoretically box if the `EqualityComparer<ContextKind>.Default` isn't optimized.
**Why it happens:** `ContextKind` is a `sealed class` (reference type), not a struct. So `TryGetValue` uses reference-based dispatch through `IEquatable<ContextKind>`. **No boxing occurs** because `ContextKind` is already a reference type.
**How to avoid:** Non-issue for this codebase. `ContextKind` is a class, so dictionary lookups are allocation-free.
**Warning signs:** N/A — confirmed safe.

### Pitfall 6: TagScorer Weight Sum Pre-computation

**What goes wrong:** Computing `totalSum` of all weights on every `Score()` call.
**Why it happens:** The weight dictionary is injected via constructor, so the total is constant.
**How to avoid:** Pre-compute `_totalWeight` in the constructor and store it as a `readonly double` field.
**Warning signs:** Unnecessary repeated computation (not allocation, but wasteful).

### Pitfall 7: Ordinal Test Fragility with Exact Floats

**What goes wrong:** Tests assert `score == 0.5` but floating-point arithmetic produces `0.49999999999999994`.
**Why it happens:** Division and multiplication don't produce exact binary representations for all decimals.
**How to avoid:** Use ordinal assertions (`IsGreaterThan`, `IsLessThan`) as the primary test strategy. For exact boundary values (0.0, 1.0), exact equality is safe because these come from early-return guard clauses, not arithmetic.
**Warning signs:** Flaky tests on different platforms/runtimes.

## Code Examples

### TUnit Ordinal Relationship Test Pattern

```csharp
// Source: Context7 TUnit docs + existing project patterns
[Test]
public async Task RecentItem_ScoresHigher_ThanOlderItem()
{
    var scorer = new RecencyScorer();
    var now = DateTimeOffset.UtcNow;

    var recent = new ContextItem { Content = "new", Tokens = 1, Timestamp = now };
    var old = new ContextItem { Content = "old", Tokens = 1, Timestamp = now.AddHours(-2) };
    var allItems = new List<ContextItem> { recent, old };

    var recentScore = scorer.Score(recent, allItems);
    var oldScore = scorer.Score(old, allItems);

    await Assert.That(recentScore).IsGreaterThan(oldScore);
}
```

### TUnit Boundary Value Test Pattern

```csharp
[Test]
public async Task NullTimestamp_Returns_Zero()
{
    var scorer = new RecencyScorer();
    var item = new ContextItem { Content = "no time", Tokens = 1 };
    var allItems = new List<ContextItem> { item };

    var score = scorer.Score(item, allItems);

    await Assert.That(score).IsEqualTo(0.0);
}
```

### TUnit IsBetween for Range Validation

```csharp
[Test]
public async Task Score_IsAlways_InZeroToOneRange()
{
    var scorer = new RecencyScorer();
    var now = DateTimeOffset.UtcNow;
    var items = new List<ContextItem>
    {
        new() { Content = "a", Tokens = 1, Timestamp = now },
        new() { Content = "b", Tokens = 1, Timestamp = now.AddMinutes(-30) },
        new() { Content = "c", Tokens = 1, Timestamp = now.AddHours(-1) },
    };

    for (var i = 0; i < items.Count; i++)
    {
        var score = scorer.Score(items[i], items);
        await Assert.That(score).IsBetween(0.0, 1.0);
    }
}
```

### BenchmarkDotNet Scorer Benchmark Pattern

```csharp
// Source: Existing TraceGatingBenchmark + BenchmarkDotNet Context7 docs
[MemoryDiagnoser]
public class ScorerBenchmark
{
    private ContextItem[] _items = null!;
    private RecencyScorer _recencyScorer = null!;
    // ... other scorers

    [Params(100, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var now = DateTimeOffset.UtcNow;
        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new ContextItem
            {
                Content = $"Item {i}",
                Tokens = 10,
                Timestamp = now.AddMinutes(-i),
                Priority = i,
                Kind = ContextKind.Message,
                Tags = [$"tag-{i % 5}"],
                FutureRelevanceHint = i / (double)ItemCount,
            })
            .ToArray();

        _recencyScorer = new RecencyScorer();
        // ... initialize other scorers
    }

    [Benchmark]
    public double RecencyScorer_AllItems()
    {
        var sum = 0.0;
        for (var i = 0; i < _items.Length; i++)
            sum += _recencyScorer.Score(_items[i], _items);
        return sum;
    }

    // ... one [Benchmark] per scorer
}
```

**Expected benchmark result:** All scorers show `Gen0: -` (dash means zero) and `Allocated: 0 B` (or dash).

### PublicAPI.Unshipped.txt Entry Pattern

```text
Wollax.Cupel.Scoring.RecencyScorer
Wollax.Cupel.Scoring.RecencyScorer.RecencyScorer() -> void
Wollax.Cupel.Scoring.RecencyScorer.Score(Wollax.Cupel.ContextItem! item, System.Collections.Generic.IReadOnlyList<Wollax.Cupel.ContextItem!>! allItems) -> double
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|-------------|-----------------|--------------|--------|
| `Math.Max(0, Math.Min(1, val))` | `Math.Clamp(val, 0.0, 1.0)` | .NET Core 2.0+ | Cleaner, JIT-intrinsic |
| `foreach` + LINQ | `for` with indexer | N/A (perf discipline) | Zero enumerator allocation |
| `HashSet<string>` for tag overlap | Nested `for` loop with `string.Equals` | N/A (small set optimization) | Zero allocation for typical 1-5 tag items |
| `Nullable<T>.GetValueOrDefault()` | Pattern matching `if (!x.HasValue)` | N/A (clarity) | Same codegen, clearer intent |

**Deprecated/outdated:**
- `IComparer`-based sorting for rank computation: allocates array + comparisons. Direct counting is O(n) and allocation-free.

## Open Questions

1. **Tag comparison case sensitivity in FrequencyScorer**
   - What we know: `ContextKind` uses `OrdinalIgnoreCase`. Tags are `IReadOnlyList<string>`.
   - Recommendation: Use `StringComparison.OrdinalIgnoreCase` for consistency with `ContextKind`. This is a Claude's Discretion item; recommend case-insensitive.

2. **Scorer namespace: `Wollax.Cupel.Scoring` vs root**
   - What we know: Existing pattern has `Diagnostics` subfolder/namespace. `IScorer` is in root namespace.
   - Recommendation: `Wollax.Cupel.Scoring` namespace in `Scoring/` subfolder. Implementations are not in the interface's namespace — this is standard (e.g., `System.Collections` vs `System.Collections.Generic`).

3. **Tied values in rank-based scorers**
   - What we know: When multiple items share the same timestamp/priority, `<` comparison gives them the same rank naturally.
   - What's unclear: Should ties produce the same score? Yes — using `<` means tied items get equal rank, producing equal scores. This is correct and consistent with the ordinal testing approach.

4. **FrequencyScorer self-exclusion**
   - What we know: Formula is `matchingItems / (allItems.Count - 1)`. Must exclude self.
   - Recommendation: Use `ReferenceEquals(other, item)` for self-exclusion. This works because `ContextItem` is a reference type (`sealed record` = class). If the same item instance appears multiple times in `allItems`, only exact reference matches are skipped — which is correct behavior (duplicates in the list are separate candidates).

## Sources

### Primary (HIGH confidence)
- Context7 `/dotnet/benchmarkdotnet` — MemoryDiagnoser usage, GlobalSetup pattern, Params attribute
- Context7 `/thomhurst/tunit` — `IsGreaterThan`, `IsLessThan`, `IsBetween`, `Arguments`, `MethodDataSource` assertions
- Context7 `/dotnet/runtime` — `Span<T>`, `ArrayPool`, `stackalloc` patterns (confirmed not needed for this phase — scorer logic is pure arithmetic over existing collections)
- Existing codebase: `ContextResult.TotalTokens` for-loop pattern, `TraceGatingBenchmark` for MemoryDiagnoser usage, `ContextKind` for OrdinalIgnoreCase pattern

### Secondary (MEDIUM confidence)
- Rank-based interpolation formula: standard mathematical approach, verified against existing `IScorer` contract requirements
- `foreach` allocation on interfaces: well-documented .NET runtime behavior (enumerator boxing through interface dispatch)

### Tertiary (LOW confidence)
- None. All findings verified against Context7 or existing codebase.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, all patterns from existing codebase
- Architecture: HIGH — follows established `Diagnostics/` subfolder pattern, all types implement existing `IScorer` interface
- Pitfalls: HIGH — most pitfalls derived from existing project's `STATE.md` decisions and Context7-verified TUnit/BenchmarkDotNet behavior
- Code examples: HIGH — adapted from existing codebase patterns (`ContextResult.TotalTokens`, `TraceGatingBenchmark`, `ContextItemTests`)

**Research date:** 2026-03-11
**Valid until:** 2026-04-11 (stable — no moving dependencies)
