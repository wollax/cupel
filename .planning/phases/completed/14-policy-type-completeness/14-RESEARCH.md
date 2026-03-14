# Phase 14: Policy Type Completeness - Research

**Researched:** 2026-03-14
**Domain:** C# enum/policy extension, JSON serialization, DI lifetime management
**Confidence:** HIGH

## Summary

This phase closes integration gaps where `ScaledScorer` and `StreamSlice` exist as implementations but are unreachable from the declarative policy (`CupelPolicy`), JSON serialization, and DI paths. Additionally, DI lifetimes must be corrected from per-resolve transient to singleton for scorers, slicers, and placers.

The codebase is well-structured with clear patterns. Adding new enum values (`ScorerType.Scaled`, `SlicerType.Stream`) and wiring them through the existing `ScorerEntry` → `PipelineBuilder.CreateScorer()` → `PipelineBuilder.WithPolicy()` chain follows established conventions. The JSON serialization uses source-generated `System.Text.Json` with `[JsonStringEnumMemberName]` attributes. DI uses `Microsoft.Extensions.DependencyInjection` keyed services.

**Primary recommendation:** Follow the existing pattern exactly. Add enum values, extend `ScorerEntry` for nesting, update `CreateScorer()` switch, update `WithPolicy()` switch, extend `BuiltInScorerTypes` (or refactor to derive from enum), and change DI registrations from `AddKeyedTransient` to a pattern that caches components as singletons.

## Standard Stack

No new libraries needed. All work uses existing dependencies:

### Core
| Library | Version | Purpose | Why Standard |
| --- | --- | --- | --- |
| System.Text.Json | .NET 10 built-in | JSON serialization with source gen | Already in use, AOT-compatible |
| Microsoft.Extensions.DependencyInjection | 10.x | DI container | Already in use |
| Microsoft.Extensions.Options | 10.x | Options pattern | Already in use |
| TUnit | current | Test framework | Already in use throughout |

### No New Dependencies Required

This phase is purely additive wiring — no new libraries, no new NuGet packages.

## Architecture Patterns

### Pattern 1: Enum Extension with JsonStringEnumMemberName

**What:** All existing enum values use `[JsonStringEnumMemberName("lowercase")]` attributes for JSON representation.
**When to use:** Every new enum value must follow this pattern.

Existing pattern (from `ScorerType.cs`):
```csharp
[JsonStringEnumMemberName("reflexive")]
Reflexive
```

New values should follow:
```csharp
[JsonStringEnumMemberName("scaled")]
Scaled
```
```csharp
[JsonStringEnumMemberName("stream")]
Stream
```

**Confidence:** HIGH — directly observed in codebase.

### Pattern 2: ScorerEntry Nesting for ScaledScorer

**What:** `ScorerEntry` currently has type-specific optional properties (`KindWeights`, `TagWeights`). ScaledScorer wraps another scorer, requiring a nested representation.

**Recommendation:** Add `ScorerEntry? InnerScorer` property (nullable, analogous to existing optional properties). This approach:
- Follows the same "optional property per-type" pattern as `KindWeights`/`TagWeights`
- Enables recursive nesting (ScaledScorer wrapping any scorer type, including Composite-like configurations)
- Produces natural JSON shape: `{ "type": "scaled", "weight": 1.0, "innerScorer": { "type": "recency", "weight": 1.0 } }`

**Validation:** Constructor must enforce `InnerScorer != null` when `Type == Scaled`, analogous to `TagWeights != null` when `Type == Tag`.

**Builder API evidence:** `ScaledScorer` accepts any `IScorer` in its constructor (no restriction on inner type). `CompositeScorer.DetectCycles()` already traverses `ScaledScorer.Inner`. This means ScaledScorer can wrap any scorer type including Composite or other Scaled — the builder API already supports full nesting depth.

**Confidence:** HIGH — existing patterns observed, validated against builder API.

### Pattern 3: PipelineBuilder.CreateScorer Switch Extension

**What:** `PipelineBuilder.CreateScorer()` uses a switch expression mapping `ScorerType` → `IScorer` instances.

For `ScorerType.Scaled`, the factory must recursively call itself to construct the inner scorer:
```csharp
ScorerType.Scaled => new ScaledScorer(CreateScorer(entry.InnerScorer!)),
```

**Confidence:** HIGH — directly observed in `PipelineBuilder.CreateScorer()`.

### Pattern 4: SlicerType.Stream in WithPolicy

**What:** `PipelineBuilder.WithPolicy()` has a switch on `SlicerType` that currently handles `Greedy` and `Knapsack`. Adding `Stream` requires calling `WithAsyncSlicer(new StreamSlice(...))`.

**Key design question:** StreamSlice implements `IAsyncSlicer`, not `ISlicer`. The current `WithPolicy()` sets `_slicer` (ISlicer) but Stream requires `_asyncSlicer`. The policy should wire StreamSlice via `WithAsyncSlicer()`, while still setting a sync slicer fallback (probably GreedySlice as default).

**Sync call behavior:** When `SlicerType.Stream` is declared and sync `Execute()` is called, `CupelPipeline.Execute()` uses `_slicer` (not `_asyncSlicer`). Only `ExecuteStreamAsync()` uses `_asyncSlicer`. So declaring Stream in policy should:
1. Set `_asyncSlicer = new StreamSlice(batchSize)`
2. Keep a sync slicer fallback (GreedySlice) for non-streaming execution
3. This is consistent — the builder API already allows calling both `WithSlicer()` and `WithAsyncSlicer()` on the same pipeline

**BatchSize configurability:** `StreamSlice` accepts `batchSize` with default 32. The policy could expose an optional `StreamBatchSize` property (analogous to `KnapsackBucketSize`). Recommendation: expose it — follows the exact same pattern.

**Confidence:** HIGH — validated against `CupelPipeline.ExecuteStreamAsync()` which checks `_asyncSlicer is null`.

### Pattern 5: DI Singleton Components

**What:** The current DI registration creates pipelines with `AddKeyedTransient`, invoking `WithPolicy().WithBudget().Build()` on every resolve. This means scorers, slicers, and placers are recreated per resolve.

The user-specified contract:
- Pipeline: **transient** (new instance per resolve) — keep as-is
- Scorers/slicers/placers: **singleton** (shared across pipeline instances)
- ITraceCollector: **transient** — keep as-is

**Implementation approach:** In the `AddCupelPipeline` factory lambda, build the components once and capture them in the closure. On subsequent resolves, create new `CupelPipeline` instances reusing the same component objects.

Concrete approach:
```csharp
services.AddKeyedTransient<CupelPipeline>(intent, (provider, _) =>
{
    // This lambda captures the shared instances via lazy initialization
    // ...
});
```

Better: use a separate singleton registration for the components, or use `Lazy<T>` within the factory closure to ensure thread-safe one-time initialization.

Simplest correct approach: Use `AddKeyedSingleton` for a "resolved components" holder, then `AddKeyedTransient` for the pipeline that wraps those components. But this adds complexity. Alternative: cache in the closure itself using `Lazy<T>`.

**Recommendation:** Create a small internal `PolicyComponents` record/class that holds the built `IScorer`, `ISlicer`, `IPlacer`, `IAsyncSlicer?`. Register this as a keyed singleton. The transient pipeline factory resolves the singleton components and creates a new `CupelPipeline` wrapping them.

This requires `CupelPipeline` to accept pre-built components — it already does via its `internal` constructor.

**Confidence:** HIGH — `CupelPipeline` constructor is `internal` and accepts all components directly.

### Pattern 6: BuiltInScorerTypes Refactoring

**What:** `CupelJsonSerializer` has a hardcoded `string[] BuiltInScorerTypes` that must stay in sync with `ScorerType` enum values. The user decision is to derive this from enum values instead.

Implementation:
```csharp
private static readonly string[] BuiltInScorerTypes = Enum.GetValues<ScorerType>()
    .Select(t => /* get JsonStringEnumMemberName attribute value */)
    .ToArray();
```

To get the `[JsonStringEnumMemberName]` value, use reflection on the enum field:
```csharp
typeof(ScorerType).GetField(t.ToString())!
    .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name
```

Note: `JsonStringEnumMemberNameAttribute` was introduced in .NET 9. The `Name` property contains the serialized string value.

**Confidence:** HIGH — `JsonStringEnumMemberNameAttribute` is a public type in System.Text.Json.

### Anti-Patterns to Avoid

- **Breaking existing constructor signatures:** `ScorerEntry` and `CupelPolicy` constructors are `[JsonConstructor]`. New parameters MUST be optional with defaults to preserve backward compatibility.
- **Adding required JSON properties:** New properties in `ScorerEntry`/`CupelPolicy` must be nullable/optional so existing JSON documents remain valid.
- **Weakening Enum.IsDefined checks:** Both `ScorerEntry` and `CupelPolicy` constructors call `Enum.IsDefined()`. New enum values are automatically handled by this — no code change needed for validation.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
| --- | --- | --- | --- |
| Enum → JSON name mapping | Manual string-to-enum map | `[JsonStringEnumMemberName]` + source gen | Already in use, consistent, AOT-safe |
| Scorer tree construction | Custom recursive builder | Recursive `CreateScorer()` with `InnerScorer` | Matches existing pattern |
| DI singleton caching | Manual `ConcurrentDictionary` | Keyed singleton registration | Framework handles thread safety |
| Enum value discovery | Hardcoded arrays | `Enum.GetValues<T>()` + reflection | User decision to eliminate drift |

## Common Pitfalls

### Pitfall 1: Source Generator Invalidation
**What goes wrong:** Adding new properties to `ScorerEntry` or `CupelPolicy` without updating `CupelJsonContext` can cause silent serialization failures with source-generated JSON.
**Why it happens:** The `[JsonSerializable(typeof(CupelPolicy))]` attribute triggers source generation at compile time. New types or structural changes may need source gen regeneration.
**How to avoid:** After adding new properties, verify serialization round-trips in tests. The source generator should handle new nullable properties automatically since `CupelPolicy` is already registered.
**Warning signs:** Properties silently missing from serialized JSON, or null after deserialization.

### Pitfall 2: Enum Value Ordering
**What goes wrong:** Inserting new enum values in the middle changes ordinal values of subsequent members.
**Why it happens:** C# enum values default to sequential integers.
**How to avoid:** Add `Scaled` and `Stream` at the end of their respective enums with explicit integer values:
```csharp
Scaled = 6  // After Reflexive = 5
Stream = 2  // After Knapsack = 1
```
**Warning signs:** Existing serialized data deserializing to wrong enum values (unlikely with string serialization, but relevant for binary).

### Pitfall 3: Recursive ScorerEntry Without Depth Limit
**What goes wrong:** Deeply nested ScaledScorer chains could cause stack overflow during construction or scoring.
**Why it happens:** No depth limit on `InnerScorer` nesting.
**How to avoid:** Accept this for now — `CompositeScorer.DetectCycles()` already handles cycle detection for the scorer graph. Depth is bounded by the JSON document size and user construction. Not a practical concern for v1.0 gap closure.
**Warning signs:** Stack overflow in deeply nested configurations (extremely unlikely in practice).

### Pitfall 4: DI Singleton Closure Capture Timing
**What goes wrong:** If singleton components are built eagerly at registration time, they may miss options configured later in the service collection.
**Why it happens:** `services.AddCupel()` configures `IOptions<CupelOptions>`, which is resolved lazily. Building components at registration time would bypass the options pattern.
**How to avoid:** Use lazy initialization — build components on first resolve, cache for subsequent resolves. The keyed singleton approach handles this naturally since singletons are resolved (not registered) lazily.
**Warning signs:** "No policy registered for intent" errors at first resolve.

### Pitfall 5: CupelPolicy Constructor Parameter Count
**What goes wrong:** Adding `streamBatchSize` parameter to `CupelPolicy` constructor makes the already-long parameter list even longer.
**Why it happens:** CupelPolicy uses a single constructor with many optional parameters.
**How to avoid:** Follow the existing pattern — `knapsackBucketSize` is already an optional parameter gated on `SlicerType`. Add `streamBatchSize` with the same pattern. The `[JsonConstructor]` attribute handles deserialization.
**Warning signs:** Constructor with 10+ parameters. Acceptable for a data object with `[JsonConstructor]` — this is not a code smell for DTO/policy classes.

## Code Examples

### ScorerType Enum Extension
```csharp
// Add at end of ScorerType enum (ScorerType.cs)
/// <summary>Wraps another scorer and normalizes its output to [0, 1].</summary>
[JsonStringEnumMemberName("scaled")]
Scaled
```

### SlicerType Enum Extension
```csharp
// Add at end of SlicerType enum (SlicerType.cs)
/// <summary>Online streaming selection via configurable micro-batches.</summary>
[JsonStringEnumMemberName("stream")]
Stream
```

### ScorerEntry InnerScorer Property
```csharp
/// <summary>
/// Inner scorer entry for the <see cref="ScorerType.Scaled"/> type.
/// Must be specified when <see cref="Type"/> is Scaled.
/// </summary>
[JsonPropertyName("innerScorer")]
public ScorerEntry? InnerScorer { get; }
```

### CupelPolicy StreamBatchSize Property
```csharp
/// <summary>
/// Batch size for the stream slicer. Must be null when <see cref="SlicerType"/> is not
/// <see cref="Cupel.SlicerType.Stream"/>, and must be positive when specified.
/// </summary>
[JsonPropertyName("streamBatchSize")]
public int? StreamBatchSize { get; }
```

### PipelineBuilder.CreateScorer Extension
```csharp
ScorerType.Scaled => new ScaledScorer(CreateScorer(entry.InnerScorer
    ?? throw new InvalidOperationException("InnerScorer is required for Scaled type."))),
```

### PipelineBuilder.WithPolicy SlicerType.Stream Case
```csharp
case SlicerType.Stream:
    UseGreedySlice(); // sync fallback
    WithAsyncSlicer(new StreamSlice(policy.StreamBatchSize ?? 32));
    break;
```

### DI Singleton Component Pattern
```csharp
// Register singleton components holder
services.AddKeyedSingleton(intent, (provider, _) =>
{
    var options = provider.GetRequiredService<IOptions<CupelOptions>>().Value;
    if (!options.TryGetPolicy(intent, out var policy))
        throw new InvalidOperationException(...);

    var builder = CupelPipeline.CreateBuilder().WithPolicy(policy);
    // Extract components... (requires internal access or builder method)
    return new ResolvedComponents(scorer, slicer, placer, asyncSlicer);
});

// Register transient pipeline
services.AddKeyedTransient<CupelPipeline>(intent, (provider, _) =>
{
    var components = provider.GetRequiredKeyedService<ResolvedComponents>(intent);
    return new CupelPipeline(
        components.Scorer, components.Slicer, components.Placer,
        budget, policy.DeduplicationEnabled, components.AsyncSlicer,
        policy.OverflowStrategy);
});
```

### BuiltInScorerTypes Derived from Enum
```csharp
private static readonly string[] BuiltInScorerTypes = Enum.GetValues<ScorerType>()
    .Select(t => typeof(ScorerType)
        .GetField(t.ToString())!
        .GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!
        .Name)
    .ToArray();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
| --- | --- | --- | --- |
| Hardcoded scorer type arrays | Enum-derived (this phase) | Phase 14 | Eliminates drift between enum and JSON detection |
| Per-resolve components | Singleton components | Phase 14 | Matches ROADMAP specification |

## Open Questions

### 1. CupelPipeline Internal Constructor Access from DI Package
- What we know: `CupelPipeline` constructor is `internal`. The DI package currently uses `PipelineBuilder.Build()` which creates the pipeline. `InternalsVisibleTo` is NOT configured from `Wollax.Cupel` → `Wollax.Cupel.Extensions.DependencyInjection` (only test projects have internal visibility).
- What's unclear: Best approach for singleton component sharing without breaking encapsulation.
- Recommendation: The simplest correct approach is to cache the entire built `CupelPipeline` as the "components holder" (since it's immutable and holds all components), but that defeats the transient pipeline goal. Better: add `InternalsVisibleTo` for the DI assembly (it's a first-party companion package, same pattern as `Wollax.Cupel.Json`). Alternative: expose a public `PipelineBuilder` method that builds components without the budget, then a separate method to create a pipeline from those components. The `InternalsVisibleTo` approach is simplest and already used for test projects in this exact codebase.

### 2. QuotaSlice + StreamSlice Composition
- What we know: `QuotaSlice` wraps `ISlicer` (sync). `StreamSlice` implements `IAsyncSlicer` (async). They are incompatible for direct composition.
- What's unclear: Whether the declarative path should support `quotas + stream` combination.
- Recommendation: Don't support this combination in Phase 14. QuotaSlice is sync-only and cannot wrap an async slicer. If both quotas and stream are declared, validation should throw at policy application time. This mirrors the builder API which keeps them separate (`WithQuotas` wraps sync slicer, `WithAsyncSlicer` is independent).

### 3. PublicAPI File Placement
- What we know: All current entries are in `PublicAPI.Shipped.txt`. `PublicAPI.Unshipped.txt` is empty (just `#nullable enable`).
- What's unclear: Whether new enum values and properties should go in Shipped or Unshipped.
- Recommendation: Place in `PublicAPI.Unshipped.txt` since this is new unreleased API surface. Move to Shipped on next release.

## Sources

### Primary (HIGH confidence)
- Direct codebase inspection: `ScorerType.cs`, `SlicerType.cs`, `ScorerEntry.cs`, `CupelPolicy.cs`, `PipelineBuilder.cs`, `CupelPipeline.cs`, `ScaledScorer.cs`, `StreamSlice.cs`, `CompositeScorer.cs`, `CupelServiceCollectionExtensions.cs`, `CupelJsonSerializer.cs`, `CupelJsonContext.cs`, `CupelJsonOptions.cs`, `QuotaSlice.cs`, `CupelPresets.cs`
- PublicAPI surface files: all four packages inspected
- Existing test patterns: `RoundTripTests.cs`, `CupelServiceCollectionExtensionsTests.cs`

### Secondary (MEDIUM confidence)
- .NET 10 `JsonStringEnumMemberNameAttribute` API — introduced in .NET 9, verified via training data and attribute usage already present in the codebase

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies, all patterns directly observed
- Architecture: HIGH — every extension point directly inspected in source
- Pitfalls: HIGH — identified from structural analysis of existing code
- DI singleton approach: HIGH — `InternalsVisibleTo` not configured for DI package but straightforward to add; pattern already established in codebase

**Research date:** 2026-03-14
**Valid until:** 2026-04-14 (stable codebase, no external dependency changes expected)
