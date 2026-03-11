# Phase 2: Interfaces & Diagnostics Infrastructure - Research

**Researched:** 2026-03-11
**Domain:** C# interface design for scoring/selection pipelines, zero-allocation tracing infrastructure, IAsyncEnumerable patterns, sealed record result types
**Confidence:** HIGH

## Summary

Phase 2 defines the load-bearing API surface (IScorer, ISlicer, IPlacer, IContextSource) and builds the tracing infrastructure (ITraceCollector, NullTraceCollector, DiagnosticTraceCollector) plus the pipeline return type (ContextResult). This is pure internal .NET work with no external dependencies — all patterns come from BCL conventions.

The key technical challenge is the zero-allocation tracing requirement: trace event construction must be gated behind an `IsEnabled` check so that running with `NullTraceCollector` produces zero allocations on trace code paths. The BCL's `DiagnosticSource.IsEnabled()` pattern is the direct inspiration. The interfaces themselves are straightforward — the decisions center on sync vs async boundaries and CancellationToken placement.

**Primary recommendation:** Define IScorer as synchronous (scores are CPU-bound computations over in-memory data). Define ISlicer and IPlacer as synchronous for the same reason. Define IContextSource with both batch and streaming methods on a single interface. Build ITraceCollector with an `IsEnabled` property gate and use `long` timestamp pairs from `Stopwatch.GetTimestamp()` for zero-allocation timing. Make ContextResult a sealed record with a nullable `SelectionReport?` trace attachment.

## Standard Stack

### Core (Phase 2 scope)

No new packages. Everything is BCL-only.

| Component | Source | Purpose | Why Standard |
| --- | --- | --- | --- |
| `System.Diagnostics.Stopwatch` | BCL | High-resolution timing | `GetTimestamp()` + `GetElapsedTime()` — zero allocation, no Stopwatch instance needed |
| `System.Runtime.CompilerServices` | BCL | `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for IsEnabled check | Ensures the gating check is inlined at call sites |
| `IAsyncEnumerable<T>` | BCL (System.Collections.Generic) | Streaming context source | Built into C# since 8.0, no external package |
| `System.Threading.CancellationToken` | BCL | Cooperative cancellation | Standard .NET cancellation pattern |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
| --- | --- | --- |
| `Stopwatch.GetTimestamp()` static | `TimeProvider.GetTimestamp()` | TimeProvider adds testability for time-dependent code, but stage timing in tracing doesn't need fake time — it measures real wall-clock elapsed time. Stopwatch is simpler and avoids an injected dependency on every trace call. TimeProvider is appropriate for time-dependent *business logic* (RecencyScorer in Phase 3), not for diagnostic timing. |
| Custom ITraceCollector | `System.Diagnostics.DiagnosticSource` | DiagnosticSource is designed for cross-library instrumentation with subscriber discovery. Cupel's tracing is internal to the pipeline — a simpler custom interface avoids the `object` payload boxing and anonymous type allocation that DiagnosticSource requires. |
| Custom ITraceCollector | `System.Diagnostics.ActivitySource` | ActivitySource is for distributed tracing with OpenTelemetry. Cupel's tracing is local pipeline diagnostics — wrong abstraction level. |

## Architecture Patterns

### Pattern 1: Synchronous Scorer Interface

**What:** IScorer as a synchronous interface returning `double` per item.
**When to use:** When the operation is CPU-bound over in-memory data with no I/O.
**Why sync:** Scorers operate on `ContextItem` properties (timestamps, tags, priority values). There is no I/O, no network, no disk access. Making IScorer async would force `Task<double>` allocations on every score call — thousands per pipeline execution. The zero-allocation hot path requirement from TRACE-03 makes this a non-starter.

```csharp
/// <summary>
/// Assigns a relevance score to a context item.
/// Output is conventionally 0.0–1.0 (documented, not enforced by type).
/// </summary>
public interface IScorer
{
    /// <summary>
    /// Scores a single item in the context of the full candidate set.
    /// </summary>
    /// <param name="item">The item to score.</param>
    /// <param name="allItems">The complete candidate set for relative scoring.</param>
    /// <returns>A relevance score, conventionally 0.0–1.0.</returns>
    double Score(ContextItem item, IReadOnlyList<ContextItem> allItems);
}
```

**Key design decisions:**
- `allItems` parameter enables relative scoring (RecencyScorer needs min/max timestamps from the set).
- Returns `double`, not `float` — `double` is the natural .NET numeric type, avoids narrowing conversions.
- No `CancellationToken` — synchronous CPU-bound work completes in microseconds per item. Cancellation overhead exceeds useful cancellation window.
- No `ContextBudget` parameter — scorers rank, they don't need to know the budget. Budget awareness is the slicer's job (ordinal-only invariant).

### Pattern 2: Synchronous Slicer Interface

**What:** ISlicer as a synchronous interface that selects items within a budget.
**When to use:** When the selection algorithm operates on in-memory scored items.

```csharp
/// <summary>
/// Selects items from scored candidates within a token budget.
/// </summary>
public interface ISlicer
{
    /// <summary>
    /// Selects items that fit within the given budget.
    /// </summary>
    /// <param name="scoredItems">Items with their scores, sorted by score descending.</param>
    /// <param name="budget">The token budget constraint.</param>
    /// <param name="traceCollector">Trace collector for diagnostic events.</param>
    /// <returns>The selected items.</returns>
    IReadOnlyList<ContextItem> Slice(
        IReadOnlyList<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector);
}
```

**Key design decisions:**
- Synchronous for the same CPU-bound reasoning as IScorer.
- `ScoredItem` is a lightweight wrapper pairing `ContextItem` with its `double Score` — needed because the slicer must see both the item and its score.
- `ITraceCollector` passed explicitly (TRACE-04: no AsyncLocal). Every pipeline stage receives the collector as a parameter.
- Budget is a parameter, not injected — slicers are stateless.

### Pattern 3: Synchronous Placer Interface

**What:** IPlacer as a synchronous interface that orders selected items.
**When to use:** Placement is pure reordering of in-memory items.

```csharp
/// <summary>
/// Determines the final ordering of selected context items.
/// </summary>
public interface IPlacer
{
    /// <summary>
    /// Orders the selected items for optimal placement in the context window.
    /// </summary>
    /// <param name="items">The items selected by the slicer.</param>
    /// <param name="traceCollector">Trace collector for diagnostic events.</param>
    /// <returns>The items in their final order.</returns>
    IReadOnlyList<ContextItem> Place(
        IReadOnlyList<ScoredItem> items,
        ITraceCollector traceCollector);
}
```

**Key design decisions:**
- Receives `ScoredItem` not plain `ContextItem` — the placer may use scores for placement decisions (U-shaped: highest scores at edges).
- Returns `IReadOnlyList<ContextItem>` (strips scores) — consumers see items, not scores. Scores belong in the trace/report.

### Pattern 4: IContextSource with Batch + Streaming

**What:** A single interface with two methods for batch and streaming context retrieval.
**When to use:** Context sources may be pre-materialized (batch) or lazily produced (streaming from a database cursor, API pagination).

```csharp
/// <summary>
/// Provides context items to the pipeline.
/// Implementations supply either batch or streaming access (or both).
/// </summary>
public interface IContextSource
{
    /// <summary>
    /// Returns all context items as a batch.
    /// </summary>
    Task<IReadOnlyList<ContextItem>> GetItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams context items for lazy/incremental consumption.
    /// </summary>
    IAsyncEnumerable<ContextItem> GetItemsStreamAsync(CancellationToken cancellationToken = default);
}
```

**Key design decisions:**
- Both methods on one interface — Phase 6 (StreamSlice) needs the streaming path; the default pipeline uses batch. Having two separate interfaces (IBatchContextSource, IStreamingContextSource) is over-engineering for no benefit.
- `Task<IReadOnlyList<ContextItem>>` for batch — async because sources may involve I/O (loading from a file, API call).
- `IAsyncEnumerable<ContextItem>` for streaming — the natural .NET pattern for async sequences.
- `CancellationToken` on both — I/O operations must be cancellable.
- Default implementations: the batch method could have a default that materializes the stream, or vice versa. This is a Phase 2 interface decision — default interface methods (DIMs) can provide the bridge.

**IAsyncEnumerable CancellationToken pattern:**
The `[EnumeratorCancellation]` attribute is required when the CancellationToken should be forwarded to the async enumerator. For `IContextSource`, the token comes from the method parameter:

```csharp
// Implementation pattern for streaming sources
public async IAsyncEnumerable<ContextItem> GetItemsStreamAsync(
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    // The cancellationToken is automatically combined with any token
    // passed via WithCancellation() by the consumer
    await foreach (var item in SomeAsyncSource(cancellationToken))
    {
        yield return item;
    }
}
```

### Pattern 5: Gated Trace Collector (IsEnabled Pattern)

**What:** `ITraceCollector` with an `IsEnabled` bool property that gates all event construction. Modeled after `DiagnosticSource.IsEnabled()` and `ILogger.IsEnabled()`.
**When to use:** Any diagnostic infrastructure where the disabled path must be zero-cost.

```csharp
/// <summary>
/// Collects trace events during pipeline execution.
/// Implementations must ensure IsEnabled is cheap (field read, no computation).
/// </summary>
public interface ITraceCollector
{
    /// <summary>
    /// Whether trace collection is active. Callers MUST check this
    /// before constructing trace event payloads.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Records a stage-level trace event (always captured when enabled).
    /// </summary>
    void RecordStageEvent(TraceEvent traceEvent);

    /// <summary>
    /// Records an item-level trace event (opt-in detail level).
    /// </summary>
    void RecordItemEvent(TraceEvent traceEvent);
}
```

**The gating pattern at call sites:**

```csharp
// CORRECT: gated construction — zero allocation when disabled
if (traceCollector.IsEnabled)
{
    var elapsed = Stopwatch.GetElapsedTime(startTimestamp);
    traceCollector.RecordStageEvent(new TraceEvent
    {
        Stage = "Score",
        Duration = elapsed,
        ItemCount = items.Count
    });
}

// WRONG: constructs the event object even when tracing is disabled
traceCollector.RecordStageEvent(new TraceEvent
{
    Stage = "Score",
    Duration = Stopwatch.GetElapsedTime(startTimestamp),
    ItemCount = items.Count
});
```

**Why this works for zero allocation:**
- `NullTraceCollector.IsEnabled` returns `false` — the branch is never taken.
- The JIT may eliminate the dead branch entirely when inlined.
- No `TraceEvent` struct/class is allocated, no string interpolation runs, no `Stopwatch.GetElapsedTime()` is called.
- The `IsEnabled` check is a single field read — sub-nanosecond.

### Pattern 6: NullObject Pattern for NullTraceCollector

**What:** A singleton no-op implementation of `ITraceCollector` that serves as the default.
**When to use:** Default parameter value when the caller doesn't need tracing.

```csharp
/// <summary>
/// No-op trace collector. All operations are zero-cost.
/// </summary>
public sealed class NullTraceCollector : ITraceCollector
{
    /// <summary>Singleton instance.</summary>
    public static readonly NullTraceCollector Instance = new();

    private NullTraceCollector() { }

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public void RecordStageEvent(TraceEvent traceEvent) { }

    /// <inheritdoc />
    public void RecordItemEvent(TraceEvent traceEvent) { }
}
```

**Key design decisions:**
- `static readonly` singleton, not `static` property — avoids a getter call. Direct field access.
- Private constructor — only one instance ever exists.
- `IsEnabled` returns `false` as a constant — the JIT can inline this and eliminate dead code after the check.
- Empty method bodies — even if callers forget the `IsEnabled` check, the cost is just the method call + argument passing (no allocation from the collector itself, though the caller may have allocated the event object unnecessarily).

### Pattern 7: DiagnosticTraceCollector with Buffered + Callback

**What:** The real trace collector that captures events and optionally streams them via callback.
**When to use:** When the caller wants diagnostic output (debugging, logging, testing).

```csharp
/// <summary>
/// Trace collector that buffers events and optionally invokes a callback for real-time consumption.
/// </summary>
public sealed class DiagnosticTraceCollector : ITraceCollector
{
    private readonly List<TraceEvent> _events = [];
    private readonly Action<TraceEvent>? _callback;
    private readonly TraceDetailLevel _detailLevel;

    public DiagnosticTraceCollector(
        TraceDetailLevel detailLevel = TraceDetailLevel.Stage,
        Action<TraceEvent>? callback = null)
    {
        _detailLevel = detailLevel;
        _callback = callback;
    }

    public bool IsEnabled => true;

    public IReadOnlyList<TraceEvent> Events => _events;

    public void RecordStageEvent(TraceEvent traceEvent)
    {
        _events.Add(traceEvent);
        _callback?.Invoke(traceEvent);
    }

    public void RecordItemEvent(TraceEvent traceEvent)
    {
        if (_detailLevel >= TraceDetailLevel.Item)
        {
            _events.Add(traceEvent);
            _callback?.Invoke(traceEvent);
        }
    }
}
```

**Key design decisions:**
- `TraceDetailLevel` enum controls two-tier verbosity (Stage vs Item). Stage-level events are always captured. Item-level events are opt-in.
- `Action<TraceEvent>?` callback enables real-time consumption (logging, streaming to UI) without requiring the caller to poll the buffer.
- `List<TraceEvent>` buffer — not `ConcurrentBag` because the pipeline is single-threaded within a single execution. Thread safety is not needed.
- Not disposable — no resources to release. The collector is scoped to a single pipeline execution.

### Pattern 8: ScoredItem Wrapper

**What:** A lightweight readonly struct pairing a `ContextItem` with its score.
**When to use:** Passing scored items between pipeline stages (scorer -> slicer -> placer).

```csharp
/// <summary>
/// Pairs a context item with its computed relevance score.
/// </summary>
/// <param name="Item">The context item.</param>
/// <param name="Score">The computed relevance score, conventionally 0.0–1.0.</param>
public readonly record struct ScoredItem(ContextItem Item, double Score);
```

**Key design decisions:**
- `readonly record struct` — value type, no heap allocation, value equality by default.
- Positional record struct — two fields, no JSON serialization needed (internal pipeline type).
- `readonly` prevents defensive copies when accessed through `in` parameters or `readonly` fields.

### Pattern 9: ContextResult as Sealed Record

**What:** The pipeline return type containing selected items and optional trace data.

```csharp
/// <summary>
/// The result of a context selection pipeline execution.
/// </summary>
public sealed record ContextResult
{
    /// <summary>The selected context items in their final placement order.</summary>
    public required IReadOnlyList<ContextItem> Items { get; init; }

    /// <summary>Total token count of selected items.</summary>
    public int TotalTokens => Items.Sum(i => i.Tokens);

    /// <summary>
    /// Selection report with inclusion/exclusion details.
    /// Null when tracing is disabled.
    /// </summary>
    public SelectionReport? Report { get; init; }
}
```

**Key design decisions:**
- `sealed record` — consistent with `ContextItem` pattern. Value equality, `with` expressions, immutable.
- `TotalTokens` as computed property — no stale data, always derived from `Items`.
- `Report` is nullable (`SelectionReport?`) — null when tracing is disabled. This is cleaner than an always-present empty report because:
  - It signals intent: "tracing was not requested" vs "tracing was requested but nothing happened."
  - No empty object allocation on the non-tracing path.
  - Consumers can check `result.Report is not null` to know if trace data is available.
- `required` on `Items` — must be provided at construction.

### Pattern 10: TraceEvent and Supporting Types

**What:** The trace event model and supporting enums.

```csharp
/// <summary>Verbosity level for trace collection.</summary>
public enum TraceDetailLevel
{
    /// <summary>Stage-level events only (Score, Slice, Place durations).</summary>
    Stage = 0,

    /// <summary>Stage-level plus per-item events (individual scores, exclusion reasons).</summary>
    Item = 1
}

/// <summary>Reason an item was excluded from the final selection.</summary>
public enum ExclusionReason
{
    /// <summary>Item scored below the selection threshold.</summary>
    LowScore,

    /// <summary>Item did not fit within the token budget.</summary>
    BudgetExceeded,

    /// <summary>Item was removed during deduplication.</summary>
    Duplicate,

    /// <summary>Item was excluded by a quota constraint.</summary>
    QuotaExceeded
}
```

**Key design decisions:**
- `ExclusionReason` is an enum, not enum + context data. Keeping it simple for Phase 2 — Phase 7 (TRACE-05) adds `SelectionReport` with richer detail. The enum is extensible (new values can be added without breaking binary compatibility).
- `TraceDetailLevel` uses integer ordering so `>=` comparison works for level checks.

### Pattern 11: SelectionReport Placeholder

**What:** The trace attachment type that will be populated by the pipeline.

```csharp
/// <summary>
/// Detailed report of the selection process.
/// Populated when a DiagnosticTraceCollector is used.
/// </summary>
public sealed record SelectionReport
{
    /// <summary>Trace events captured during pipeline execution.</summary>
    public required IReadOnlyList<TraceEvent> Events { get; init; }
}
```

**Key design decisions:**
- Minimal in Phase 2 — just wraps the event list. Phase 7 (TRACE-05) will add `IncludedItems`, `ExcludedItems`, `DryRun()` etc.
- Sealed record for consistency.
- `required` on `Events` — always has trace data when the report exists.

### Pattern 12: Zero-Allocation Timing with Stopwatch.GetTimestamp()

**What:** Using static `Stopwatch` methods for timing without allocating a `Stopwatch` instance.

```csharp
// Start timing (zero allocation — returns a long)
long startTimestamp = Stopwatch.GetTimestamp();

// ... do work ...

// Get elapsed time (zero allocation — static method on two longs)
TimeSpan elapsed = Stopwatch.GetElapsedTime(startTimestamp);
```

**Why not `new Stopwatch()`:**
- `new Stopwatch()` allocates a class instance on the heap.
- `Stopwatch.GetTimestamp()` is a static method returning a `long`. Zero allocation.
- `Stopwatch.GetElapsedTime(long)` was added in .NET 7. Computes elapsed from a single timestamp (uses current time as end). Zero allocation.
- This is the recommended pattern in .NET 7+ for high-performance timing.

**Why not `TimeProvider`:**
- `TimeProvider.System.GetTimestamp()` does the same thing but requires an injected `TimeProvider` instance.
- For diagnostic timing (measuring real elapsed wall-clock time), there's no reason to abstract the clock. You always want real time.
- `TimeProvider` is appropriate for business logic that depends on "what time is it now" (e.g., RecencyScorer comparing timestamps). Diagnostic timing is fundamentally different — it measures real elapsed time, and faking it in tests has no value.

### Anti-Patterns to Avoid

- **AsyncLocal for trace propagation:** The CONTEXT.md explicitly forbids this. Pass `ITraceCollector` as a parameter to every pipeline stage. AsyncLocal creates invisible coupling and makes the trace lifecycle non-obvious.
- **Async IScorer:** Returning `Task<double>` allocates a Task per score call. With 500 items and 6 scorers, that's 3,000 Task allocations per pipeline run. Scorers are CPU-bound — async adds allocation overhead for no benefit.
- **Event construction outside IsEnabled check:** The entire point of gated tracing is that the disabled path does zero work. Every `new TraceEvent(...)` must be inside an `if (traceCollector.IsEnabled)` block.
- **Making TraceEvent a class:** If TraceEvent is a reference type, every trace event allocates on the heap. Use a `readonly record struct` for small fixed-size events to keep them on the stack (when captured in the `IsEnabled` block and passed directly to `RecordStageEvent`). Note: `DiagnosticTraceCollector` stores them in a `List<TraceEvent>` which boxes structs — but this only happens on the enabled path, which is expected to allocate.
- **Interface default methods (DIMs) for IContextSource bridge:** While C# supports default interface methods, using them for the batch-from-stream or stream-from-batch bridge adds complexity. Better to have abstract base classes or require implementors to provide both. Alternatively, provide static helper methods or extension methods for the conversion.
- **ILogger in core:** The core package has zero dependencies. `Microsoft.Extensions.Logging.Abstractions` is an external package. ILogger bridge belongs in the DI companion package (PKG-02), not in core.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
| --- | --- | --- | --- |
| High-resolution timing | Custom timing infrastructure | `Stopwatch.GetTimestamp()` + `Stopwatch.GetElapsedTime()` | Zero allocation, nanosecond precision, built into BCL since .NET 7 |
| Async enumerable cancellation | Manual cancellation token threading | `[EnumeratorCancellation]` attribute | Compiler handles token combination with `WithCancellation()` automatically |
| Null object for tracing | Null checks scattered through pipeline | `NullTraceCollector.Instance` singleton | Single implementation, no null checks needed, consistent interface |
| Value-type score wrapper | Tuple or manual struct | `readonly record struct ScoredItem` | Gets value equality, deconstruction, ToString for free |

## Common Pitfalls

### Pitfall 1: TraceEvent Allocation on Disabled Path
**What goes wrong:** Trace events are constructed and passed to `RecordStageEvent` even when tracing is disabled, causing heap allocations on every pipeline run.
**Why it happens:** It's natural to write `traceCollector.RecordStageEvent(new TraceEvent(...))` without the `IsEnabled` guard — the method call "looks" no-op since `NullTraceCollector` ignores it.
**How to avoid:** Every trace event construction MUST be inside `if (traceCollector.IsEnabled) { ... }`. The benchmark in Success Criteria #4 verifies this by checking for zero Gen0 allocations with `NullTraceCollector`.
**Warning signs:** `[MemoryDiagnoser]` benchmark shows non-zero `Allocated` bytes when running with `NullTraceCollector`.

### Pitfall 2: LINQ in TotalTokens Computed Property
**What goes wrong:** `Items.Sum(i => i.Tokens)` allocates a delegate and enumerator on every access to `TotalTokens`.
**Why it happens:** LINQ's `Sum()` with a lambda creates a closure delegate allocation. `IReadOnlyList<T>` iteration via LINQ uses `IEnumerable<T>.GetEnumerator()` which boxes for structs.
**How to avoid:** Use a manual loop in the computed property, or cache the value at construction time. Since `ContextResult` is immutable (sealed record with `init` properties), the value can be computed once.
**Warning signs:** Multiple accesses to `TotalTokens` show up in allocation profiling.
**Recommendation:** Compute once and store:

```csharp
public sealed record ContextResult
{
    private readonly int _totalTokens;

    public required IReadOnlyList<ContextItem> Items
    {
        get;
        init
        {
            field = value;
            _totalTokens = ComputeTotal(value);
        }
    }

    public int TotalTokens => _totalTokens;

    private static int ComputeTotal(IReadOnlyList<ContextItem> items)
    {
        var total = 0;
        for (var i = 0; i < items.Count; i++)
            total += items[i].Tokens;
        return total;
    }
}
```

Alternatively, use the simpler LINQ approach and document that `TotalTokens` is O(n) per access. Given that `ContextResult` is typically accessed once for the total, LINQ is acceptable if the property is documented as computed. The benchmark will catch if this matters.

### Pitfall 3: IAsyncEnumerable Without EnumeratorCancellation
**What goes wrong:** `CancellationToken` passed to `GetItemsStreamAsync()` is not forwarded to the async enumerator when consumers use `WithCancellation()`.
**Why it happens:** Without `[EnumeratorCancellation]`, the compiler-generated state machine ignores the token from `WithCancellation()`.
**How to avoid:** Always apply `[EnumeratorCancellation]` to the `CancellationToken` parameter on `IAsyncEnumerable`-returning methods. This is a documentation/guidance concern for implementors.
**Warning signs:** Cancellation doesn't work when consuming `IContextSource.GetItemsStreamAsync()` with `await foreach (var item in source.GetItemsStreamAsync().WithCancellation(token))`.

### Pitfall 4: Record Value Equality with IReadOnlyList Properties
**What goes wrong:** Two `ContextResult` instances with identical items are not considered equal because `IReadOnlyList<T>` uses reference equality.
**Why it happens:** Same issue documented in Phase 1 research for `ContextItem`. Record-generated `Equals` uses `EqualityComparer<T>.Default` which calls `object.Equals` for interface types — reference equality.
**How to avoid:** Either override `Equals`/`GetHashCode` on `ContextResult` (like `ContextBudget` does) or accept reference equality and document it. Since `ContextResult` is a pipeline output that's typically consumed once, structural equality is less important than for `ContextItem`.
**Warning signs:** Tests comparing two `ContextResult` instances fail unexpectedly.

### Pitfall 5: PublicApiAnalyzers and Interface Members
**What goes wrong:** Adding interface members to `PublicAPI.Unshipped.txt` requires specific syntax that differs from class members.
**Why it happens:** Interface members in PublicApiAnalyzers use the declaring type prefix: `Wollax.Cupel.IScorer.Score(...)`.
**How to avoid:** Run the RS0016 code fix after adding each interface to auto-generate the correct entries. Don't hand-write PublicAPI.txt entries for interfaces.
**Warning signs:** RS0016 warnings that won't go away despite adding entries manually.

### Pitfall 6: Struct Boxing in List<TraceEvent>
**What goes wrong:** If `TraceEvent` is a `readonly record struct`, storing it in `List<TraceEvent>` does NOT box (generics avoid boxing). However, casting to `IReadOnlyList<TraceEvent>` from `List<TraceEvent>` is fine — `List<T>` implements `IReadOnlyList<T>` directly.
**What actually goes wrong:** If `TraceEvent` implements an interface and you cast a `TraceEvent` to that interface, THAT boxes. Keep `TraceEvent` as a plain struct — no interface implementation.
**How to avoid:** `TraceEvent` should be a `readonly record struct` with no interface implementations. Access it only through generic collections and direct typed references.
**Warning signs:** Unexpected Gen0 allocations in the tracing-enabled benchmark path.

## Code Examples

### Complete ITraceCollector Usage in a Pipeline Stage

```csharp
public IReadOnlyList<ScoredItem> ScoreAll(
    IReadOnlyList<ContextItem> items,
    IScorer scorer,
    ITraceCollector traceCollector)
{
    // Zero-allocation timestamp capture (just a long)
    long startTimestamp = traceCollector.IsEnabled
        ? Stopwatch.GetTimestamp()
        : 0;

    var scored = new ScoredItem[items.Count];
    for (var i = 0; i < items.Count; i++)
    {
        var score = scorer.Score(items[i], items);
        scored[i] = new ScoredItem(items[i], score);
    }

    // Gated trace event — nothing allocates when disabled
    if (traceCollector.IsEnabled)
    {
        traceCollector.RecordStageEvent(new TraceEvent
        {
            Stage = PipelineStage.Score,
            Duration = Stopwatch.GetElapsedTime(startTimestamp),
            ItemCount = items.Count
        });
    }

    return scored;
}
```

### IContextSource Default Implementation Bridge

```csharp
// Extension methods to bridge batch <-> streaming (not on the interface)
public static class ContextSourceExtensions
{
    /// <summary>
    /// Materializes a streaming source into a batch.
    /// </summary>
    public static async Task<IReadOnlyList<ContextItem>> MaterializeAsync(
        this IContextSource source,
        CancellationToken cancellationToken = default)
    {
        var items = new List<ContextItem>();
        await foreach (var item in source.GetItemsStreamAsync(cancellationToken))
        {
            items.Add(item);
        }
        return items;
    }
}
```

### Benchmark for Zero-Allocation Verification

```csharp
[MemoryDiagnoser]
public class TraceGatingBenchmark
{
    private ContextItem[] _items = null!;

    [Params(100, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new ContextItem { Content = $"Item {i}", Tokens = 10 })
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public int BaselineNoTracing()
    {
        var collector = NullTraceCollector.Instance;
        return SimulatePipeline(_items, collector);
    }

    [Benchmark]
    public int WithDiagnosticTracing()
    {
        var collector = new DiagnosticTraceCollector(TraceDetailLevel.Stage);
        return SimulatePipeline(_items, collector);
    }

    private static int SimulatePipeline(
        ContextItem[] items, ITraceCollector traceCollector)
    {
        long startTimestamp = traceCollector.IsEnabled
            ? Stopwatch.GetTimestamp()
            : 0;

        var sum = 0;
        for (var i = 0; i < items.Length; i++)
            sum += items[i].Tokens;

        if (traceCollector.IsEnabled)
        {
            traceCollector.RecordStageEvent(new TraceEvent
            {
                Stage = PipelineStage.Score,
                Duration = Stopwatch.GetElapsedTime(startTimestamp),
                ItemCount = items.Length
            });
        }

        return sum;
    }
}
```

**Expected benchmark results:**
- `BaselineNoTracing`: 0 bytes allocated (Gen0 = 0)
- `WithDiagnosticTracing`: Non-zero allocated (TraceEvent storage, List growth) — this is acceptable

### TUnit Test Patterns for Interfaces

```csharp
// Testing that NullTraceCollector is a true no-op
[Test]
public async Task NullTraceCollector_IsEnabled_ReturnsFalse()
{
    await Assert.That(NullTraceCollector.Instance.IsEnabled).IsFalse();
}

[Test]
public async Task NullTraceCollector_IsSingleton()
{
    var a = NullTraceCollector.Instance;
    var b = NullTraceCollector.Instance;
    await Assert.That(ReferenceEquals(a, b)).IsTrue();
}

[Test]
public async Task DiagnosticTraceCollector_IsEnabled_ReturnsTrue()
{
    var collector = new DiagnosticTraceCollector();
    await Assert.That(collector.IsEnabled).IsTrue();
}

[Test]
public async Task DiagnosticTraceCollector_Callback_InvokedOnRecord()
{
    var received = new List<TraceEvent>();
    var collector = new DiagnosticTraceCollector(
        TraceDetailLevel.Stage,
        callback: e => received.Add(e));

    collector.RecordStageEvent(new TraceEvent
    {
        Stage = PipelineStage.Score,
        Duration = TimeSpan.FromMilliseconds(1),
        ItemCount = 10
    });

    await Assert.That(received).HasCount().EqualTo(1);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
| --- | --- | --- | --- |
| `new Stopwatch()` instance | `Stopwatch.GetTimestamp()` static | .NET 7 (2022) | Zero allocation timing |
| `DiagnosticSource` for internal tracing | Custom `ITraceCollector` with `IsEnabled` gate | N/A (design choice) | Avoids `object` payload boxing from DiagnosticSource |
| `AsyncLocal<T>` for context propagation | Explicit parameter passing | N/A (design choice) | Visible dependency, testable, no hidden state |
| Separate batch/stream interfaces | Single `IContextSource` with both methods | N/A (design choice) | Simpler API surface, implementors choose which to optimize |

## Open Questions

### 1. TraceEvent: struct vs class?
**What we know:** `readonly record struct` avoids heap allocation at construction. `List<TraceEvent>` storage doesn't box generic structs. Stage-level events are few (5-6 per pipeline run). Item-level events could be many (500+ items).
**What's unclear:** Whether the struct size matters. A TraceEvent with `PipelineStage` (enum), `TimeSpan` (8 bytes), `int` (4 bytes), and possibly a string message could be 30-40 bytes. Structs over ~40 bytes lose their copy advantage.
**Recommendation:** Use `readonly record struct`. Stage events are few. Item events are opt-in and only captured when tracing is explicitly enabled — allocation is expected on that path. Keep TraceEvent small: stage enum, duration, item count. No string message in the struct — use the stage enum for identification.

### 2. ScoredItem visibility
**What we know:** `ScoredItem` is used between pipeline stages (scorer output -> slicer input -> placer input). It needs to be public because ISlicer and IPlacer are public interfaces with `ScoredItem` in their signatures.
**What's unclear:** Whether consumers will create `ScoredItem` instances directly (test doubles, custom pipeline stages).
**Recommendation:** Make `ScoredItem` public. It's in the interface signatures — it must be public. Consumers implementing custom scorers/slicers/placers will need it.

### 3. Should IContextSource have default interface method implementations?
**What we know:** Most sources will implement only batch or only streaming. Having both methods with no default requires implementors to provide both.
**What's unclear:** Whether DIMs are the right approach or whether abstract base classes are better.
**Recommendation:** Provide a default `GetItemsStreamAsync` that wraps batch, and a default `GetItemsAsync` that materializes the stream. This way, implementors only need to override the one that's natural for their source. DIMs work well here since there's no state involved — pure delegation.

## Sources

### Primary (HIGH confidence)
- .NET Runtime source (Context7 `/dotnet/runtime`) — DiagnosticSource IsEnabled pattern, Stopwatch.GetTimestamp() usage, IAsyncEnumerable patterns
- Existing Cupel codebase (Phase 1) — ContextItem sealed record pattern, ContextBudget validation pattern, smart enum pattern, TUnit test conventions, BenchmarkDotNet setup
- Phase 2 CONTEXT.md — locked decisions on trace granularity, interface patterns, ContextResult structure, diagnostic consumption model

### Secondary (MEDIUM confidence)
- .NET runtime design guidelines — coding conventions for interface design, NullObject pattern
- DiagnosticSource Users Guide — IsEnabled gating pattern for zero-allocation tracing

### Tertiary (LOW confidence)
- None — all findings are based on BCL patterns and existing codebase conventions.

## Metadata

**Confidence breakdown:**
- Interface design (IScorer, ISlicer, IPlacer): HIGH — standard C# interface patterns, decisions constrained by CONTEXT.md and zero-allocation requirement
- IContextSource async patterns: HIGH — standard IAsyncEnumerable with well-documented CancellationToken handling
- ITraceCollector gating pattern: HIGH — directly modeled on DiagnosticSource.IsEnabled(), a proven BCL pattern
- NullTraceCollector: HIGH — textbook NullObject pattern
- DiagnosticTraceCollector: HIGH — straightforward list buffer + optional callback, constrained by CONTEXT.md
- ContextResult sealed record: HIGH — follows established ContextItem pattern from Phase 1
- ScoredItem readonly record struct: HIGH — standard value type wrapper
- TraceEvent struct design: MEDIUM — size vs allocation tradeoff needs validation via benchmark
- Zero-allocation timing: HIGH — Stopwatch.GetTimestamp() is documented BCL API since .NET 7

**Research date:** 2026-03-11
**Valid until:** 2026-04-11 (stable BCL patterns, 30-day validity)
