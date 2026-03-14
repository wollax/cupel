# Phase 8: Policy System & Named Presets - Research

**Completed:** 2026-03-13
**Confidence:** HIGH (all findings based on direct codebase reading; no external dependencies)

---

## Standard Stack

No new external dependencies. Everything is internal C# using existing codebase patterns:

- **Sealed classes** with constructor validation (pattern: `ContextBudget`, `QuotaSet`)
- **Fluent builders** with `Build()` validation (pattern: `PipelineBuilder`, `QuotaBuilder`)
- **`System.Diagnostics.CodeAnalysis.ExperimentalAttribute`** (C# 12 / .NET 8+) for preset markers
- **`System.Text.Json`** attributes for serialization readiness (pattern: `ContextBudget`, `ContextItem`)
- **TUnit** for tests (not xUnit/NUnit — codebase uses `TUnit.Core`, `TUnit.Assertions`)
- **`PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`** tracked via `Microsoft.CodeAnalysis.PublicApiAnalyzers`

---

## Architecture Patterns

### 1. CupelPolicy: Pure Data Object (sealed class, not record)

**Recommendation: Sealed class** (like `ContextBudget`), not a record.

Rationale:
- `ContextBudget` is a sealed class specifically to prevent `with`-expression bypassing constructor validation (STATE.md decision)
- Policy has the same invariants — scorer weights must be valid, slicer/placer references must be non-null
- Constructor validates everything; no invalid policy can exist at runtime
- `System.Text.Json` attributes (`[JsonPropertyName]`, `[JsonConstructor]`) for Phase 9 serialization readiness

**Properties (all read-only, set via constructor):**

| Property | Type | Description |
|---|---|---|
| `Name` | `string?` | Optional display name for diagnostics/debugging |
| `Description` | `string?` | Optional human-readable description |
| `ScorerEntries` | `IReadOnlyList<ScorerEntry>` | Scorer + weight pairs |
| `SlicerType` | `SlicerType` enum | Which slicer (`Greedy`, `Knapsack`) |
| `KnapsackBucketSize` | `int?` | Only when `SlicerType == Knapsack` |
| `PlacerType` | `PlacerType` enum | Which placer (`Chronological`, `UShaped`) |
| `DeduplicationEnabled` | `bool` | Default `true` |
| `OverflowStrategy` | `OverflowStrategy` | Default `Throw` |
| `Quotas` | `Action<QuotaBuilder>?` | Optional quota configuration |

**Key design note on scorer references:** Policies must be serialization-ready for Phase 9. Scorers are `IScorer` implementations — interfaces cannot be serialized. Use a **discriminated config approach**:

```csharp
// A serializable descriptor for scorer configuration
public sealed class ScorerEntry
{
    public ScorerType Type { get; }       // enum: Recency, Priority, Kind, Tag, Frequency, Reflexive
    public double Weight { get; }          // relative weight for CompositeScorer
    // Type-specific config (nullable, validated by type)
    public IReadOnlyDictionary<ContextKind, double>? KindWeights { get; }
    public IReadOnlyDictionary<string, double>? TagWeights { get; }
}
```

This avoids storing live `IScorer` instances in the policy (which breaks serialization) while remaining type-safe.

### 2. Slicer/Placer Type Enums

Small enums that map to concrete types. The codebase has exactly 2 slicers and 2 placers:

**SlicerType:**
- `Greedy` -> `GreedySlice` (default)
- `Knapsack` -> `KnapsackSlice`

**PlacerType:**
- `Chronological` -> `ChronologicalPlacer` (default)
- `UShaped` -> `UShapedPlacer`

**ScorerType:**
- `Recency` -> `RecencyScorer`
- `Priority` -> `PriorityScorer`
- `Kind` -> `KindScorer` (optional custom weights)
- `Tag` -> `TagScorer` (requires tag weights)
- `Frequency` -> `FrequencyScorer`
- `Reflexive` -> `ReflexiveScorer`

### 3. PipelineBuilder.WithPolicy() Integration

The builder already has all the knobs. `WithPolicy()` is a convenience method that calls existing builder methods:

```csharp
public PipelineBuilder WithPolicy(CupelPolicy policy)
{
    // Resolve scorers from ScorerEntries -> AddScorer() calls
    // Resolve slicer from SlicerType -> UseGreedySlice() / UseKnapsackSlice()
    // Resolve placer from PlacerType -> WithPlacer()
    // Apply dedup, overflow, quotas
    return this;
}
```

**Override semantics: last-write-wins.** This matches the existing builder pattern — calling `WithScorer()` twice overwrites the first. So `builder.WithPolicy(p).WithPlacer(new CustomPlacer())` overrides the policy's placer. No merge logic needed.

**Validation concern:** `WithPolicy()` uses `AddScorer()` for each entry. The builder already validates that `WithScorer()` and `AddScorer()` cannot be mixed. So `WithPolicy()` should clear any prior scorer state, or the builder should detect and handle this. Simplest: `WithPolicy()` calls `AddScorer()` for each entry — if the user previously called `WithScorer()`, `Build()` will throw with the existing "Cannot mix" error. This is acceptable behavior (user error to mix policy + manual scorer).

### 4. Named Presets Exposure

**Recommendation: Static class `CupelPresets`** (not static properties on `CupelPolicy`).

Rationale:
- Keeps `CupelPolicy` as a pure data type with no static methods/properties
- Separate class can be `[Experimental]` at the class level or per-method
- Per-preset `[Experimental]` with individual diagnostic IDs (locked decision) maps naturally to per-method attributes
- Pattern: `CupelPresets.Chat()`, `CupelPresets.Rag()`, etc.

### 5. CupelOptions and Intent-Based Lookup

**Recommendation: Core library (Phase 8), not DI package.**

`CupelOptions` is a simple dictionary wrapper — no DI dependency:

```csharp
public sealed class CupelOptions
{
    private readonly Dictionary<string, CupelPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public CupelOptions AddPolicy(string intent, CupelPolicy policy) { ... }
    public CupelPolicy GetPolicy(string intent) { ... }
    public bool TryGetPolicy(string intent, out CupelPolicy? policy) { ... }
}
```

Intent keys: **Free-form strings, case-insensitive** (like `ContextKind` and `ContextSource` patterns). No constrained enum — presets use well-known string constants but users can register custom intents.

### 6. [Experimental] Attribute Pattern

`System.Diagnostics.CodeAnalysis.ExperimentalAttribute` is built into .NET 8+ (the project targets `net10.0`). Usage:

```csharp
[Experimental("CUPEL001", UrlFormat = "https://github.com/wollax/cupel/blob/main/docs/presets.md#{0}")]
public static CupelPolicy Chat() => ...
```

Each preset gets a unique diagnostic ID:
- `CUPEL001` — Chat
- `CUPEL002` — CodeReview
- `CUPEL003` — Rag
- `CUPEL004` — DocumentQa
- `CUPEL005` — ToolUse
- `CUPEL006` — LongRunning
- `CUPEL007` — Debugging

Users suppress per-preset: `#pragma warning disable CUPEL003` to opt into the RAG preset without suppressing all.

**Diagnostic is an error by default** — consumers must explicitly opt-in via `#pragma warning disable` or project-level suppression.

---

## Don't Hand-Roll

| Problem | Use Instead |
|---|---|
| Experimental API markers | `[Experimental("CUPELXXX")]` from `System.Diagnostics.CodeAnalysis` |
| Scorer instantiation from config | Enum-based `ScorerType` + factory `switch` expression in builder |
| JSON serialization attributes | `[JsonPropertyName]`, `[JsonConstructor]` — same as `ContextBudget` |
| Policy validation | Constructor validation pattern — same as `ContextBudget` |
| Weight normalization | `CompositeScorer` already handles this — policy just stores raw weights |

---

## Common Pitfalls

### 1. Scorer config objects and TagScorer/KindScorer parameters
`TagScorer` requires `IReadOnlyDictionary<string, double>` tag weights. `KindScorer` accepts optional `IReadOnlyDictionary<ContextKind, double>` kind weights. The policy's `ScorerEntry` must carry these type-specific configs. If a `ScorerEntry` has `Type == Tag` but null `TagWeights`, construction should throw.

### 2. QuotaSet serialization complexity
`QuotaBuilder` uses `Action<QuotaBuilder>` which is not serializable. For Phase 9 readiness, the policy should store quota config as data (e.g., `IReadOnlyList<QuotaEntry>` with `Kind`, `MinPercent`, `MaxPercent`) rather than an `Action<QuotaBuilder>` delegate. The builder resolves this data into `QuotaBuilder` calls.

### 3. Policy + manual builder conflict
If a user calls both `WithPolicy()` and `WithScorer()` / `AddScorer()`, the builder's existing mutual exclusivity check will catch this at `Build()` time. Document this clearly.

### 4. PublicAPI.Unshipped.txt tracking
Every new public type and member must be added to `PublicAPI.Unshipped.txt`. The project uses `Microsoft.CodeAnalysis.PublicApiAnalyzers`. Build will fail if entries are missing.

### 5. Preset weight tuning is arbitrary
Named presets are `[Experimental]` precisely because scorer weights are educated guesses, not empirically validated. Document this in XML docs. Don't over-engineer — presets are starting points.

### 6. Budget is NOT in the policy
This is a locked decision. Verify no preset accidentally includes budget config. The builder enforces `WithBudget()` is still required after `WithPolicy()`.

---

## Code Examples

### CupelPolicy construction pattern (follows ContextBudget style)

```csharp
public sealed class CupelPolicy
{
    [JsonPropertyName("name")]
    public string? Name { get; }

    [JsonPropertyName("description")]
    public string? Description { get; }

    [JsonPropertyName("scorers")]
    public IReadOnlyList<ScorerEntry> Scorers { get; }

    [JsonPropertyName("slicerType")]
    public SlicerType SlicerType { get; }

    [JsonPropertyName("knapsackBucketSize")]
    public int? KnapsackBucketSize { get; }

    [JsonPropertyName("placerType")]
    public PlacerType PlacerType { get; }

    [JsonPropertyName("deduplicationEnabled")]
    public bool DeduplicationEnabled { get; }

    [JsonPropertyName("overflowStrategy")]
    public OverflowStrategy OverflowStrategy { get; }

    [JsonPropertyName("quotas")]
    public IReadOnlyList<QuotaEntry>? Quotas { get; }

    [JsonConstructor]
    public CupelPolicy(
        IReadOnlyList<ScorerEntry> scorers,
        SlicerType slicerType = SlicerType.Greedy,
        PlacerType placerType = PlacerType.Chronological,
        bool deduplicationEnabled = true,
        OverflowStrategy overflowStrategy = OverflowStrategy.Throw,
        int? knapsackBucketSize = null,
        IReadOnlyList<QuotaEntry>? quotas = null,
        string? name = null,
        string? description = null)
    {
        // Validate scorers non-empty, weights positive, type-specific configs present
        // Validate knapsackBucketSize only with Knapsack slicer
        // Store as defensive copies
    }
}
```

### ScorerEntry (serializable scorer descriptor)

```csharp
public sealed class ScorerEntry
{
    [JsonPropertyName("type")]
    public ScorerType Type { get; }

    [JsonPropertyName("weight")]
    public double Weight { get; }

    [JsonPropertyName("kindWeights")]
    public IReadOnlyDictionary<ContextKind, double>? KindWeights { get; }

    [JsonPropertyName("tagWeights")]
    public IReadOnlyDictionary<string, double>? TagWeights { get; }

    [JsonConstructor]
    public ScorerEntry(
        ScorerType type,
        double weight,
        IReadOnlyDictionary<ContextKind, double>? kindWeights = null,
        IReadOnlyDictionary<string, double>? tagWeights = null)
    {
        // Validate weight > 0 and finite
        // Validate kindWeights provided when Type == Kind (optional — default weights exist)
        // Validate tagWeights provided when Type == Tag (required — no default)
    }
}
```

### QuotaEntry (serializable quota descriptor)

```csharp
public sealed class QuotaEntry
{
    [JsonPropertyName("kind")]
    public ContextKind Kind { get; }

    [JsonPropertyName("minPercent")]
    public double? MinPercent { get; }

    [JsonPropertyName("maxPercent")]
    public double? MaxPercent { get; }
}
```

### PipelineBuilder.WithPolicy()

```csharp
public PipelineBuilder WithPolicy(CupelPolicy policy)
{
    ArgumentNullException.ThrowIfNull(policy);

    // Resolve scorers
    foreach (var entry in policy.Scorers)
    {
        var scorer = CreateScorer(entry);
        AddScorer(scorer, entry.Weight);
    }

    // Resolve slicer
    switch (policy.SlicerType)
    {
        case SlicerType.Greedy: UseGreedySlice(); break;
        case SlicerType.Knapsack: UseKnapsackSlice(policy.KnapsackBucketSize ?? 100); break;
    }

    // Resolve placer
    WithPlacer(policy.PlacerType switch
    {
        PlacerType.Chronological => new ChronologicalPlacer(),
        PlacerType.UShaped => new UShapedPlacer(),
        _ => throw new ArgumentOutOfRangeException()
    });

    WithDeduplication(policy.DeduplicationEnabled);
    WithOverflowStrategy(policy.OverflowStrategy);

    // Resolve quotas if present
    if (policy.Quotas is { Count: > 0 })
    {
        WithQuotas(qb =>
        {
            foreach (var q in policy.Quotas)
            {
                if (q.MinPercent.HasValue) qb.Require(q.Kind, q.MinPercent.Value);
                if (q.MaxPercent.HasValue) qb.Cap(q.Kind, q.MaxPercent.Value);
            }
        });
    }

    return this;
}

private static IScorer CreateScorer(ScorerEntry entry) => entry.Type switch
{
    ScorerType.Recency => new RecencyScorer(),
    ScorerType.Priority => new PriorityScorer(),
    ScorerType.Kind => entry.KindWeights is not null
        ? new KindScorer(entry.KindWeights)
        : new KindScorer(),
    ScorerType.Tag => new TagScorer(entry.TagWeights
        ?? throw new InvalidOperationException("TagScorer requires TagWeights.")),
    ScorerType.Frequency => new FrequencyScorer(),
    ScorerType.Reflexive => new ReflexiveScorer(),
    _ => throw new ArgumentOutOfRangeException()
};
```

### Named preset example (CupelPresets.Chat)

```csharp
public static class CupelPresets
{
    [Experimental("CUPEL001")]
    public static CupelPolicy Chat() => new(
        scorers:
        [
            new ScorerEntry(ScorerType.Recency, weight: 3.0),
            new ScorerEntry(ScorerType.Kind, weight: 1.0),
        ],
        slicerType: SlicerType.Greedy,
        placerType: PlacerType.Chronological,
        name: "Chat",
        description: "Optimized for conversational LLM interactions. Prioritizes recent messages."
    );
}
```

### CupelOptions intent-based lookup

```csharp
public sealed class CupelOptions
{
    private readonly Dictionary<string, CupelPolicy> _policies = new(StringComparer.OrdinalIgnoreCase);

    public CupelOptions AddPolicy(string intent, CupelPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);
        ArgumentNullException.ThrowIfNull(policy);
        _policies[intent] = policy;
        return this;
    }

    public CupelPolicy GetPolicy(string intent)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);
        if (!_policies.TryGetValue(intent, out var policy))
            throw new KeyNotFoundException($"No policy registered for intent '{intent}'.");
        return policy;
    }

    public bool TryGetPolicy(string intent, [NotNullWhen(true)] out CupelPolicy? policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(intent);
        return _policies.TryGetValue(intent, out policy);
    }
}
```

---

## Existing Type Surface (Reference for Planner)

### Scorers (all implement `IScorer`)
| Type | Config | Parameterless? |
|---|---|---|
| `RecencyScorer` | None | Yes |
| `PriorityScorer` | None | Yes |
| `KindScorer` | Optional `IReadOnlyDictionary<ContextKind, double>` | Yes (has defaults) |
| `TagScorer` | Required `IReadOnlyDictionary<string, double>` | No |
| `FrequencyScorer` | None | Yes |
| `ReflexiveScorer` | None | Yes |
| `CompositeScorer` | `IReadOnlyList<(IScorer, double)>` | No (built by builder) |
| `ScaledScorer` | `IScorer inner` | No (decorator) |

### Slicers (implement `ISlicer`)
| Type | Config |
|---|---|
| `GreedySlice` | None |
| `KnapsackSlice` | `int bucketSize = 100` |
| `QuotaSlice` | `ISlicer inner`, `QuotaSet quotas` (decorator) |

### Placers (implement `IPlacer`)
| Type | Config |
|---|---|
| `ChronologicalPlacer` | None |
| `UShapedPlacer` | None |

### Builder Defaults
- Slicer: `GreedySlice` (if none specified)
- Placer: `ChronologicalPlacer` (if none specified)
- Deduplication: `true`
- Overflow: `OverflowStrategy.Throw`
- Budget: **required** (no default)
- Scorer: **required** (no default)

---

## Preset Rationale (7 presets)

| Preset | Primary Scorers | Slicer | Placer | Notes |
|---|---|---|---|---|
| **Chat** | Recency(3), Kind(1) | Greedy | Chronological | Conversational — recent messages matter most |
| **CodeReview** | Kind(2), Priority(2), Recency(1) | Greedy | Chronological | Code diffs + tool output weighted equally with priority |
| **Rag** | Reflexive(3), Kind(1) | Greedy | UShaped | FutureRelevanceHint carries retrieval scores; U-shaped for attention |
| **DocumentQa** | Kind(2), Reflexive(2), Priority(1) | Knapsack | UShaped | Documents are large/variable — knapsack optimizes packing |
| **ToolUse** | Kind(2), Recency(2), Priority(1) | Greedy | Chronological | Tool outputs need recency + kind weighting |
| **LongRunning** | Recency(3), Frequency(1), Kind(1) | Greedy | Chronological | Long sessions — aggressive recency, frequency breaks ties |
| **Debugging** | Priority(3), Kind(2), Recency(1) | Greedy | Chronological | Error context — priority-first for stack traces and errors |

All presets use `DeduplicationEnabled = true` and `OverflowStrategy = Throw` (safe defaults).

---

## File Placement

| New File | Namespace | Description |
|---|---|---|
| `src/.../CupelPolicy.cs` | `Wollax.Cupel` | Policy data object |
| `src/.../ScorerEntry.cs` | `Wollax.Cupel` | Scorer config descriptor |
| `src/.../ScorerType.cs` | `Wollax.Cupel` | Scorer type enum |
| `src/.../SlicerType.cs` | `Wollax.Cupel` | Slicer type enum |
| `src/.../PlacerType.cs` | `Wollax.Cupel` | Placer type enum |
| `src/.../QuotaEntry.cs` | `Wollax.Cupel.Slicing` | Quota config descriptor |
| `src/.../CupelPresets.cs` | `Wollax.Cupel` | Named preset factory methods |
| `src/.../CupelOptions.cs` | `Wollax.Cupel` | Intent-based policy registry |
| Modified: `PipelineBuilder.cs` | — | Add `WithPolicy()` method |

---

*Phase: 08-policy-system-named-presets*
*Research completed: 2026-03-13*
