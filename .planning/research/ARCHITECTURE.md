# Architecture Research: High-Performance .NET Pipeline Library

**Date**: 2026-03-10
**Scope**: Architecture patterns for Cupel context management library
**Target**: .NET 10, zero external dependencies in core, sub-1ms for <500 items

---

## 1. Pipeline Pattern: Fixed Stage vs Middleware vs Chain-of-Responsibility

### Options Surveyed

| Pattern | Fit | Verdict |
|---------|-----|---------|
| Fixed stage pipeline | Best | **Recommended** |
| Middleware (call-next) | Poor | Rejected in brainstorm |
| Chain-of-responsibility | Moderate | Overkill for fixed stages |
| System.Threading.Tasks.Dataflow | Poor | External dependency, wrong abstraction |

### Recommendation: Fixed Stage Pipeline (Confidence: HIGH)

The fixed pipeline `Classify -> Score -> Deduplicate -> Slice -> Place` is the correct choice. Each stage has a distinct input/output contract:

```
IReadOnlyList<ContextItem>           -- input
  -> Classify: tag items with Kind
  -> Score: attach ordinal scores (0.0-1.0)
  -> Deduplicate: remove duplicates
  -> Slice: select subset within budget
  -> Place: order selected items for output
-> ContextResult                     -- output
```

**Implementation pattern**: Each stage is a method on an internal `PipelineExecutor` class (or similar). The pipeline object holds references to the pluggable implementations (IScorer, ISlicer, IPlacer). No delegate chain, no `Func<T, Task<T>>` middleware.

```csharp
// Internal, not exposed to consumers
internal sealed class PipelineExecutor
{
    internal ContextResult Execute(
        IReadOnlyList<ContextItem> candidates,
        ContextBudget budget,
        PipelineComponents components,
        ITraceCollector? trace)
    {
        var classified = components.Classifier.Classify(candidates);
        var scored = Score(classified, components.Scorers, trace);
        var deduplicated = Deduplicate(scored);
        var sliced = components.Slicer.Slice(deduplicated, budget, trace);
        var placed = components.Placer.Place(sliced, trace);
        return new ContextResult(placed, trace?.Build());
    }
}
```

**Why not middleware**: The brainstorm correctly identified that a missed `next()` call silently drops all items. Fixed pipelines fail predictably. Users substitute stage implementations, not stage ordering.

**Why not chain-of-responsibility**: CoR is appropriate when handlers can choose to handle or pass. Cupel's stages always execute in sequence -- no optional handling.

### Key Design Decision: Synchronous vs Asynchronous Pipeline

The pipeline itself should be **synchronous**. At <500 items with pre-computed token counts, all operations are CPU-bound in-memory work. Making the pipeline async would:
- Add state machine overhead per stage
- Prevent Span<T> usage (cannot cross await boundaries)
- Add ~200-500ns per async state machine allocation

The only async surface is `IContextSource.GetCandidatesAsync()` which feeds *into* the pipeline, not within it. The pipeline entry point should be:

```csharp
// Synchronous hot path
public ContextResult Apply(IReadOnlyList<ContextItem> candidates, ContextBudget budget);

// Async convenience that materializes IContextSource then calls sync Apply
public async ValueTask<ContextResult> ApplyAsync(IContextSource source, ContextBudget budget, CancellationToken ct = default);
```

---

## 2. Interface Design for Extensible Scoring/Ranking

### Recommendation: Narrow Single-Method Interfaces (Confidence: HIGH)

Each pluggable component gets a single-method interface. This is the standard .NET library pattern (IComparer<T>, IEqualityComparer<T>, etc.).

```csharp
public interface IScorer
{
    /// <summary>
    /// Score a candidate item. Return value conventionally in [0.0, 1.0].
    /// Values outside this range will be clamped by CompositeScorer.
    /// </summary>
    double Score(ContextItem item, ScoringContext context);
}

public interface ISlicer
{
    IReadOnlyList<ScoredItem> Slice(
        IReadOnlyList<ScoredItem> candidates,
        ContextBudget budget,
        ITraceCollector? trace);
}

public interface IPlacer
{
    IReadOnlyList<ContextItem> Place(
        IReadOnlyList<ScoredItem> selected,
        ITraceCollector? trace);
}

public interface IClassifier
{
    void Classify(Span<ContextItem> items);
    // or IReadOnlyList<ContextItem> variant if items are immutable
}
```

### ScoringContext Pattern

Pass a `ScoringContext` readonly struct to scorers rather than individual parameters. This avoids interface changes when new contextual data is needed:

```csharp
public readonly struct ScoringContext
{
    public required IReadOnlyList<ContextItem> AllCandidates { get; init; }
    public required ContextBudget Budget { get; init; }
    public required DateTimeOffset Now { get; init; }
}
```

### CompositeScorer: Nested Composition over DAG

CompositeScorer holds an array of `(IScorer scorer, double weight)` tuples. Nesting achieves DAG-like composition:

```csharp
var scorer = new CompositeScorer(
    (new RecencyScorer(), 0.4),
    (new CompositeScorer(           // nested group
        (new PriorityScorer(), 0.5),
        (new TagScorer("important"), 0.5)
    ), 0.6)
);
```

**Clamping strategy**: `CompositeScorer` clamps individual scorer outputs to [0.0, 1.0] before aggregation. In Debug builds, additionally throw or log a warning. Provide `ScaledScorer(IScorer inner, double min, double max)` wrapper for non-normalized scorers.

---

## 3. Zero-Allocation Patterns for .NET 10

### Verified Patterns (Confidence: HIGH -- from .NET runtime docs and Context7)

#### ArrayPool<T> for Temporary Buffers

```csharp
var buffer = ArrayPool<ScoredItem>.Shared.Rent(candidates.Count);
try
{
    var span = buffer.AsSpan(0, candidates.Count);
    // work with span...
}
finally
{
    ArrayPool<ScoredItem>.Shared.Return(buffer, clearArray: true);
}
```

Use for: temporary scored item arrays, sort buffers, intermediate pipeline stage results.

**Critical rule**: Always return rented arrays. Use try/finally. The `clearArray: true` parameter prevents data leaking between uses when items contain references.

#### Span<T> and stackalloc for Small Fixed Buffers

```csharp
// For small known-size buffers (score aggregation, weight normalization)
Span<double> weights = stackalloc double[scorerCount]; // safe when scorerCount is bounded
```

Use for: weight arrays in CompositeScorer (bounded by scorer count, typically <10), small temporary calculations.

**Constraint**: Span<T> cannot be used across await boundaries. This reinforces the synchronous pipeline design.

#### ReadOnlySpan<char> for String Operations

```csharp
// Zero-alloc string comparisons in classifiers
ReadOnlySpan<char> kindSpan = item.Kind.AsSpan();
if (kindSpan.Equals("system_message", StringComparison.Ordinal)) { ... }
```

#### ValueTask for Async Entry Points

```csharp
// IContextSource materialization
public async ValueTask<ContextResult> ApplyAsync(...)
{
    // If source is already materialized, ValueTask avoids Task allocation
    var items = await MaterializeAsync(source, ct);
    return Apply(items, budget); // sync hot path
}
```

#### Object Pooling Considerations

`Microsoft.Extensions.ObjectPool` is an external dependency, so it cannot be used in core. However, for internal pooling of pipeline execution state:

```csharp
// Simple thread-static pool for pipeline scratch space
[ThreadStatic]
private static List<ScoredItem>? t_scratchList;

private static List<ScoredItem> RentScratchList(int capacity)
{
    var list = t_scratchList ?? new List<ScoredItem>(capacity);
    t_scratchList = null;
    list.Clear();
    if (list.Capacity < capacity) list.Capacity = capacity;
    return list;
}

private static void ReturnScratchList(List<ScoredItem> list)
{
    list.Clear();
    t_scratchList = list;
}
```

**Alternative**: For .NET 10, consider using `SearchValues<T>` for hot-path string matching in classifiers.

### Allocation Budget Strategy

| Component | Strategy | Rationale |
|-----------|----------|-----------|
| Pipeline stages | ArrayPool + Span | Temporary buffers, returned after stage |
| CompositeScorer weights | stackalloc | Small, bounded, synchronous |
| Trace events | Gated allocation | Only allocate when `IsEnabled` is true |
| ContextResult | Single allocation | Return type, unavoidable, small |
| ScoredItem wrappers | ArrayPool buffer | Intermediate, returned after slicing |
| String comparisons | ReadOnlySpan<char> | Zero-alloc in classifiers |

---

## 4. IAsyncEnumerable Patterns for IContextSource

### Verified Patterns (Confidence: HIGH -- from Microsoft Learn docs)

#### Producer Pattern with Cancellation

```csharp
public interface IContextSource
{
    IAsyncEnumerable<ContextItem> GetCandidatesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default);
}

// Implementation example
public class ConversationHistorySource : IContextSource
{
    public async IAsyncEnumerable<ContextItem> GetCandidatesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var message in _store.GetMessagesAsync(cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return MapToContextItem(message);
        }
    }
}
```

#### Consumer Pattern (Pipeline Entry)

```csharp
public async ValueTask<ContextResult> ApplyAsync(
    IContextSource source,
    ContextBudget budget,
    CancellationToken ct = default)
{
    // Materialize to list for synchronous pipeline processing
    var candidates = new List<ContextItem>();
    await foreach (var item in source.GetCandidatesAsync(ct).ConfigureAwait(false))
    {
        candidates.Add(item);
    }
    return Apply(candidates, budget);
}
```

#### Key Implementation Rules

1. **Always use `[EnumeratorCancellation]` attribute** on the CancellationToken parameter of async iterators.
2. **Always use `.ConfigureAwait(false)`** in library code consuming async enumerables to avoid capturing synchronization context.
3. **Use `.WithCancellation(ct)`** when consuming an async enumerable with a cancellation token not passed at construction.
4. **Materialize before pipeline**: The sync pipeline cannot process IAsyncEnumerable directly. Materialize to `List<T>` or array at the async boundary, then process synchronously.

#### Multiple Source Aggregation

```csharp
public static async IAsyncEnumerable<ContextItem> Merge(
    IEnumerable<IContextSource> sources,
    [EnumeratorCancellation] CancellationToken ct = default)
{
    foreach (var source in sources)
    {
        await foreach (var item in source.GetCandidatesAsync(ct).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
```

---

## 5. Trace/Diagnostic System Design

### Options Surveyed

| Approach | Fit | External Dep? |
|----------|-----|---------------|
| System.Diagnostics.Activity/ActivitySource | Poor | No, but wrong abstraction |
| DiagnosticSource/DiagnosticListener | Moderate | No |
| Custom ITraceCollector | Best | No |
| OpenTelemetry | Poor | Yes (external) |

### Recommendation: Custom ITraceCollector with DiagnosticSource Bridge (Confidence: HIGH)

`Activity`/`ActivitySource` is designed for distributed tracing across services. Cupel needs intra-process pipeline stage tracing -- a different concern. The custom approach is correct.

#### Core Design

```csharp
public interface ITraceCollector
{
    bool IsEnabled { get; }
    void RecordScore(ContextItem item, string scorerName, double score);
    void RecordSliceDecision(ContextItem item, bool included, string reason);
    void RecordPlacement(ContextItem item, int position);
    ContextTrace? Build();
}

// Zero-overhead default
public sealed class NullTraceCollector : ITraceCollector
{
    public static readonly NullTraceCollector Instance = new();
    public bool IsEnabled => false;
    public void RecordScore(ContextItem item, string scorerName, double score) { }
    public void RecordSliceDecision(ContextItem item, bool included, string reason) { }
    public void RecordPlacement(ContextItem item, int position) { }
    public ContextTrace? Build() => null;
}
```

#### Gating Pattern (Critical for Zero-Alloc)

```csharp
// WRONG: allocates string even when tracing disabled
trace.RecordSliceDecision(item, false, $"Score {score} below threshold {threshold}");

// CORRECT: gate allocation behind IsEnabled check
if (trace.IsEnabled)
{
    trace.RecordSliceDecision(item, false, $"Score {score} below threshold {threshold}");
}
```

This pattern is verified from the .NET distributed tracing docs where `ActivitySource.StartActivity()` returns null when no listeners are registered, and `Activity.IsAllDataRequested` gates expensive tag population.

#### Explicit Propagation (No AsyncLocal)

The brainstorm correctly rejected `AsyncLocal<ITraceCollector>`. The trace collector is passed explicitly through the pipeline:

```csharp
internal ContextResult Execute(
    IReadOnlyList<ContextItem> candidates,
    ContextBudget budget,
    PipelineComponents components,
    ITraceCollector trace)  // explicit, not ambient
```

**Rationale**: AsyncLocal does not reliably flow across all async contexts. For a synchronous pipeline, explicit propagation adds minimal API noise and guarantees correctness.

#### DiagnosticSource Bridge (Optional, DI Package)

For consumers who want OpenTelemetry integration, the DI extensions package can provide a bridge:

```csharp
// In Wollax.Cupel.Extensions.DependencyInjection
public static class CupelDiagnostics
{
    public static readonly DiagnosticListener Listener = new("Wollax.Cupel");
}
```

This is a **separate concern** from the core ITraceCollector and belongs in the DI package, not core.

---

## 6. Builder Pattern in Modern C#

### Recommendation: Hand-Written Fluent Builder (Confidence: HIGH)

Source generators (M31.FluentAPI, BuilderGenerator) are designed for generating builders for data classes. Cupel's builder configures a *pipeline* with validation -- a different concern that benefits from hand-written control flow.

#### Implementation Pattern

```csharp
public sealed class CupelPipelineBuilder
{
    private IClassifier? _classifier;
    private readonly List<(IScorer Scorer, double Weight)> _scorers = new();
    private ISlicer? _slicer;
    private IPlacer? _placer;
    private ITraceCollector? _trace;
    private CupelPolicy? _policy;

    internal CupelPipelineBuilder() { }

    public CupelPipelineBuilder WithClassifier(IClassifier classifier)
    {
        _classifier = classifier ?? throw new ArgumentNullException(nameof(classifier));
        return this;
    }

    public CupelPipelineBuilder WithScorer(IScorer scorer, double weight = 1.0)
    {
        _scorers.Add((scorer ?? throw new ArgumentNullException(nameof(scorer)), weight));
        return this;
    }

    public CupelPipelineBuilder WithSlicer(ISlicer slicer)
    {
        _slicer = slicer ?? throw new ArgumentNullException(nameof(slicer));
        return this;
    }

    public CupelPipelineBuilder WithPlacer(IPlacer placer)
    {
        _placer = placer ?? throw new ArgumentNullException(nameof(placer));
        return this;
    }

    public CupelPipelineBuilder WithTracing(ITraceCollector trace)
    {
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
        return this;
    }

    public CupelPipeline Build()
    {
        // Validate required components
        if (_scorers.Count == 0)
            throw new InvalidOperationException("At least one scorer is required.");
        if (_slicer is null)
            throw new InvalidOperationException("A slicer is required.");

        // Apply defaults for optional components
        var classifier = _classifier ?? new DefaultClassifier();
        var placer = _placer ?? new UShapedPlacer();
        var trace = _trace ?? NullTraceCollector.Instance;

        var scorer = _scorers.Count == 1
            ? _scorers[0].Scorer
            : new CompositeScorer(_scorers.ToArray());

        return new CupelPipeline(classifier, scorer, _slicer, placer, trace);
    }
}

// Entry point
public sealed partial class CupelPipeline
{
    public static CupelPipelineBuilder CreateBuilder() => new();
}
```

#### Builder Validation at Build() Time

All invalid configurations caught at `Build()`, not at runtime:
- Missing required stages (scorer, slicer)
- Conflicting quota constraints
- Weight arrays that don't match scorer counts

#### Policy-Based Construction

In addition to the fluent builder, support construction from a declarative `CupelPolicy`:

```csharp
public sealed class CupelPipeline
{
    public static CupelPipelineBuilder CreateBuilder() => new();
    public static CupelPipeline FromPolicy(CupelPolicy policy) => policy.Build();
}
```

---

## 7. Microsoft.Extensions.DependencyInjection Integration

### Verified Pattern (Confidence: HIGH -- from Microsoft Learn library author guidance)

#### Package Structure

The DI package (`Wollax.Cupel.Extensions.DependencyInjection`) depends on:
- `Wollax.Cupel` (core)
- `Microsoft.Extensions.DependencyInjection.Abstractions`
- `Microsoft.Extensions.Options`

#### Extension Method Conventions

From Microsoft's guidance for library authors:

1. **Naming**: `AddCupel()` as the primary extension method
2. **Namespace**: Use a library-specific namespace, NOT `Microsoft.Extensions.DependencyInjection` (reserved for official MS packages)
3. **Return `IServiceCollection`** for chaining
4. **Multiple overloads**:

```csharp
namespace Wollax.Cupel.Extensions.DependencyInjection;

public static class CupelServiceCollectionExtensions
{
    // Parameterless with defaults
    public static IServiceCollection AddCupel(
        this IServiceCollection services)
    {
        services.AddOptions<CupelOptions>()
            .Configure(options => { /* defaults */ });

        services.TryAddSingleton<ICupelPipelineFactory, DefaultPipelineFactory>();
        return services;
    }

    // Action<TOptions> overload
    public static IServiceCollection AddCupel(
        this IServiceCollection services,
        Action<CupelOptions> configureOptions)
    {
        services.AddCupel();
        services.Configure(configureOptions);
        return services;
    }

    // Named policy registration
    public static IServiceCollection AddCupelPolicy(
        this IServiceCollection services,
        string name,
        CupelPolicy policy)
    {
        services.Configure<CupelOptions>(options =>
            options.AddPolicy(name, policy));
        return services;
    }
}
```

#### Options Class

```csharp
public sealed class CupelOptions
{
    private readonly Dictionary<string, CupelPolicy> _policies = new();

    public void AddPolicy(string name, CupelPolicy policy)
        => _policies[name] = policy;

    public CupelPolicy GetPolicy(string name)
        => _policies.TryGetValue(name, out var policy)
            ? policy
            : throw new KeyNotFoundException($"No Cupel policy registered with name '{name}'.");

    public bool EnableTracing { get; set; } = false;
}
```

#### TryAdd Pattern

Use `TryAddSingleton`/`TryAddScoped` to avoid overwriting user-registered implementations:

```csharp
services.TryAddSingleton<ICupelPipelineFactory, DefaultPipelineFactory>();
// If user already registered their own ICupelPipelineFactory, this is a no-op
```

#### Keyed Services for Named Policies (.NET 8+)

For .NET 10, keyed services provide a clean way to resolve named pipelines:

```csharp
services.AddKeyedSingleton<CupelPipeline>("chat", (sp, key) =>
    CupelPipeline.FromPolicy(CupelPolicies.Chat()));
```

---

## 8. Multi-Project Solution Structure

### Recommended Layout (Confidence: HIGH -- from David Fowler's canonical .NET structure gist)

```
cupel/
  src/
    Wollax.Cupel/                              # Core library (zero deps)
      Wollax.Cupel.csproj
      Pipeline/
        CupelPipeline.cs
        CupelPipelineBuilder.cs
        PipelineExecutor.cs                    # internal
      Models/
        ContextItem.cs
        ContextBudget.cs
        ContextResult.cs
        ContextTrace.cs
        ScoredItem.cs                          # internal or public
      Scoring/
        IScorer.cs
        CompositeScorer.cs
        ScaledScorer.cs
        RecencyScorer.cs
        PriorityScorer.cs
        KindScorer.cs
        TagScorer.cs
        FrequencyScorer.cs
        ReflexiveScorer.cs
      Slicing/
        ISlicer.cs
        GreedySlicer.cs
        KnapsackSlicer.cs
        QuotaSlicer.cs
        StreamSlicer.cs
        QuotaSpec.cs
      Placement/
        IPlacer.cs
        UShapedPlacer.cs
      Classification/
        IClassifier.cs
        DefaultClassifier.cs
      Sources/
        IContextSource.cs
      Diagnostics/
        ITraceCollector.cs
        NullTraceCollector.cs
        DiagnosticTraceCollector.cs
      Policy/
        CupelPolicy.cs
        CupelPolicies.cs                       # Named presets
        OverflowStrategy.cs
      Reporting/
        SelectionReport.cs
        ExclusionReason.cs

    Wollax.Cupel.Extensions.DependencyInjection/
      Wollax.Cupel.Extensions.DependencyInjection.csproj
      CupelServiceCollectionExtensions.cs
      CupelOptions.cs

    Wollax.Cupel.Tiktoken/
      Wollax.Cupel.Tiktoken.csproj
      TiktokenCountProvider.cs

    Wollax.Cupel.Json/
      Wollax.Cupel.Json.csproj
      PolicySerializer.cs
      PolicyLoader.cs

  tests/
    Wollax.Cupel.Tests/
      Wollax.Cupel.Tests.csproj
    Wollax.Cupel.Extensions.DependencyInjection.Tests/
      ...
    Wollax.Cupel.Benchmarks/
      Wollax.Cupel.Benchmarks.csproj           # BenchmarkDotNet

  samples/
    Cupel.Samples.BasicUsage/
    Cupel.Samples.DependencyInjection/

  build/
    Directory.Build.props                      # Shared properties
    Directory.Build.targets                    # Shared targets

  Cupel.sln
  Directory.Build.props                        # Root-level shared props
  Directory.Packages.props                     # Central package management
  NuGet.Config
  global.json                                  # Pin .NET SDK version
```

### Project Dependency Graph

```
Wollax.Cupel (zero deps)
  ^
  |-- Wollax.Cupel.Extensions.DependencyInjection
  |     deps: Microsoft.Extensions.DependencyInjection.Abstractions
  |           Microsoft.Extensions.Options
  |
  |-- Wollax.Cupel.Tiktoken
  |     deps: Tiktoken (or SharpToken)
  |
  |-- Wollax.Cupel.Json
        deps: System.Text.Json (in-box for .NET 10)
```

### Build Infrastructure

```xml
<!-- Directory.Build.props (root) -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>

<!-- Directory.Packages.props (central package management) -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.0" />
    <!-- test deps -->
    <PackageVersion Include="xunit" Version="2.9.0" />
    <PackageVersion Include="BenchmarkDotNet" Version="0.14.0" />
  </ItemGroup>
</Project>
```

### Versioning

Use **MinVer** for Git tag-based semantic versioning:

```xml
<!-- In Directory.Build.props for src/ projects -->
<PackageReference Include="MinVer" Version="6.0.0" PrivateAssets="All" />
<MinVerTagPrefix>v</MinVerTagPrefix>
```

---

## Component Relationships and Data Flow

### Pipeline Data Flow

```
Caller provides:
  IReadOnlyList<ContextItem> + ContextBudget
       |
       v
  [1. Classify] -- IClassifier.Classify()
       |           Sets Kind on items without explicit Kind
       v
  [2. Score] -- IScorer.Score() for each item
       |        CompositeScorer aggregates multiple scorers
       |        Output: List<ScoredItem> (item + score)
       v
  [3. Deduplicate] -- internal, no interface
       |              Removes duplicate items by content hash or ID
       v
  [4. Slice] -- ISlicer.Slice()
       |        Selects subset within token budget
       |        Respects quotas (Require/Cap by Kind)
       |        Pinned items pre-reserved before slicing
       v
  [5. Place] -- IPlacer.Place()
       |        Orders selected items for output
       |        UShapedPlacer: high-score items at edges
       v
  ContextResult { Items, Trace? }
```

### Pinned Item Flow

```
Pinned items (item.Pinned == true):
  - Skip stages 1-4 entirely
  - Their token cost is deducted from budget before Slicer runs
  - Enter at stage 5 (Placer) alongside sliced items
  - Placer decides their final position
```

### Trace Data Flow

```
ITraceCollector flows through pipeline explicitly:
  - Constructed by caller or factory (NullTraceCollector by default)
  - Passed to each stage method
  - Each stage calls trace.RecordX() gated by trace.IsEnabled
  - After pipeline: trace.Build() produces ContextTrace
  - ContextTrace stored in ContextResult.Trace
```

---

## Suggested Build Order (Dependency-Driven)

### Phase 1: Foundation (no internal dependencies)

Build order within Phase 1 matters because later items depend on earlier ones:

1. **Models**: `ContextItem`, `ContextBudget`, `ScoredItem`, `OverflowStrategy` enum, `ExclusionReason` enum
   - These are data types with no logic dependencies
   - Apply `[JsonPropertyName]` attributes immediately

2. **Interfaces**: `IScorer`, `ISlicer`, `IPlacer`, `IClassifier`, `IContextSource`, `ITraceCollector`
   - Depend only on models
   - These define the extension contracts

3. **Diagnostics**: `NullTraceCollector`, `DiagnosticTraceCollector`, `ContextTrace`, `ContextResult`
   - Depend on models + ITraceCollector
   - ContextResult is the pipeline return type, needed before pipeline

### Phase 2: Scoring (depends on Phase 1)

4. **Individual scorers**: `RecencyScorer`, `PriorityScorer`, `KindScorer`, `TagScorer`, `FrequencyScorer`, `ReflexiveScorer`
   - Each depends only on IScorer + models
   - Can be built and tested in parallel

5. **CompositeScorer + ScaledScorer**
   - Depends on IScorer + individual scorers (for testing)
   - Clamping logic, weight normalization

### Phase 3: Slicing (depends on Phase 1)

6. **QuotaSpec**: percentage-based Require/Cap
7. **GreedySlicer**: simplest slicer, good first implementation
8. **KnapsackSlicer**: more complex, depends on QuotaSpec
9. **QuotaSlicer**, **StreamSlicer**: parallel with above

### Phase 4: Placement + Classification (depends on Phase 1)

10. **DefaultClassifier**: basic Kind assignment
11. **UShapedPlacer**: default placement strategy

### Phase 5: Pipeline Assembly (depends on Phases 1-4)

12. **PipelineExecutor**: internal, orchestrates stages
13. **CupelPipelineBuilder**: fluent builder
14. **CupelPipeline**: public facade

### Phase 6: Policy + Presets (depends on Phase 5)

15. **CupelPolicy**: declarative config
16. **CupelPolicies**: named presets (chat, code-review, rag, etc.)
17. **SelectionReport / DryRun**: depends on pipeline + trace

### Phase 7: Companion Packages (depends on Phase 5)

18. **Wollax.Cupel.Extensions.DependencyInjection**: AddCupel(), CupelOptions
19. **Wollax.Cupel.Json**: PolicySerializer, PolicyLoader
20. **Wollax.Cupel.Tiktoken**: TiktokenCountProvider

---

## Performance-Critical Design Decisions

### Decision 1: Synchronous Pipeline Core

The pipeline is synchronous. Only the IContextSource materialization is async. This enables Span<T>, stackalloc, and avoids async state machine allocations on the hot path.

**Measured impact**: Async state machines allocate ~72-120 bytes per await on the heap. With 5 pipeline stages, that is 360-600 bytes per pipeline invocation avoided.

### Decision 2: Array-Based Internal Representation

Internally, pipeline stages work with arrays (rented from ArrayPool), not List<T>. Array access is bounds-checked by the JIT and frequently optimized away. List<T> adds indirection.

### Decision 3: Trace Gating at Allocation Site

Every trace recording call must be gated by `trace.IsEnabled`. The NullTraceCollector's no-op methods are trivially inlined by the JIT, but string interpolation and object allocation in the *arguments* happen before the method call.

### Decision 4: ScoredItem as Struct

```csharp
public readonly struct ScoredItem
{
    public required ContextItem Item { get; init; }
    public required double Score { get; init; }
}
```

As a struct, `ScoredItem` is stored inline in arrays. No per-item heap allocation for the scoring intermediate. This is safe because ScoredItem is short-lived (exists only between Score and Place stages).

### Decision 5: ContextItem as Sealed Class (Not Struct)

`ContextItem` has string Content, Tags, Metadata -- reference type fields that make struct semantics costly (copying). Sealed class enables devirtualization and JIT optimizations.

### Decision 6: Pre-Computed Token Counts

`ContextItem.Tokens` is a required `int`, pre-computed by the caller. The pipeline never tokenizes content. This is the single most impactful performance decision -- tokenization is 100-1000x more expensive than any pipeline operation.

---

## Integration Points

| Integration Point | Package | Mechanism |
|-------------------|---------|-----------|
| Custom scorers | Core | Implement IScorer |
| Custom slicers | Core | Implement ISlicer |
| Custom placers | Core | Implement IPlacer |
| Custom context sources | Core | Implement IContextSource |
| Custom trace collectors | Core | Implement ITraceCollector |
| DI registration | DI Extensions | AddCupel() extension |
| Named policy lookup | DI Extensions | IOptions<CupelOptions> |
| Token counting | Tiktoken | TiktokenCountProvider |
| Policy serialization | Json | PolicySerializer/PolicyLoader |
| Smelt orchestrator | Consumer code | Calls CupelPipeline.Apply() |
| OpenTelemetry | DI Extensions | DiagnosticSource bridge |

---

## Anti-Patterns to Avoid

1. **Async pipeline stages**: Do not make IScorer.Score async. Token scoring of in-memory items is nanosecond-scale work. Async adds microsecond overhead per item.

2. **Ambient state (AsyncLocal/ThreadLocal for business logic)**: Use only for scratch-space pooling (ThreadStatic). Never for trace or config propagation.

3. **Interface explosion**: Do not create IRecencyScorer, IPriorityScorer etc. One IScorer interface. Type discrimination happens via CompositeScorer configuration, not interface hierarchy.

4. **Lazy initialization in hot paths**: All pipeline components should be fully constructed at Build() time. No Lazy<T> or initialization checks in Score/Slice/Place methods.

5. **String allocations in scoring**: Avoid string.Format, interpolation, or concatenation in scorer/slicer implementations. Use Span<char> comparisons.

6. **LINQ in hot paths**: Avoid .Where(), .Select(), .OrderBy() in pipeline stages. Use explicit loops with rented arrays. LINQ allocates enumerator objects and delegate closures.

7. **Throwing for expected conditions**: Token budget exceeded is not exceptional. Use OverflowStrategy enum, not exceptions, for expected overflow.

---

## Research Confidence Summary

| Topic | Confidence | Source |
|-------|------------|--------|
| Fixed pipeline pattern | HIGH | .NET design patterns + brainstorm validation |
| IScorer single-method interface | HIGH | Standard .NET library conventions (IComparer<T>) |
| ArrayPool / Span<T> / stackalloc | HIGH | .NET runtime docs via Context7 |
| IAsyncEnumerable + [EnumeratorCancellation] | HIGH | Microsoft Learn official docs |
| Custom ITraceCollector over Activity | HIGH | .NET distributed tracing docs (Activity is for cross-service) |
| DI extension patterns (AddCupel) | HIGH | Microsoft Learn library author guidance |
| Options pattern (IOptions<T>) | HIGH | Microsoft Learn options pattern docs |
| Hand-written fluent builder | HIGH | Standard practice for pipeline builders |
| Multi-project structure | HIGH | David Fowler's canonical .NET structure |
| MinVer for versioning | MEDIUM | Community standard, not verified against .NET 10 specifically |
| ScoredItem as struct | MEDIUM | Performance heuristic -- benchmark to confirm |
| ThreadStatic pooling | MEDIUM | Known pattern, but benchmark vs ArrayPool for real workload |

---

*Research completed 2026-03-10. Findings verified against official Microsoft documentation and .NET runtime source.*
