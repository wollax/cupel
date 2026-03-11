# Stack Research: Cupel Context Management Library

**Date**: 2026-03-10
**Scope**: .NET 10 / C# 14 stack decisions for a high-performance, zero-dependency context management pipeline library.

---

## 1. Runtime & Language: .NET 10 LTS / C# 14

**.NET 10** is the current LTS release (supported through November 2028). Ships with C# 14.

### C# 14 Features to Leverage

| Feature | Relevance to Cupel | Confidence |
|---|---|---|
| **Extension members** (new `extension` blocks) | Extension properties for fluent API on `IServiceCollection`, `ContextItem` helpers. Replaces static extension method classes with cleaner syntax. | HIGH |
| **`field` keyword** (field-backed properties) | Validation in property setters without explicit backing fields. Use on `ContextItem.Content` (null guard), `ContextBudget.MaxTokens` (range guard). | HIGH |
| **Implicit Span conversions** | First-class `Span<T>` / `ReadOnlySpan<T>` conversions. Use for zero-allocation iteration in hot paths (scorer loops, slicer candidate arrays). | HIGH |
| **Null-conditional assignment** (`?.` on LHS) | Cleaner trace collector patterns: `trace?.Record(...)` assignment. | MEDIUM |
| **Lambda parameter modifiers** | `ref`/`in` on lambda params without type annotation. Useful for span-based delegates in scoring. | MEDIUM |
| **Partial constructors/events** | Limited relevance. Skip unless source generators are added later. | LOW |

### .NET 10 Runtime/BCL Improvements to Leverage

| Feature | Relevance | Confidence |
|---|---|---|
| **JIT inlining improvements** | Pipeline hot paths benefit automatically. Ensure methods are small enough to inline. | HIGH |
| **Method devirtualization** | Interface dispatch for `IScorer`, `ISlicer`, `IPlacer` benefits from sealed concrete types. Mark implementations as `sealed` where possible. | HIGH |
| **Stack allocation improvements** | Compiler can stack-allocate more structs. Design `ScoredItem` as a readonly struct. | HIGH |
| **System.Text.Json improvements** | New `ReferenceHandler` options, strict serialization settings. Use in `Wollax.Cupel.Json`. | HIGH |
| **AVX10.2 / SIMD** | Overkill for <500 items. Do not pursue. | HIGH (not needed) |

### Recommendation

Target `net10.0` exclusively. No multi-targeting. This is a new library with no legacy consumers, and .NET 10 LTS gives 3 years of support. Single TFM keeps the build simple and allows full use of C# 14 and runtime improvements.

---

## 2. Testing Framework

### Decision: xUnit v3

**Package**: `xunit.v3` v3.2.2 (January 2026)
**Confidence**: HIGH (verified on NuGet, xunit.net release notes)

| Criterion | xUnit v3 | NUnit 4.x | MSTest 3.x |
|---|---|---|---|
| .NET 10 support | Yes (net10.0 template) | Yes | Yes |
| Parallel by default | Yes | Opt-in | Opt-in |
| DI/constructor injection | Built-in (test class constructors) | Limited | Limited |
| Community momentum | Dominant for .NET libraries | Strong | Microsoft-backed |
| Microsoft Testing Platform v2 | Supported | Supported | Native |

**Why xUnit v3 over alternatives**:
- De facto standard for .NET library projects (runtime, ASP.NET Core, EF Core all use xUnit).
- Constructor injection aligns with Cupel's DI companion package testing.
- Parallel execution by default catches thread-safety issues early.
- v3 is a ground-up rewrite with `ValueTask` lifecycle, no `IClassFixture` ceremony overhead.

**What NOT to use**: TUnit (too new, <1 year old, ecosystem tooling immature). MSTest (better for application projects, not library projects).

### Assertion Library: Shouldly

**Package**: `Shouldly` (latest stable, MIT license)
**Confidence**: MEDIUM

FluentAssertions v8+ requires a commercial license ($130/dev) since the Xceed partnership (January 2025). FluentAssertions 7.x remains Apache 2.0 but will only receive critical bug fixes.

**Recommendation**: Use **Shouldly** (MIT, actively maintained). Fluent readable assertions without licensing concerns. If assertion needs are simple, xUnit's built-in `Assert` class is also fine for a library project.

**Alternative considered**: `AwesomeAssertions` (community fork of FluentAssertions under original Apache 2.0 license). Viable but smaller community. Shouldly is the safer bet.

### Snapshot Testing: Verify

**Package**: `Verify.XunitV3` v31.13.2
**Confidence**: HIGH

Useful for testing `ContextResult`, `SelectionReport`, and `ContextTrace` serialization output. Snapshot tests catch unintended changes to public API shapes and JSON output.

---

## 3. Benchmarking: BenchmarkDotNet

**Package**: `BenchmarkDotNet` v0.15.8
**Confidence**: HIGH (verified on NuGet, supports .NET 10 via `RuntimeMoniker.Net10`)

### Configuration for Sub-Millisecond Targets

The <1ms pipeline target for <500 items requires careful benchmark configuration:

```csharp
[Config(typeof(CupelBenchmarkConfig))]
[MemoryDiagnoser]
public class PipelineBenchmarks
{
    private class CupelBenchmarkConfig : ManualConfig
    {
        public CupelBenchmarkConfig()
        {
            AddJob(Job.Default
                .WithRuntime(CoreRuntime.Core100) // .NET 10
                .WithWarmupCount(5)
                .WithIterationCount(20)
                .WithIterationTime(TimeInterval.Millisecond * 250)
                .WithMaxRelativeError(0.02));

            AddDiagnoser(MemoryDiagnoser.Default);
        }
    }
}
```

### Key Diagnostics

| Diagnoser | Purpose |
|---|---|
| `[MemoryDiagnoser]` | Track allocations. Target: 0 bytes on hot paths with tracing disabled. |
| `[ThreadingDiagnoser]` | Monitor lock contention if any shared state exists. |
| Custom column for "items/sec" | Derived metric: items processed per second at various scales (100, 500, 1000). |

### Benchmark Structure

Create a dedicated `Wollax.Cupel.Benchmarks` project (not a NuGet package). Benchmark scenarios:
- **Pipeline end-to-end**: 100, 250, 500 items through full Classify-Score-Deduplicate-Slice-Place.
- **Individual stages**: Each stage in isolation.
- **Allocation tracking**: Tracing enabled vs disabled to verify zero-alloc guarantee.
- **Scorer composite**: Deep vs flat composite scorer nesting.

---

## 4. NuGet Packaging

### Multi-Package Solution Structure

```
cupel/
  Directory.Build.props          # Shared metadata, versioning
  Directory.Build.targets        # Shared build logic
  Directory.Packages.props       # Central Package Management
  cupel.sln
  src/
    Wollax.Cupel/
    Wollax.Cupel.Extensions.DependencyInjection/
    Wollax.Cupel.Tiktoken/
    Wollax.Cupel.Json/
  tests/
    Wollax.Cupel.Tests/
    Wollax.Cupel.Extensions.DependencyInjection.Tests/
    Wollax.Cupel.Tiktoken.Tests/
    Wollax.Cupel.Json.Tests/
  benchmarks/
    Wollax.Cupel.Benchmarks/
```

### Central Package Management (CPM)

**Confidence**: HIGH (standard practice, Microsoft-recommended)

Use `Directory.Packages.props` at solution root with `<ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>`. All version pins in one file.

### Directory.Build.props (Shared Metadata)

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
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <PackageTags>context;llm;agent;pipeline;token;scoring</PackageTags>

    <!-- Deterministic builds -->
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <EmbedUntrackedSources>true</EmbedUntrackedSources>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>
</Project>
```

### Versioning Strategy

Use **MinVer** or manual `<Version>` in `Directory.Build.props`. MinVer derives version from git tags (e.g., `v1.0.0-alpha.1`), which aligns with the tag-based release workflow.

**Package**: `MinVer` (latest, MIT license)
**Confidence**: MEDIUM (popular convention, but manual versioning is also fine for 4 packages)

### Package Dependency Graph

```
Wollax.Cupel                              → zero dependencies
Wollax.Cupel.Extensions.DependencyInjection → Wollax.Cupel + Microsoft.Extensions.DependencyInjection.Abstractions
Wollax.Cupel.Tiktoken                     → Wollax.Cupel + Microsoft.ML.Tokenizers (or Tiktoken)
Wollax.Cupel.Json                         → Wollax.Cupel (System.Text.Json is in-box for net10.0)
```

**Critical**: `Wollax.Cupel.Json` should NOT take a dependency on System.Text.Json NuGet package. For `net10.0`, System.Text.Json is part of the shared framework (in-box). Only reference the NuGet package if multi-targeting older TFMs, which we are not.

---

## 5. Tiktoken / Token Counting Companion

### Decision: Microsoft.ML.Tokenizers

**Package**: `Microsoft.ML.Tokenizers` v2.0.0 + encoding data packages
**Confidence**: HIGH

| Library | Version | Downloads | Maintainer | .NET 10 | Notes |
|---|---|---|---|---|---|
| **Microsoft.ML.Tokenizers** | 2.0.0 | 4.4M total | Microsoft (.NET team) | net8.0+ | Official. Has `TiktokenTokenizer.CreateForModel()`. |
| Tiktoken | 2.2.0 | 2.1M total | Community | net8.0+ | Performance-focused. Split packages per encoding. |
| TiktokenSharp | 1.2.0 | Lower | Community | Varies | Port of Python tiktoken. Less maintained. |
| SharpToken | 2.0.4 | Moderate | Community | net6.0+ | Deprecated in favor of Microsoft.ML.Tokenizers. |

**Why Microsoft.ML.Tokenizers**:
- Official Microsoft library, actively developed by the .NET team.
- `TiktokenTokenizer.CreateForModel("gpt-4o")` API is clean and well-documented.
- SharpToken's README explicitly recommends migrating to Microsoft.ML.Tokenizers.
- Long-term support guaranteed as part of Microsoft's AI stack.

**Companion package data dependencies**:
- `Microsoft.ML.Tokenizers.Data.O200kBase` (GPT-4o, o-series models)
- `Microsoft.ML.Tokenizers.Data.Cl100kBase` (GPT-4, GPT-3.5-turbo)

**Note**: Microsoft.ML.Tokenizers v2.0.0 targets net8.0 and netstandard2.0. It will work on .NET 10 via forward compatibility. The library may release a net10.0 target in a future version.

### Cupel.Tiktoken API Shape

```csharp
// Wollax.Cupel.Tiktoken provides a convenience bridge
public static class ContextItemExtensions
{
    extension(ContextItem item)
    {
        public int CountTokens(TiktokenTokenizer tokenizer)
            => tokenizer.CountTokens(item.Content);
    }
}

public static class ContextItemCollectionExtensions
{
    extension(IEnumerable<ContextItem> items)
    {
        public IEnumerable<ContextItem> WithTokenCounts(TiktokenTokenizer tokenizer)
            => items.Select(item => item with { Tokens = tokenizer.CountTokens(item.Content) });
    }
}
```

---

## 6. DI Integration (Wollax.Cupel.Extensions.DependencyInjection)

### Pattern: IServiceCollection Extension Methods

**Dependency**: `Microsoft.Extensions.DependencyInjection.Abstractions` (10.0.x)
**Confidence**: HIGH

Take a dependency on **Abstractions only**, not the full `Microsoft.Extensions.DependencyInjection` package. This is the standard pattern for library packages (EF Core, MediatR, Serilog all do this).

### Standard Convention

```csharp
namespace Microsoft.Extensions.DependencyInjection;

public static class CupelServiceCollectionExtensions
{
    public static IServiceCollection AddCupel(
        this IServiceCollection services,
        Action<CupelOptions>? configure = null)
    {
        services.AddSingleton<ICupelPipelineFactory, CupelPipelineFactory>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    public static IServiceCollection AddCupelPolicy(
        this IServiceCollection services,
        string name,
        CupelPolicy policy)
    {
        services.AddSingleton(new NamedCupelPolicy(name, policy));
        return services;
    }
}
```

**Key conventions**:
- Place extension methods in `Microsoft.Extensions.DependencyInjection` namespace (IntelliSense discoverability).
- Return `IServiceCollection` for chaining.
- Use `IOptions<T>` / `IOptionsMonitor<T>` patterns for configuration.
- Register pipeline factory as singleton (pipelines are stateless given a policy).

---

## 7. JSON Serialization (Wollax.Cupel.Json)

### Approach: System.Text.Json Source Generation

**In-box**: System.Text.Json ships with .NET 10 (no NuGet dependency needed).
**Confidence**: HIGH

### Source Generator Context

```csharp
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(CupelPolicy))]
[JsonSerializable(typeof(ContextBudget))]
[JsonSerializable(typeof(ContextResult))]
[JsonSerializable(typeof(SelectionReport))]
[JsonSerializable(typeof(ContextTrace))]
public partial class CupelJsonContext : JsonSerializerContext { }
```

**Why source generation over reflection**:
- Zero startup cost (no runtime reflection).
- Trim-safe and NativeAOT-compatible from day 1.
- Aligns with the zero-allocation philosophy.
- The `[JsonPropertyName]` attributes on all public types (PROJECT.md requirement) work with both reflection and source generation, but source generation makes them compile-time verified.

**Design decision**: The core `Wollax.Cupel` package puts `[JsonPropertyName]` on all public types but does NOT reference System.Text.Json. These attributes are in `System.Text.Json.Serialization` which is in-box for net10.0 (part of the shared framework, no package reference needed). The `Wollax.Cupel.Json` companion package provides the `JsonSerializerContext` and convenience serialize/deserialize methods.

### Polymorphic Serialization

For `IScorer` / `ISlicer` / `IPlacer` config serialization, use the `[JsonDerivedType]` + `[JsonPolymorphic]` attributes (available since .NET 7, in-box for .NET 10):

```csharp
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(RecencyScorerConfig), "recency")]
[JsonDerivedType(typeof(PriorityScorerConfig), "priority")]
// etc.
public abstract class ScorerConfig { }
```

This enables `RegisterScorer(string name, Func<IScorer> factory)` to work with JSON roundtripping.

---

## 8. CI/CD

### GitHub Actions Workflow

**Confidence**: HIGH

#### Build + Test (PR / push)

```yaml
# .github/workflows/ci.yml
name: CI
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore -c Release
      - run: dotnet test --no-build -c Release --logger trx
      - run: dotnet pack --no-build -c Release -o ./artifacts
      - uses: actions/upload-artifact@v4
        with:
          name: packages
          path: ./artifacts/*.nupkg
```

#### Publish (tag-triggered)

**Recommendation**: Use **NuGet Trusted Publishing** (OIDC, no stored API keys).

NuGet.org now supports Trusted Publishing via OpenID Connect. GitHub Actions requests a short-lived token (1-hour validity) directly from NuGet.org, eliminating the need for long-lived API keys stored in repository secrets.

```yaml
# .github/workflows/publish.yml
name: Publish
on:
  push:
    tags: ['v*']

permissions:
  id-token: write  # Required for OIDC

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Full history for MinVer
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build -c Release
      - run: dotnet test -c Release
      - run: dotnet pack -c Release -o ./artifacts
      - uses: dotnet/nuget-login@v1  # Trusted Publishing OIDC
      - run: dotnet nuget push ./artifacts/*.nupkg --source https://api.nuget.org/v3/index.json
```

**Setup**: Configure a Trusted Publishing policy on nuget.org linking your GitHub repo + workflow file.

---

## 9. What NOT to Add (and Why)

| Temptation | Reason to Skip |
|---|---|
| **Polly / resilience** | No network calls, no retry scenarios. Pure computation. |
| **Serilog / logging abstraction** | Core is zero-dependency. Tracing is internal (`ITraceCollector`). If consumers want logging, they wrap the trace collector. |
| **AutoMapper / Mapster** | 4 public models. Manual mapping is clearer and avoids a dependency. |
| **MediatR** | Fixed pipeline stages, not a mediator pattern. Would add complexity for no benefit. |
| **IMemoryCache** | Cupel is stateless per invocation. Caching is the caller's responsibility. |
| **Moq / NSubstitute** | Interfaces in Cupel are simple (1-2 methods). Hand-written test doubles are clearer and avoid NSubstitute's Castle.Core dependency. If needed, NSubstitute (MIT) is the better choice over Moq (SponsorLink controversy). |
| **Multi-targeting (net8.0 + net10.0)** | No existing consumers. Single TFM reduces build complexity and testing matrix. |
| **NativeAOT publishing** | Library doesn't need to publish as AOT. Being AOT-compatible (trim-safe) is enough, and source-generated JSON gives that for free. |
| **SourceLink** | Actually DO add this (Microsoft.SourceLink.GitHub). Zero-cost, enables step-through debugging for consumers. Not a runtime dependency. |

---

## 10. Recommended Development Dependencies

All in `Directory.Packages.props`:

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
  <ItemGroup>
    <!-- Production (companion packages only) -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
    <PackageVersion Include="Microsoft.ML.Tokenizers" Version="2.0.0" />
    <PackageVersion Include="Microsoft.ML.Tokenizers.Data.O200kBase" Version="2.0.0" />
    <PackageVersion Include="Microsoft.ML.Tokenizers.Data.Cl100kBase" Version="2.0.0" />

    <!-- Testing -->
    <PackageVersion Include="xunit.v3" Version="3.2.2" />
    <PackageVersion Include="Shouldly" Version="4.2.1" />
    <PackageVersion Include="Verify.XunitV3" Version="31.13.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />

    <!-- Benchmarking -->
    <PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />

    <!-- Build tooling -->
    <PackageVersion Include="MinVer" Version="6.0.0" />
    <PackageVersion Include="Microsoft.SourceLink.GitHub" Version="8.0.0" />
  </ItemGroup>
</Project>
```

**Note**: Verify `Shouldly` and `MinVer` latest versions before use -- the versions above are from training data, not verified against NuGet. All other versions are verified.

---

## 11. Summary: Stack at a Glance

| Layer | Choice | Version | Confidence |
|---|---|---|---|
| **Runtime** | .NET 10 LTS | 10.0 | HIGH |
| **Language** | C# 14 | — | HIGH |
| **Core dependencies** | None (BCL only) | — | HIGH |
| **DI companion** | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.0 | HIGH |
| **Tokenizer companion** | Microsoft.ML.Tokenizers | 2.0.0 | HIGH |
| **JSON companion** | System.Text.Json (in-box) + source generation | — | HIGH |
| **Test framework** | xUnit v3 | 3.2.2 | HIGH |
| **Assertions** | Shouldly (MIT) | Latest | MEDIUM |
| **Snapshot testing** | Verify.XunitV3 | 31.13.2 | HIGH |
| **Benchmarking** | BenchmarkDotNet | 0.15.8 | HIGH |
| **Versioning** | MinVer (git tag-based) | Latest | MEDIUM |
| **CI/CD** | GitHub Actions + NuGet Trusted Publishing | — | HIGH |
| **Package management** | Central Package Management (Directory.Packages.props) | — | HIGH |

---

## Verification Gaps

Items I could not fully verify and should be checked before implementation:

1. **Shouldly latest version**: Training data says 4.2.1 but this may be stale. Check NuGet.
2. **MinVer latest version**: Training data says 6.0.0. Check NuGet.
3. **Microsoft.ML.Tokenizers net10.0 TFM**: Currently targets net8.0. Verify it works on net10.0 via forward compat (should be fine, but test early).
4. **Microsoft.SourceLink.GitHub version**: May have a newer release. Check NuGet.
5. **`[JsonPropertyName]` without System.Text.Json PackageReference**: Verify the attribute is available in-box for net10.0 without an explicit NuGet reference (it should be, as it's part of the shared framework).
