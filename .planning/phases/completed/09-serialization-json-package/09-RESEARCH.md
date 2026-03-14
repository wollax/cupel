# Phase 9: Serialization & JSON Package - Research

**Researched:** 2026-03-14
**Domain:** System.Text.Json source generation, polymorphic serialization, companion NuGet package design
**Confidence:** HIGH

## Summary

This phase introduces `Wollax.Cupel.Json`, a separate NuGet package that enables `CupelPolicy` (and its graph of types) to round-trip through JSON. The package will use STJ source generation via `JsonSerializerContext` with polymorphic `$type` discriminators.

The existing `CupelPolicy` already uses enum-based type discrimination (`ScorerType`, `SlicerType`, `PlacerType`) and has `[JsonPropertyName]` and `[JsonConstructor]` attributes on all public types. The JSON package's job is to provide a source-generated `JsonSerializerContext` that can serialize/deserialize the full policy graph, plus a registration mechanism for consumer-defined custom scorers.

**Primary recommendation:** Use STJ's `JsonSerializerContext` with `[JsonSerializable]` attributes for source generation. The existing enum-based CupelPolicy model maps naturally to JSON without needing `[JsonDerivedType]` polymorphic attributes — the enums already serve as type discriminators. The custom scorer registration hook (`RegisterScorer`) should use a `TypeInfoResolverChain`-based approach, providing a modifier that dynamically adds custom scorer entries at deserialization time.

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
| --- | --- | --- | --- |
| System.Text.Json | in-box (.NET 10) | JSON serialization/deserialization | First-party, AOT-compatible, source-gen support |

### Supporting
| Library | Version | Purpose | When to Use |
| --- | --- | --- | --- |
| Microsoft.CodeAnalysis.PublicApiAnalyzers | 3.3.4 | Public API tracking | Already in use — new package needs its own `PublicAPI.*.txt` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
| --- | --- | --- |
| STJ source gen | Reflection-based STJ | Slower startup, no AOT support, but simpler setup |
| STJ | Newtonsoft.Json | Richer polymorphism but extra dependency, conflicts with project goal of minimal deps |

**Installation:**
No additional packages needed. `System.Text.Json` is in-box for .NET 10. The new project just needs a `<ProjectReference>` to `Wollax.Cupel`.

## Architecture Patterns

### Recommended Project Structure
```
src/
└── Wollax.Cupel.Json/
    ├── Wollax.Cupel.Json.csproj
    ├── CupelJsonContext.cs          # Source-generated JsonSerializerContext
    ├── CupelJsonSerializer.cs       # Public API: Serialize/Deserialize methods
    ├── CupelJsonOptions.cs          # Options for custom scorer registration
    ├── CupelJsonException.cs        # Path-aware deserialization exception (optional)
    ├── Validation/
    │   └── PolicyJsonValidator.cs   # Post-deserialization validation with JSON-path errors
    ├── PublicAPI.Shipped.txt
    └── PublicAPI.Unshipped.txt
tests/
└── Wollax.Cupel.Json.Tests/
    ├── Wollax.Cupel.Json.Tests.csproj
    ├── RoundTripTests.cs
    ├── ValidationTests.cs
    ├── CustomScorerTests.cs
    └── ErrorMessageTests.cs
```

### Pattern 1: Source-Generated JsonSerializerContext
**What:** A partial class deriving from `JsonSerializerContext` with `[JsonSerializable]` for each root type.
**When to use:** Always — this is the required entry point for source-gen serialization.
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(CupelPolicy))]
[JsonSerializable(typeof(ContextBudget))]
public partial class CupelJsonContext : JsonSerializerContext
{
}
```

**Critical limitation:** Polymorphism is supported in **metadata-based** source generation but **not fast-path** source generation. Since `CupelPolicy` doesn't use interface-based polymorphism (it uses enums), this limitation does not apply to the current type graph. The context will work with both modes.

### Pattern 2: Enum Serialization as camelCase Strings
**What:** Enums (`ScorerType`, `SlicerType`, `PlacerType`, `OverflowStrategy`) serialize as lowercase/camelCase strings, not integers.
**When to use:** For all enum properties in the JSON schema.
**Example:**
```csharp
// Use JsonStringEnumConverter<T> (generic version is AOT-safe)
[JsonConverter(typeof(JsonStringEnumConverter<ScorerType>))]
public enum ScorerType { ... }

// OR use blanket policy on the context:
[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
```

**Important:** The generic `JsonStringEnumConverter<TEnum>` is required for AOT/source-gen compatibility. The non-generic `JsonStringEnumConverter` does NOT work with Native AOT.

### Pattern 3: Facade API with Validation
**What:** A static `CupelJsonSerializer` class that wraps `JsonSerializer` calls with pre/post validation.
**When to use:** Always — consumers should not use `CupelJsonContext` directly for deserialization if validation is needed.
**Example:**
```csharp
public static class CupelJsonSerializer
{
    public static string Serialize(CupelPolicy policy, CupelJsonOptions? options = null)
    {
        var context = CreateContext(options);
        return JsonSerializer.Serialize(policy, context.CupelPolicy);
    }

    public static CupelPolicy Deserialize(string json, CupelJsonOptions? options = null)
    {
        var context = CreateContext(options);
        var policy = JsonSerializer.Deserialize(json, context.CupelPolicy)
            ?? throw new CupelJsonException("$", "Policy cannot be null.");
        // CupelPolicy constructor validates at construction time
        return policy;
    }
}
```

### Pattern 4: Custom Scorer Registration via Options
**What:** An options object that collects custom scorer registrations before constructing the context.
**When to use:** When consumers define custom `IScorer` implementations and need them to round-trip through JSON.
**Example:**
```csharp
public sealed class CupelJsonOptions
{
    private readonly Dictionary<string, Func<JsonElement?, IScorer>> _scorerFactories = new();

    public CupelJsonOptions RegisterScorer(string typeName, Func<IScorer> factory)
    {
        _scorerFactories[typeName] = _ => factory();
        return this;
    }

    // Config-aware overload for scorers that need deserialized configuration
    public CupelJsonOptions RegisterScorer(string typeName, Func<JsonElement?, IScorer> factory)
    {
        _scorerFactories[typeName] = factory;
        return this;
    }
}
```

### Pattern 5: TypeInfoResolverChain for Extensibility
**What:** Combining the source-generated context with a custom `IJsonTypeInfoResolver` that handles custom scorer types.
**When to use:** When custom scorers are registered.
**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
var options = new JsonSerializerOptions
{
    TypeInfoResolver = JsonTypeInfoResolver.Combine(
        CupelJsonContext.Default,
        customScorerResolver)
};

// Or using the chain:
options.TypeInfoResolverChain.Add(customScorerResolver);
```

### Anti-Patterns to Avoid
- **Don't use `[JsonDerivedType]` for scorers/slicers/placers:** The existing model uses enums, not an interface hierarchy. Adding `[JsonDerivedType]` to `IScorer` would require modifying the core package and wouldn't work because scorers are not serialized as polymorphic objects — they're described by `ScorerEntry` with a `ScorerType` enum.
- **Don't use `DefaultJsonTypeInfoResolver` with reflection:** For AOT compatibility, always use the source-generated context as the base resolver.
- **Don't use non-generic `JsonStringEnumConverter`:** It's not AOT-safe. Always use `JsonStringEnumConverter<TEnum>`.
- **Don't modify types in `Wollax.Cupel` to accommodate serialization:** The JSON package is a companion; changes should be limited to adding `[JsonConverter]` attributes on enums if not already present (they aren't — the enums serialize as integers by default currently).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
| --- | --- | --- | --- |
| JSON serialization | Custom Utf8JsonReader/Writer logic | `JsonSerializer` + source-generated context | Source gen handles all the complexity |
| Enum-to-string mapping | Manual switch/dictionary mapping | `JsonStringEnumConverter<TEnum>` | Handles naming policies, case-insensitive read |
| Combining resolvers | Manual resolver composition | `JsonTypeInfoResolver.Combine()` | First-party API, handles precedence correctly |
| camelCase naming | Manual `[JsonPropertyName]` on every new property | `PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase` on context | Applied globally by source gen |
| JSON path tracking | Manual path accumulation | `JsonException.Path` property | Built into STJ |

**Key insight:** The existing types already have `[JsonPropertyName]` and `[JsonConstructor]` attributes. The source-generated context should honor these. The `PropertyNamingPolicy = CamelCase` on the context will apply to any properties without explicit `[JsonPropertyName]` attributes, but the existing attributes take precedence.

## Common Pitfalls

### Pitfall 1: Source Gen + Custom Converters Interaction
**What goes wrong:** Custom `JsonConverter` attributes on types (like `ContextKindJsonConverter` on `ContextKind`, `ContextKindDictionaryConverter` on `ContextBudget.ReservedSlots`) may not be picked up by the source generator if the types aren't explicitly included.
**Why it happens:** Source generation only generates metadata for types listed in `[JsonSerializable]` and their reachable members. Custom converters referenced via `[JsonConverter]` on the types themselves ARE respected by source gen.
**How to avoid:** The existing `[JsonConverter(typeof(ContextKindJsonConverter))]` on `ContextKind` and `[JsonConverter(typeof(ContextKindDictionaryConverter))]` on `ReservedSlots` will be honored automatically. Verify with round-trip tests.
**Warning signs:** `ContextKind` serializes as `{"Value":"Message"}` instead of `"Message"`.

### Pitfall 2: Existing [JsonPropertyName] vs Context-Level PropertyNamingPolicy
**What goes wrong:** If the source-gen context uses `PropertyNamingPolicy = CamelCase`, properties that already have `[JsonPropertyName]` are unaffected (attribute wins). But NEW properties added without `[JsonPropertyName]` will get camelCase naming automatically.
**Why it happens:** `[JsonPropertyName]` takes precedence over the naming policy.
**How to avoid:** This is actually the desired behavior. Existing properties keep their explicit names. The context-level policy is a safety net for any missed properties.
**Warning signs:** None — this is correct behavior.

### Pitfall 3: CupelPolicy Constructor Validation vs JSON Deserialization
**What goes wrong:** `CupelPolicy`'s `[JsonConstructor]` performs validation (non-empty scorers, knapsack constraints, etc.). If the JSON has invalid data, `ArgumentException`/`ArgumentOutOfRangeException` will be thrown during deserialization, wrapped in a `JsonException`.
**Why it happens:** STJ wraps constructor exceptions in `JsonException` during deserialization.
**How to avoid:** This is actually desirable — the CONTEXT.md says "validation at deserialization time." However, the error messages won't include JSON path context automatically. The facade API should catch these and re-throw with path information.
**Warning signs:** Error messages like `"Scorers must contain at least one entry"` without JSON path context.

### Pitfall 4: IReadOnlyList/IReadOnlyDictionary Deserialization
**What goes wrong:** STJ can deserialize into `IReadOnlyList<T>` and `IReadOnlyDictionary<K,V>` properties if there's a constructor parameter accepting them (which CupelPolicy has via `[JsonConstructor]`).
**Why it happens:** STJ maps JSON arrays/objects to the constructor parameters by matching `[JsonPropertyName]` to the constructor parameter names.
**How to avoid:** Ensure the `[JsonConstructor]` parameter names match the `[JsonPropertyName]` values (they do — existing code already handles this correctly with camelCase matching).
**Warning signs:** `null` collections after deserialization when the JSON has non-null arrays.

### Pitfall 5: Enum Values Not Serializing as Strings
**What goes wrong:** By default, STJ serializes enums as integers. If enum string conversion isn't configured, `"slicerType": 0` instead of `"slicerType": "greedy"`.
**Why it happens:** Default STJ behavior is integer enum serialization.
**How to avoid:** Either use `[JsonConverter(typeof(JsonStringEnumConverter<SlicerType>))]` on each enum type, or set `UseStringEnumConverter = true` on the `JsonSourceGenerationOptions`. The blanket approach is simpler but less granular. Since the CONTEXT.md specifies camelCase discriminator values (`"greedy"`, `"knapsack"`), need to verify that `JsonStringEnumConverter<T>` with `CamelCase` naming policy produces the correct values. STJ's `JsonStringEnumConverter<T>` uses the naming policy set on the options/context for enum value names.
**Warning signs:** Enum members serialize as `"Greedy"` (PascalCase) instead of `"greedy"` (camelCase).

### Pitfall 6: Separate Package Needs Its Own PublicAPI Tracking
**What goes wrong:** The new `Wollax.Cupel.Json` project needs its own `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` files and a reference to `Microsoft.CodeAnalysis.PublicApiAnalyzers`.
**Why it happens:** Public API analyzers are per-project.
**How to avoid:** Copy the pattern from `Wollax.Cupel.csproj` — include the analyzer reference and both API tracking files.
**Warning signs:** Build warnings or missing API surface tracking.

### Pitfall 7: Unknown $type Error Message Design
**What goes wrong:** The CONTEXT.md requires that unknown `$type` values fail immediately with a descriptive exception listing registered types and suggesting `RegisterScorer()`.
**Why it happens:** This is a custom requirement beyond what STJ provides natively.
**How to avoid:** Since `CupelPolicy` uses `ScorerType` enum (not polymorphic `$type`), the "unknown $type" scenario only applies to the JSON facade layer. The enum deserialization will throw a `JsonException` for unknown enum values. The facade should catch this and produce the friendly error with registered types.
**Warning signs:** Generic `JsonException` without guidance on how to register custom types.

## Code Examples

### Source-Generated Context
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CupelPolicy))]
[JsonSerializable(typeof(ContextBudget))]
[JsonSerializable(typeof(ScorerEntry))]
[JsonSerializable(typeof(QuotaEntry))]
public partial class CupelJsonContext : JsonSerializerContext
{
}
```

### Round-Trip Serialization
```csharp
// Serialize
var json = JsonSerializer.Serialize(policy, CupelJsonContext.Default.CupelPolicy);

// Deserialize
var policy = JsonSerializer.Deserialize(json, CupelJsonContext.Default.CupelPolicy);
```

### Custom Scorer Registration Pattern
```csharp
var options = new CupelJsonOptions()
    .RegisterScorer("myCustom", () => new MyCustomScorer());

// Deserialization with custom scorers:
var policy = CupelJsonSerializer.Deserialize(json, options);
```

### Expected JSON Shape (from CONTEXT.md decisions)
```json
{
  "name": "My Policy",
  "description": "A custom policy",
  "scorers": [
    { "type": "recency", "weight": 3.0 },
    { "type": "kind", "weight": 1.0, "kindWeights": { "SystemPrompt": 1.0, "Memory": 0.8 } },
    { "type": "tag", "weight": 2.0, "tagWeights": { "important": 1.0, "optional": 0.3 } }
  ],
  "slicerType": "greedy",
  "placerType": "chronological",
  "deduplicationEnabled": true,
  "overflowStrategy": "throw",
  "knapsackBucketSize": null,
  "quotas": [
    { "kind": "SystemPrompt", "minPercent": 10.0, "maxPercent": 30.0 }
  ]
}
```

### Path-Aware Error Messages
```csharp
// Desired error output format:
// "$.scorers[1].weight: must be > 0, got -0.5"
// "$.slicerType: unknown value 'custom'. Known values: greedy, knapsack, stream"
```

## Architecture Decision: Enum-Based vs Polymorphic $type

The CONTEXT.md specifies `$type` discriminators for scorers, slicers, and placers. However, the existing `CupelPolicy` model uses **enums** (`ScorerType`, `SlicerType`, `PlacerType`), not polymorphic interfaces. This creates a design tension:

**Option A: Keep enum-based model, map enum values to camelCase strings**
- The `ScorerType.Recency` enum member serializes as `"recency"` via `JsonStringEnumConverter<ScorerType>` with camelCase naming policy
- The JSON property is `"type"` (from existing `[JsonPropertyName("type")]` on `ScorerEntry.Type`)
- Pros: Zero changes to core library, natural fit
- Cons: The discriminator property is `"type"` not `"$type"`, doesn't use STJ's native `[JsonDerivedType]`

**Option B: Introduce polymorphic DTOs with `$type` discriminator**
- Create a parallel DTO hierarchy (e.g., `ScorerConfigDto`, `SlicerConfigDto`, `PlacerConfigDto`) with `[JsonPolymorphic]` and `[JsonDerivedType]`
- Map between DTOs and domain types
- Pros: Uses STJ's native polymorphism, `$type` discriminator as specified
- Cons: Significant complexity, DTO mapping layer, duplicated types

**Recommendation: Option A** — The existing enum model is clean and works well. The CONTEXT.md mentions `$type` discriminators in the context of slicer/placer being "single polymorphic objects" (`{ "$type": "greedy", ... }`). However, the current `CupelPolicy` uses `SlicerType`/`PlacerType` enums that map to simple string values like `"greedy"`. This is effectively the same user experience — the JSON just uses `"slicerType": "greedy"` instead of `"slicer": { "$type": "greedy" }`.

**If the `$type` discriminator is strictly required**, Option B adds a mapper layer. The planner should clarify this with the user or check if the CONTEXT.md decisions were made before the enum-based model was finalized. The enum model was established in Phase 8 and the CONTEXT.md for Phase 9 references discriminator values that align perfectly with enum members.

**Resolution path:** The CONTEXT.md says slicers/placers are "single polymorphic objects" with `$type` form. This means the JSON shape should be:
```json
{
  "slicer": { "$type": "greedy" },
  "placer": { "$type": "chronological" }
}
```
rather than:
```json
{
  "slicerType": "greedy",
  "placerType": "chronological"
}
```

This means the JSON package needs a **mapping layer** — it serializes/deserializes a JSON DTO model and converts to/from the domain `CupelPolicy`. The DTO model would use `[JsonPolymorphic]`/`[JsonDerivedType]` for the slicer/placer/scorer configs. This is Option B and aligns with the CONTEXT.md decisions.

**Final recommendation:** Implement Option B for scorers (where custom types exist) and use a simpler wrapper for slicers/placers (where the set is fixed). The DTO approach enables the `$type` form and the custom scorer registration hook naturally.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
| --- | --- | --- | --- |
| Non-generic `JsonStringEnumConverter` | `JsonStringEnumConverter<TEnum>` | .NET 8 | Required for AOT/source-gen |
| Reflection-based polymorphism | `[JsonDerivedType]` + `[JsonPolymorphic]` | .NET 7 | Native polymorphic support |
| Manual resolver composition | `JsonTypeInfoResolver.Combine()` | .NET 8 | Clean multi-context support |
| `TypeInfoResolver` property only | `TypeInfoResolverChain` property | .NET 8 | Mutable resolver chain |
| `$type` must be first property | `AllowOutOfOrderMetadataProperties` | .NET 9 | Relaxed ordering requirement |
| No custom enum member names | `[JsonStringEnumMemberName]` attribute | .NET 9 | Custom JSON names for enum values |

**Key .NET 9+ features relevant to this phase:**
- `[JsonStringEnumMemberName("customName")]` can customize individual enum member JSON names, enabling `ScorerType.Recency` to serialize as `"recency"` without relying on naming policy.
- `AllowOutOfOrderMetadataProperties` allows `$type` to appear anywhere in the object (useful for forward-compatibility).

**Key .NET 10 considerations:**
- Source generation continues to improve but the API surface is stable since .NET 8.
- No breaking changes to polymorphism APIs.

## Open Questions

1. **Enum model vs $type DTO model**
   - What we know: CONTEXT.md specifies `$type` discriminators, but existing domain model uses enums.
   - What's unclear: Whether the CONTEXT.md decisions were made with awareness of the enum-based model, or whether the intent is to have a different JSON shape than the direct enum serialization.
   - Recommendation: Implement the DTO mapper approach per CONTEXT.md — the JSON shape should use `$type` for scorers/slicers/placers as polymorphic objects. The existing `[JsonPropertyName]` attributes on `CupelPolicy` describe the *domain model's* serialization, not the *public JSON contract*.

2. **Enum member naming: CamelCase via policy vs explicit [JsonStringEnumMemberName]**
   - What we know: Both approaches work. `CamelCase` naming policy produces `"recency"` from `Recency`. `[JsonStringEnumMemberName]` is more explicit.
   - What's unclear: Whether `CamelCase` policy handles `UShaped` → `"uShaped"` correctly (it should per camelCase rules).
   - Recommendation: Use `PropertyNamingPolicy = CamelCase` on the context, validate with tests. Fall back to `[JsonStringEnumMemberName]` on individual members if camelCase policy produces unexpected results.

3. **Custom scorer factory signature**
   - What we know: CONTEXT.md leaves factory signature as Claude's discretion.
   - What's unclear: Whether custom scorers need configuration from the JSON (a `JsonElement` parameter) or are parameterless.
   - Recommendation: Provide both overloads — `Func<IScorer>` for simple scorers, `Func<JsonElement?, IScorer>` for scorers that need config. The config-aware signature enables custom scorers to read additional properties from their JSON object.

4. **Whether ContextBudget should have its own top-level round-trip API**
   - What we know: The success criteria mention ContextBudget round-tripping.
   - What's unclear: Whether ContextBudget is always nested inside a policy or also serialized standalone.
   - Recommendation: Include `[JsonSerializable(typeof(ContextBudget))]` on the context so it can be used standalone, but the primary API focuses on CupelPolicy.

## Sources

### Primary (HIGH confidence)
- Microsoft Learn: [Polymorphic serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism) — `[JsonDerivedType]`, `[JsonPolymorphic]`, type discriminators, contract model polymorphism
- Microsoft Learn: [Source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation) — `JsonSerializerContext`, `[JsonSerializable]`, `JsonSourceGenerationOptions`, enum handling
- Microsoft Learn: [Custom contracts](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/custom-contracts) — `DefaultJsonTypeInfoResolver`, modifiers, `IJsonTypeInfoResolver`
- Codebase analysis of `CupelPolicy.cs`, `ScorerEntry.cs`, `ContextBudget.cs`, `ContextKind.cs` — existing `[JsonPropertyName]`, `[JsonConstructor]`, `[JsonConverter]` attributes

### Secondary (MEDIUM confidence)
- .NET runtime repo Context7 docs — source gen patterns, PACKAGE.md usage examples

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — in-box System.Text.Json, no third-party dependencies
- Architecture: HIGH — well-documented STJ patterns, verified against official docs
- Pitfalls: HIGH — cross-referenced with official docs and codebase analysis
- Custom scorer registration: MEDIUM — design is sound but implementation will need validation against STJ source-gen constraints

**Research date:** 2026-03-14
**Valid until:** 2026-04-14 (stable .NET 10 API surface)
