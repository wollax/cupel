# Phase 1: Project Scaffold & Core Models - Research

**Researched:** 2026-03-10
**Domain:** .NET 10 project scaffolding, immutable record models, smart enum pattern, JSON annotation, public API tracking
**Confidence:** HIGH

## Summary

Phase 1 establishes the solution structure (3 projects), build infrastructure (`Directory.Build.props`, `Directory.Packages.props`, `global.json`), and the two load-bearing models (`ContextItem`, `ContextBudget`) plus two smart enum types (`ContextKind`, `ContextSource`). Every subsequent phase depends on these types compiling cleanly with `TreatWarningsAsErrors`, `Nullable enable`, and `PublicApiAnalyzers` enforced.

The standard approach is a `Directory.Build.props` at the solution root centralizing build settings, Central Package Management via `Directory.Packages.props`, and a `global.json` pinning the .NET 10 SDK. `ContextItem` is a sealed record with `{ get; init; }` properties and `[JsonPropertyName]` on every public property. Smart enums are hand-rolled sealed classes implementing `IEquatable<T>` with a custom `JsonConverter<T>` for plain-string serialization.

**Primary recommendation:** Scaffold the solution with centralized build config first, then implement `ContextKind`/`ContextSource` smart enums, then `ContextItem` and `ContextBudget` models, then wire up tests and benchmarks. Use TUnit 1.19.x as the test framework per the CONTEXT.md decision (overriding the earlier STACK.md xUnit recommendation).

## Standard Stack

### Core (Phase 1 scope)

| Library | Version | Purpose | Why Standard |
| --- | --- | --- | --- |
| .NET 10 SDK | 10.0.x | Runtime and build toolchain | Current LTS, C# 14, single TFM |
| Microsoft.CodeAnalysis.PublicApiAnalyzers | 3.3.4 (stable) | Track public API surface, prevent accidental breaking changes | Used by dotnet/runtime, ASP.NET Core, Polly |
| Microsoft.SourceLink.GitHub | 10.0.102 | Source-link for step-through debugging | Zero runtime cost, standard for NuGet packages |
| MinVer | 7.0.0 | Git-tag-based semantic versioning | Eliminates manual version management |

### Testing (Phase 1 scope)

| Library | Version | Purpose | Why Standard |
| --- | --- | --- | --- |
| TUnit | 1.19.22 | Test framework | User decision (CONTEXT.md). Source-generated, parallel-by-default, .NET 10 native support, no `Microsoft.NET.Test.Sdk` needed |
| BenchmarkDotNet | 0.15.8 | Performance benchmarking | De facto .NET benchmark standard, supports .NET 10 via `net10.0` TFM |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
| --- | --- | --- |
| TUnit | xUnit v3 | xUnit is more established ecosystem; TUnit is faster (source-gen) and was explicitly chosen in CONTEXT.md |
| MinVer | Manual `<Version>` | Manual is simpler but error-prone for multi-package repos |
| PublicApiAnalyzers 3.3.4 | 5.0.0-preview | Preview has newer features but is prerelease; 3.3.4 is battle-tested |

### Installation

```bash
# Solution and projects created via dotnet CLI
dotnet new sln -n Cupel
dotnet new classlib -n Wollax.Cupel -o src/Wollax.Cupel
dotnet new console -n Wollax.Cupel.Tests -o tests/Wollax.Cupel.Tests
dotnet new console -n Wollax.Cupel.Benchmarks -o benchmarks/Wollax.Cupel.Benchmarks
```

Note: TUnit test projects use `<OutputType>Exe</OutputType>` (not the test SDK). BenchmarkDotNet projects also use `<OutputType>Exe</OutputType>`.

## Architecture Patterns

### Recommended Project Structure

```
cupel/
  global.json                              # Pin .NET 10 SDK
  Directory.Build.props                    # Shared build settings
  Directory.Packages.props                 # Central Package Management
  Cupel.sln
  src/
    Wollax.Cupel/
      Wollax.Cupel.csproj
      PublicAPI.Shipped.txt                # PublicApiAnalyzers (empty initially)
      PublicAPI.Unshipped.txt              # PublicApiAnalyzers
      ContextItem.cs
      ContextBudget.cs
      ContextKind.cs                       # Smart enum
      ContextSource.cs                     # Smart enum
  tests/
    Wollax.Cupel.Tests/
      Wollax.Cupel.Tests.csproj
      Models/
        ContextItemTests.cs
        ContextBudgetTests.cs
        ContextKindTests.cs
        ContextSourceTests.cs
  benchmarks/
    Wollax.Cupel.Benchmarks/
      Wollax.Cupel.Benchmarks.csproj
      Program.cs                           # BenchmarkDotNet entry point
```

### Pattern 1: Sealed Record with Init Properties

**What:** `ContextItem` as a `sealed record` with `{ get; init; }` properties, `[JsonPropertyName]` on each, and constructor validation.
**When to use:** Immutable data transfer types that must serialize to JSON.

```csharp
// ContextItem is NOT a positional record (no primary constructor) to allow
// [JsonPropertyName] on each property and flexible initialization.
[JsonConverter(typeof(ContextItemJsonConverter))] // only if needed, otherwise STJ handles records natively
public sealed record ContextItem
{
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    [JsonPropertyName("kind")]
    public ContextKind Kind { get; init; } = ContextKind.Message;

    [JsonPropertyName("source")]
    public ContextSource Source { get; init; } = ContextSource.Chat;

    [JsonPropertyName("priority")]
    public int? Priority { get; init; }

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();

    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    [JsonPropertyName("futureRelevanceHint")]
    public double? FutureRelevanceHint { get; init; }

    [JsonPropertyName("pinned")]
    public bool Pinned { get; init; }

    [JsonPropertyName("originalTokens")]
    public int? OriginalTokens { get; init; }
}
```

**Key design decisions:**
- `required` keyword on `Content` and `Tokens` enforces non-null/present at construction.
- `sealed record` gives value equality, `with` expression support, `ToString()`, and prevents inheritance.
- No positional constructor -- properties with `[JsonPropertyName]` are cleaner and avoid the `[JsonPropertyName]` on primary constructor parameter issue (dotnet/runtime#104019).
- Default values on optional properties (`Tags = []`, `Pinned = false`, etc.) enable clean `new ContextItem { Content = "...", Tokens = 42 }` construction.

### Pattern 2: Smart Enum (Hand-Rolled)

**What:** A sealed class wrapping a string value with static well-known instances, `IEquatable<T>`, and a custom `JsonConverter` for plain-string serialization.
**When to use:** When you need enum-like behavior with extensibility (user-defined values) and case-insensitive comparison.

```csharp
[JsonConverter(typeof(ContextKindJsonConverter))]
public sealed class ContextKind : IEquatable<ContextKind>
{
    public static readonly ContextKind Message = new("Message");
    public static readonly ContextKind Document = new("Document");
    public static readonly ContextKind ToolOutput = new("ToolOutput");
    public static readonly ContextKind Memory = new("Memory");
    public static readonly ContextKind SystemPrompt = new("SystemPrompt");

    public string Value { get; }

    public ContextKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public bool Equals(ContextKind? other)
        => other is not null
        && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ContextKind);

    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(ContextKind? left, ContextKind? right)
        => Equals(left, right);

    public static bool operator !=(ContextKind? left, ContextKind? right)
        => !Equals(left, right);
}

// JsonConverter for plain-string serialization: "Message" not {"Value":"Message"}
internal sealed class ContextKindJsonConverter : JsonConverter<ContextKind>
{
    public override ContextKind Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("ContextKind value cannot be null.");
        return new ContextKind(value);
    }

    public override void Write(
        Utf8JsonWriter writer, ContextKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
```

**Why not Ardalis.SmartEnum:** External dependency. The core package must have zero dependencies. The pattern is ~50 lines per type -- not worth a dependency.

**Why not C# enum + `[JsonStringEnumConverter]`:** C# enums are not extensible. Users cannot define `new ContextKind("custom-kind")` with a native enum.

### Pattern 3: Validated Model (ContextBudget)

**What:** A type with constructor validation that throws on invalid inputs.
**When to use:** Configuration/constraint types where invalid state is a programming error.

```csharp
public sealed class ContextBudget
{
    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; }

    [JsonPropertyName("targetTokens")]
    public int TargetTokens { get; }

    [JsonPropertyName("outputReserve")]
    public int OutputReserve { get; }

    [JsonPropertyName("reservedSlots")]
    public IReadOnlyDictionary<ContextKind, int> ReservedSlots { get; }

    [JsonPropertyName("estimationSafetyMarginPercent")]
    public double EstimationSafetyMarginPercent { get; }

    public ContextBudget(
        int maxTokens,
        int targetTokens,
        int outputReserve = 0,
        IReadOnlyDictionary<ContextKind, int>? reservedSlots = null,
        double estimationSafetyMarginPercent = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(targetTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputReserve);
        ArgumentOutOfRangeException.ThrowIfNegative(estimationSafetyMarginPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(estimationSafetyMarginPercent, 100);

        if (targetTokens > maxTokens)
            throw new ArgumentException(
                $"TargetTokens ({targetTokens}) cannot exceed MaxTokens ({maxTokens}).",
                nameof(targetTokens));

        MaxTokens = maxTokens;
        TargetTokens = targetTokens;
        OutputReserve = outputReserve;
        ReservedSlots = reservedSlots ?? new Dictionary<ContextKind, int>();
        EstimationSafetyMarginPercent = estimationSafetyMarginPercent;
    }
}
```

**Design note:** `ContextBudget` is a class (not a record) because it has constructor validation logic. Records with primary constructors make validation awkward. A sealed class with `[JsonConstructor]` for deserialization works cleanly.

### Pattern 4: Directory.Build.props Configuration

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

    <!-- NuGet metadata -->
    <Authors>Wollax</Authors>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <PackageProjectUrl>https://github.com/wollax/cupel</PackageProjectUrl>
    <RepositoryUrl>https://github.com/wollax/cupel</RepositoryUrl>
    <RepositoryType>git</RepositoryType>
    <PackageTags>context;llm;agent;pipeline;token;scoring</PackageTags>

    <!-- Deterministic builds -->
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>

  <ItemGroup>
    <!-- SourceLink -->
    <PackageReference Include="Microsoft.SourceLink.GitHub" PrivateAssets="All" />
  </ItemGroup>
</Project>
```

### Pattern 5: Central Package Management

```xml
<!-- Directory.Packages.props -->
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Build tooling (all projects) -->
    <GlobalPackageReference Include="MinVer" Version="7.0.0" PrivateAssets="All" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="10.0.102" />

    <!-- API surface tracking (src projects only) -->
    <PackageVersion Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4" />

    <!-- Testing -->
    <PackageVersion Include="TUnit" Version="1.19.22" />

    <!-- Benchmarking -->
    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />
  </ItemGroup>
</Project>
```

### Pattern 6: global.json

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

### Anti-Patterns to Avoid

- **Positional record constructors for `ContextItem`:** `[JsonPropertyName]` on primary constructor parameters has known issues (dotnet/runtime#104019). Use non-positional record with `{ get; init; }` properties instead.
- **Using `Ardalis.SmartEnum` in core:** External dependency. Hand-roll the ~50 lines.
- **`Microsoft.NET.Test.Sdk` with TUnit:** TUnit explicitly states this package is **incompatible** and prevents test discovery. TUnit projects use `<OutputType>Exe</OutputType>` and run via `dotnet run` or `dotnet test`.
- **Multi-targeting (net8.0 + net10.0):** No existing consumers. Single TFM reduces build complexity.
- **Struct for `ContextItem`:** Has reference-type fields (string, IReadOnlyList, IReadOnlyDictionary). Copying cost outweighs benefits.

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
| --- | --- | --- | --- |
| Git-based versioning | Manual `<Version>` bumping | MinVer 7.0.0 | Eliminates human error, works with CI tag-based releases |
| Source debugging for consumers | Nothing (skip it) | Microsoft.SourceLink.GitHub | Zero runtime cost, consumers can step through source |
| Public API tracking | Manual review of API changes | PublicApiAnalyzers 3.3.4 | Compiler-enforced, catches accidental API breaks at build time |
| Central package version management | Duplicated versions across csproj | Directory.Packages.props (CPM) | Built into NuGet, no extra tooling |

**Key insight:** Phase 1 build infrastructure decisions are permanent. Retrofitting `PublicApiAnalyzers` or CPM later requires touching every project. Set them up from day 1.

## Common Pitfalls

### Pitfall 1: TUnit + Microsoft.NET.Test.Sdk Conflict
**What goes wrong:** Adding `Microsoft.NET.Test.Sdk` to a TUnit project causes test discovery to fail silently.
**Why it happens:** TUnit uses Microsoft.Testing.Platform directly and has its own entry point generation. The test SDK conflicts with this.
**How to avoid:** Never add `Microsoft.NET.Test.Sdk` to TUnit projects. Use `<OutputType>Exe</OutputType>` and the `TUnit` meta-package only.
**Warning signs:** Tests compile but `dotnet test` finds 0 tests.

### Pitfall 2: [JsonPropertyName] on Record Primary Constructor Parameters
**What goes wrong:** `[JsonPropertyName]` attributes on primary constructor parameters may not be respected during deserialization in some System.Text.Json versions.
**Why it happens:** `[JsonPropertyName]` targets properties, not parameters. Applying it to a primary constructor parameter applies it to the parameter, not the synthesized property.
**How to avoid:** Use non-positional records with explicit `{ get; init; }` properties and `[JsonPropertyName]` on each property.
**Warning signs:** JSON property names revert to C# property names during serialization.

### Pitfall 3: PublicApiAnalyzers Missing Nullable Header
**What goes wrong:** RS0036/RS0041 warnings about nullable annotations not tracked.
**Why it happens:** `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` need `#nullable enable` as first line when the project uses nullable reference types.
**How to avoid:** Add `#nullable enable` as the first line of both `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` immediately on creation.
**Warning signs:** Spurious RS0036 warnings about nullable annotation changes.

### Pitfall 4: ContextBudget Deserialization Without [JsonConstructor]
**What goes wrong:** System.Text.Json cannot deserialize `ContextBudget` because it has a parameterized constructor with validation.
**Why it happens:** STJ needs to know which constructor to use. With a single constructor it auto-detects, but `[JsonConstructor]` makes intent explicit and prevents future breakage if overloads are added.
**How to avoid:** Add `[JsonConstructor]` to the `ContextBudget` constructor. Ensure parameter names match JSON property names (camelCase matching works by default).
**Warning signs:** `JsonException` on deserialization of ContextBudget.

### Pitfall 5: Smart Enum Dictionary Key Serialization
**What goes wrong:** When `ContextKind` is used as a dictionary key (e.g., in `ReservedSlots`), System.Text.Json cannot serialize/deserialize it by default.
**Why it happens:** STJ requires dictionary keys to be strings or have a `TypeConverter`. A custom `JsonConverter<ContextKind>` alone does not enable dictionary key support.
**How to avoid:** Implement a `JsonConverter<Dictionary<ContextKind, int>>` or use `TypeConverter` on `ContextKind`. Alternatively, serialize `ReservedSlots` as a flat array of `{kind, tokens}` pairs.
**Warning signs:** `NotSupportedException` when serializing `ContextBudget.ReservedSlots`.

### Pitfall 6: BenchmarkDotNet TreatWarningsAsErrors Conflict
**What goes wrong:** BenchmarkDotNet's generated benchmark project inherits `TreatWarningsAsErrors` from `Directory.Build.props` and fails to compile.
**Why it happens:** BenchmarkDotNet generates temporary projects that may produce warnings from generated code.
**How to avoid:** The benchmark project itself should work fine; the issue is in BenchmarkDotNet's auto-generated runner projects. BenchmarkDotNet 0.15.8 sets `<ImportDirectoryBuildProps>false</ImportDirectoryBuildProps>` in generated projects, so this is handled. Verify early with a simple benchmark.
**Warning signs:** Benchmark runs fail with CS-prefixed compiler errors in temporary directories.

## Code Examples

### TUnit Test Structure

```csharp
// Source: TUnit official docs (Context7 /thomhurst/tunit)
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

public class ContextItemTests
{
    [Test]
    public async Task Content_Is_Required()
    {
        // ContextItem requires Content and Tokens
        var item = new ContextItem { Content = "hello", Tokens = 5 };
        await Assert.That(item.Content).IsEqualTo("hello");
        await Assert.That(item.Tokens).IsEqualTo(5);
    }

    [Test]
    public async Task Default_Tags_Is_Empty()
    {
        var item = new ContextItem { Content = "test", Tokens = 1 };
        await Assert.That(item.Tags).IsEmpty();
    }

    [Test]
    [Arguments("Message")]
    [Arguments("Document")]
    [Arguments("ToolOutput")]
    public async Task ContextKind_WellKnown_Values_Exist(string value)
    {
        var kind = new ContextKind(value);
        await Assert.That(kind.Value).IsEqualTo(value);
    }

    [Test]
    public async Task ContextKind_Is_Case_Insensitive()
    {
        var lower = new ContextKind("message");
        var upper = new ContextKind("MESSAGE");
        await Assert.That(lower).IsEqualTo(upper);
    }
}
```

### TUnit Project File

```xml
<!-- tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="TUnit" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Wollax.Cupel\Wollax.Cupel.csproj" />
  </ItemGroup>
</Project>
```

### BenchmarkDotNet Empty Baseline

```csharp
// benchmarks/Wollax.Cupel.Benchmarks/Program.cs
using BenchmarkDotNet.Running;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
```

```csharp
// benchmarks/Wollax.Cupel.Benchmarks/EmptyPipelineBenchmark.cs
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class EmptyPipelineBenchmark
{
    private ContextItem[] _items = null!;

    [Params(100, 250, 500)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _items = Enumerable.Range(0, ItemCount)
            .Select(i => new ContextItem
            {
                Content = $"Item {i}",
                Tokens = 10
            })
            .ToArray();
    }

    [Benchmark(Baseline = true)]
    public int BaselineIteration()
    {
        // Baseline: just iterate items (establishes floor)
        var sum = 0;
        foreach (var item in _items)
            sum += item.Tokens;
        return sum;
    }
}
```

### Core Project File

```xml
<!-- src/Wollax.Cupel/Wollax.Cupel.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <Description>Context management library for coding agents. Select optimal context windows with scoring, slicing, and placement.</Description>
    <PackageReadmeFile>README.md</PackageReadmeFile>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="All" />
  </ItemGroup>
  <ItemGroup>
    <AdditionalFiles Include="PublicAPI.Shipped.txt" />
    <AdditionalFiles Include="PublicAPI.Unshipped.txt" />
  </ItemGroup>
</Project>
```

### PublicAPI Text Files (Initial)

```
// PublicAPI.Shipped.txt
#nullable enable
```

```
// PublicAPI.Unshipped.txt
#nullable enable
```

Both files start with `#nullable enable` and nothing else. As public API is added, invoke the RS0016 code fix to populate `PublicAPI.Unshipped.txt`. Before a release, move entries to `PublicAPI.Shipped.txt`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
| --- | --- | --- | --- |
| xUnit v2 / NUnit 3 | TUnit 1.x (source-gen, parallel-by-default) | 2024-2025 | Faster test discovery, no reflection, .NET 10 native |
| Manual `<Version>` tags | MinVer 7.0 (git-tag-based) | Mature, v7 Jan 2026 | Eliminates version management ceremony |
| SourceLink 8.0 | SourceLink 10.0.102 | Jan 2026 | Aligns with .NET 10 SDK versioning |
| `PackageVersion` scattered | Central Package Management | .NET 6+ era | Single source of truth for versions |
| FluentAssertions | TUnit built-in assertions | 2025 (FA licensing change) | No external assertion library needed for TUnit |

**Deprecated/outdated:**
- **FluentAssertions 8+**: Commercial license since January 2025 (Xceed partnership). Not relevant here since TUnit has built-in assertions.
- **Microsoft.NET.Test.Sdk with TUnit**: Explicitly incompatible. Do not add.

## Open Questions

1. **ContextBudget: sealed class vs sealed record?**
   - What we know: Records give value equality and `with` expressions. Classes are more natural for constructor validation.
   - What's unclear: Whether `with` expression support on ContextBudget is valuable (would let users do `budget with { TargetTokens = 5000 }`).
   - Recommendation: Use sealed class. ContextBudget has validation constraints that `with` expressions would bypass (e.g., TargetTokens > MaxTokens check). If `with` is desired later, add explicit mutation methods.

2. **Smart enum dictionary key serialization**
   - What we know: `ContextKind` is used as a dictionary key in `ContextBudget.ReservedSlots`. STJ requires special handling for non-string dictionary keys.
   - What's unclear: Whether to add `TypeConverter` to `ContextKind` or write a specialized dictionary converter.
   - Recommendation: Start with a custom `JsonConverter` for the dictionary. Defer `TypeConverter` unless other consumers need it.

3. **TUnit assertion style: await-based vs sync**
   - What we know: TUnit assertions are async (`await Assert.That(...).IsEqualTo(...)`). This is mandatory, not optional.
   - What's unclear: Whether missing `await` causes silent test passes.
   - Recommendation: TUnit ships with a built-in analyzer that flags missing `await` on assertions. Ensure this analyzer is active (it is by default with the TUnit package).

## Sources

### Primary (HIGH confidence)
- Context7 `/thomhurst/tunit` -- TUnit installation, test discovery, assertions, data-driven tests, lifecycle hooks
- Context7 `/dotnet/benchmarkdotnet` -- BenchmarkDotNet setup, MemoryDiagnoser, configuration
- [NuGet: TUnit 1.19.22](https://www.nuget.org/packages/TUnit) -- Version, .NET 10 support, dependencies verified
- [NuGet: BenchmarkDotNet 0.15.8](https://www.nuget.org/packages/BenchmarkDotNet) -- Version, .NET 10 support verified
- [NuGet: Microsoft.SourceLink.GitHub 10.0.102](https://www.nuget.org/packages/Microsoft.SourceLink.GitHub) -- Latest version verified
- [NuGet: MinVer 7.0.0](https://www.nuget.org/packages/MinVer/) -- Latest version verified
- [NuGet: Microsoft.CodeAnalysis.PublicApiAnalyzers 3.3.4](https://www.nuget.org/packages/Microsoft.CodeAnalysis.PublicApiAnalyzers/) -- Latest stable verified
- [Microsoft Learn: Central Package Management](https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management) -- CPM setup
- [Microsoft Learn: global.json](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json) -- SDK pinning format

### Secondary (MEDIUM confidence)
- [dotnet/roslyn-analyzers PublicApiAnalyzers.Help.md](https://github.com/dotnet/roslyn-analyzers/blob/main/src/PublicApiAnalyzers/PublicApiAnalyzers.Help.md) -- PublicAPI.txt setup, RS0016 workflow
- [dotnet/runtime#104019](https://github.com/dotnet/runtime/issues/104019) -- JsonPropertyName on record primary constructors limitation
- [Ardalis/SmartEnum](https://github.com/ardalis/SmartEnum) -- Smart enum pattern reference (not used as dependency)

### Tertiary (LOW confidence)
- None -- all findings verified with primary or secondary sources.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- all versions verified on NuGet, .NET 10 compatibility confirmed
- Architecture (records, smart enums): HIGH -- standard C# patterns, STJ behavior verified via official docs
- Build infrastructure (CPM, global.json, PublicApiAnalyzers): HIGH -- Microsoft-documented, widely adopted
- TUnit specifics: HIGH -- verified via Context7 and NuGet, explicit .NET 10 TFM support
- Pitfalls: MEDIUM -- based on known issues and documented behavior, but some (dictionary key serialization) need validation during implementation

**Research date:** 2026-03-10
**Valid until:** 2026-04-10 (stable ecosystem, 30-day validity)
