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

---

# Rust Crate & Dual-Language Stack Research

**Date**: 2026-03-14
**Scope**: Moving `assay-cupel` Rust crate into the cupel monorepo, crates.io publishing, dual-language CI/CD.

**Context**: The Rust crate (`assay-cupel`) was developed in the separate `wollax/assay` repository during Phase 12. It passes all 28 required conformance tests. The next milestone involves migrating it into this monorepo and publishing to crates.io.

---

## 12. Crates.io Trusted Publishing

### Does crates.io support trusted publishing?

**Yes.** Crates.io supports Trusted Publishing via OIDC as of July 2025 (RFC 3691). Over 770 packages had configured it by January 2026.

**Confidence**: HIGH (verified via official Rust Blog, crates.io docs, RFC)

### How It Works

1. Configure a Trusted Publishing policy on crates.io linking your GitHub repo, workflow file, and optionally a GitHub Environment
2. The `rust-lang/crates-io-auth-action@v1` action exchanges a GitHub OIDC token for a short-lived crates.io access token (30 minutes)
3. The action's post-step automatically revokes the token when the job completes
4. `pull_request_target` and `workflow_run` triggers are blocked for security

### Workflow Template

```yaml
name: Publish to crates.io
on:
  push:
    tags: ['v*']

jobs:
  publish:
    runs-on: ubuntu-latest
    environment: release  # Optional: adds manual approval gate
    permissions:
      id-token: write     # Required for OIDC token exchange
    steps:
      - uses: actions/checkout@v4
      - uses: actions/rust-lang/setup-rust-toolchain@v1
      - uses: rust-lang/crates-io-auth-action@v1
        id: auth
      - run: cargo publish --manifest-path crates/assay-cupel/Cargo.toml
        env:
          CARGO_REGISTRY_TOKEN: ${{ steps.auth.outputs.token }}
```

### Symmetry with NuGet Publishing

Both registries now support OIDC trusted publishing, giving a consistent security model:

| Registry | OIDC Action | Token Lifetime | Setup Location |
|----------|-------------|----------------|----------------|
| NuGet.org | `NuGet/login@v1` | 1 hour | nuget.org publisher settings |
| crates.io | `rust-lang/crates-io-auth-action@v1` | 30 minutes | crates.io crate settings |

No long-lived API tokens need to be stored in repository secrets for either registry.

### crates.io Setup Steps

1. Log into crates.io with GitHub
2. Navigate to the crate's settings (after first manual `cargo publish`)
3. Add a Trusted Publishing configuration: repository owner, repo name, workflow filename
4. Optionally restrict to a specific GitHub Environment (recommended: `release`)

**Note**: The first publish of a new crate must be done manually with `cargo publish` and a personal API token. Trusted Publishing can only be configured after the crate exists on crates.io.

---

## 13. Cargo.toml Metadata for Published Crates

### Required Fields (crates.io rejects without these)

| Field | Purpose | Value for assay-cupel |
|-------|---------|----------------------|
| `name` | Crate name on registry | `assay-cupel` |
| `version` | SemVer version | `1.0.0` (tracks spec major.minor) |
| `description` | Short plain-text blurb | Required, non-empty |
| `license` OR `license-file` | SPDX 2.3 expression or path | `MIT` |

### Recommended Fields

| Field | Purpose | Value |
|-------|---------|-------|
| `edition` | Rust edition | `"2024"` |
| `rust-version` | MSRV declaration | `"1.85"` (see section 14) |
| `repository` | Source code URL | `https://github.com/wollax/cupel` |
| `homepage` | Project homepage | `https://github.com/wollax/cupel` |
| `documentation` | Docs URL | `https://docs.rs/assay-cupel` (auto-generated) |
| `readme` | Path to README | `crates/assay-cupel/README.md` |
| `keywords` | Max 5, alphanumeric/`-`/`_` | `["context", "llm", "agent", "token", "pipeline"]` |
| `categories` | Max 5, must match crates.io slugs | `["algorithms", "data-structures"]` |
| `authors` | List of authors | `["Wollax"]` |

### Custom Metadata for Spec Version

```toml
[package.metadata.cupel]
spec_version = "1.0"
```

This embeds the Cupel spec version the crate implements, enabling tooling to check compatibility. Spec major.minor is the cross-language compatibility anchor (per publishing brainstorm).

### Recommended Cargo.toml

```toml
[package]
name = "assay-cupel"
version = "1.0.0"
edition = "2024"
rust-version = "1.85"
description = "Cupel specification implementation — context management pipeline for LLM agents"
license = "MIT"
repository = "https://github.com/wollax/cupel"
homepage = "https://github.com/wollax/cupel"
readme = "README.md"
keywords = ["context", "llm", "agent", "token", "pipeline"]
categories = ["algorithms", "data-structures"]
authors = ["Wollax"]

[package.metadata.cupel]
spec_version = "1.0"

[dependencies]
thiserror = "2"
chrono = { version = "0.4", features = ["serde"] }

[dev-dependencies]
toml = "0.8"
serde = { version = "1", features = ["derive"] }
serde_json = "1"
```

**Confidence**: HIGH (fields verified against Cargo Book manifest reference)

### Keywords vs Categories

- **Keywords** are free-form (max 5, alphanumeric + `-` + `_`, max 20 chars each). Used for search.
- **Categories** must exactly match a slug from `https://crates.io/category_slugs`. Used for browsing/filtering. Relevant slugs: `algorithms`, `data-structures`, `text-processing`.

---

## 14. MSRV Policy and rust-toolchain.toml

### What is a Reasonable MSRV for a New Crate in 2026?

**Latest stable is Rust 1.94.0** (released 2026-03-05).

Common MSRV policies in the ecosystem:

| Policy | Used By | Implication for assay-cupel |
|--------|---------|----------------------------|
| N-2 stable versions | kube-rs | Would be ~1.92 |
| 6-month window | hyper | Would be ~1.88 |
| Edition minimum | Many new crates | 1.85 (edition 2024 minimum) |
| Latest stable | Some libraries | 1.94 |

**Recommendation: `rust-version = "1.85"`** (edition 2024 minimum)

**Rationale**:
- The crate already uses `edition = "2024"` (from the assay workspace), which requires Rust 1.85+
- Setting MSRV to the edition minimum is the simplest policy for a new crate
- No features from 1.86+ are required by the current implementation
- The Cargo resolver respects `rust-version` and will prefer compatible dependency versions
- Can be bumped to a newer version in a minor release if new features are needed

**Confidence**: HIGH — edition minimum is the most conservative sensible choice.

### rust-toolchain.toml Configuration

Place at **repository root** (not inside `crates/`). This ensures `rustup` auto-installs the correct toolchain when anyone enters the repo, and GitHub Actions respects it.

```toml
[toolchain]
channel = "1.85"
components = ["rustfmt", "clippy"]
```

**Key behaviors**:
- `rustup` reads this file automatically when running any `cargo` command in the repo
- `actions-rust-lang/setup-rust-toolchain@v1` reads it automatically — no explicit `toolchain` input needed
- Pins the CI toolchain to the MSRV, ensuring the crate builds on its declared minimum
- Components ensure `cargo fmt` and `cargo clippy` are available without extra setup

**Alternative**: Use `channel = "stable"` for always-latest in CI, and verify MSRV separately with `cargo msrv` or a dedicated CI job. This is more common for established crates but adds CI complexity.

**Recommendation**: Pin to MSRV in `rust-toolchain.toml` for simplicity. Add a separate "latest stable" CI job if compatibility issues arise.

**Confidence**: HIGH

---

## 15. Dual-Language CI/CD in GitHub Actions

### Architecture: Separate Workflows with Path Filters

A dual-language monorepo should NOT use a single monolithic CI workflow. Use **separate workflows per language** with path-based triggers, plus shared workflows for cross-cutting concerns.

**Confidence**: HIGH (standard pattern for polyglot repos)

### Proposed Workflow Structure

```
.github/workflows/
  ci.yml              # .NET build + test (existing, add path filters)
  ci-rust.yml          # Rust build + test + clippy + fmt (new)
  release.yml          # NuGet publish (existing, unchanged)
  release-rust.yml     # crates.io publish (new)
  spec.yml             # mdBook deploy (existing, unchanged)
```

### Path Filter Configuration

#### ci.yml (existing, add path filters)

```yaml
name: CI (.NET)
on:
  push:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/**'
      - 'benchmarks/**'
      - '*.sln'
      - 'Directory.*.props'
      - 'Directory.*.targets'
      - '.github/workflows/ci.yml'
  pull_request:
    branches: [main]
    paths:
      - 'src/**'
      - 'tests/**'
      - 'benchmarks/**'
      - '*.sln'
      - 'Directory.*.props'
      - 'Directory.*.targets'
      - '.github/workflows/ci.yml'
```

#### ci-rust.yml (new)

```yaml
name: CI (Rust)
on:
  push:
    branches: [main]
    paths:
      - 'crates/**'
      - 'Cargo.toml'
      - 'Cargo.lock'
      - 'rust-toolchain.toml'
      - '.github/workflows/ci-rust.yml'
  pull_request:
    branches: [main]
    paths:
      - 'crates/**'
      - 'Cargo.toml'
      - 'Cargo.lock'
      - 'rust-toolchain.toml'
      - '.github/workflows/ci-rust.yml'

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions-rust-lang/setup-rust-toolchain@v1
        # Reads rust-toolchain.toml automatically
        # Includes Swatinem/rust-cache by default
      - run: cargo build --release --manifest-path crates/assay-cupel/Cargo.toml
      - run: cargo test --manifest-path crates/assay-cupel/Cargo.toml
      - run: cargo clippy --manifest-path crates/assay-cupel/Cargo.toml -- -D warnings

  fmt:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions-rust-lang/setup-rust-toolchain@v1
        with:
          components: rustfmt
      - run: cargo fmt --manifest-path crates/assay-cupel/Cargo.toml -- --check
```

### Key Design Decisions

#### Why Separate Workflows (Not Matrix)

| Approach | Pros | Cons |
|----------|------|------|
| **Separate workflows** | Independent triggers, clear status checks, no wasted runs | More YAML files |
| **Single workflow with matrix** | Single file | Can't path-filter per matrix entry; .NET changes trigger Rust builds and vice versa |

Separate workflows are the standard approach for polyglot repos. GitHub's required status checks can be configured per workflow.

#### Path Filter Gotchas

1. **Negation patterns**: Use `!` prefix to exclude paths after including them. Order matters.
2. **Self-inclusion**: Always include the workflow file itself in path filters (`.github/workflows/ci-rust.yml`), so CI changes are tested.
3. **Tag pushes**: `paths` filters are NOT evaluated for tag pushes. Release workflows triggered by tags run unconditionally (which is correct).
4. **Required checks on PRs**: If a path-filtered workflow doesn't run (no matching files changed), the status check is "skipped." GitHub allows configuring branch protection to accept skipped checks for path-filtered workflows.

### actions-rust-lang/setup-rust-toolchain@v1

This is the recommended Rust GitHub Action (replaces the deprecated `actions-rs/toolchain`).

**Key features**:
- Reads `rust-toolchain.toml` automatically if present
- Integrates `Swatinem/rust-cache@v2` by default (caches `~/.cargo` and `target/`)
- Configures problem matchers for Rust compiler output
- Supports `components` and `targets` inputs for additional toolchain configuration

**Cache behavior**: Enabled by default. Cache key is based on `Cargo.lock`, toolchain, and job name. No manual cache configuration needed.

**Confidence**: HIGH (actively maintained, 3.7k stars, recommended by Rust GitHub Actions org)

### Conformance Test Integration

The conformance TOML test vectors live at `conformance/required/` and `conformance/optional/`. When the Rust crate moves into the monorepo, its integration tests should reference these vectors directly (path dependency), eliminating the need for copying or submodules.

Add `conformance/**` to the Rust CI path filter:

```yaml
paths:
  - 'crates/**'
  - 'conformance/**'  # Spec vector changes should re-run Rust tests
  - 'Cargo.toml'
  - 'Cargo.lock'
  - 'rust-toolchain.toml'
```

---

## 16. Monorepo Layout After Migration

### Proposed Directory Structure

```
cupel/
  .github/workflows/
    ci.yml               # .NET CI (path-filtered)
    ci-rust.yml           # Rust CI (path-filtered)
    release.yml           # NuGet publish (existing)
    release-rust.yml      # crates.io publish (new)
    spec.yml              # mdBook deploy (existing)

  # .NET
  src/                   # .NET source projects
  tests/                 # .NET test projects
  benchmarks/            # .NET benchmarks
  Directory.Build.props
  Directory.Packages.props
  cupel.sln

  # Rust
  crates/
    assay-cupel/
      Cargo.toml
      src/
      tests/
  Cargo.toml             # Workspace root (members = ["crates/*"])
  Cargo.lock
  rust-toolchain.toml

  # Shared
  conformance/           # TOML test vectors (consumed by both .NET and Rust)
  spec/                  # mdBook specification
  LICENSE
  README.md
```

### Workspace Cargo.toml (Root)

```toml
[workspace]
members = ["crates/*"]
resolver = "2"
```

This is a minimal workspace root. It exists solely to let `cargo` discover crates under `crates/`. The `.sln` file and `Cargo.toml` coexist at the repo root without conflict.

### Build Tool Isolation

- `dotnet` ignores `Cargo.toml`, `Cargo.lock`, `rust-toolchain.toml`, and `crates/`
- `cargo` ignores `*.sln`, `*.csproj`, `Directory.*.props`, `src/`, `tests/`, `benchmarks/`
- No build system contamination between languages

---

## 17. Release Workflow: crates.io

### release-rust.yml

```yaml
name: Publish to crates.io
on:
  workflow_dispatch:
    inputs:
      dry-run:
        description: 'Dry run — verify without publishing'
        required: false
        type: boolean
        default: false

permissions:
  id-token: write
  contents: write

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions-rust-lang/setup-rust-toolchain@v1
      - run: cargo test --manifest-path crates/assay-cupel/Cargo.toml
      - run: cargo clippy --manifest-path crates/assay-cupel/Cargo.toml -- -D warnings
      - run: cargo package --manifest-path crates/assay-cupel/Cargo.toml --allow-dirty
        # --allow-dirty because untracked .NET files exist in workspace

  publish:
    runs-on: ubuntu-latest
    needs: verify
    if: ${{ inputs.dry-run != true }}
    environment: release
    steps:
      - name: Verify running from main
        if: github.ref != 'refs/heads/main'
        run: |
          echo "::error::Must be run from main branch"
          exit 1

      - uses: actions/checkout@v4
      - uses: actions-rust-lang/setup-rust-toolchain@v1

      - uses: rust-lang/crates-io-auth-action@v1
        id: auth

      - run: cargo publish --manifest-path crates/assay-cupel/Cargo.toml
        env:
          CARGO_REGISTRY_TOKEN: ${{ steps.auth.outputs.token }}
```

### Parallel with NuGet Release Workflow

The existing `release.yml` uses `workflow_dispatch` with a dry-run option and the same `release` environment. The Rust workflow mirrors this structure for consistency:

| Concern | NuGet (release.yml) | crates.io (release-rust.yml) |
|---------|--------------------|-----------------------------|
| Trigger | `workflow_dispatch` | `workflow_dispatch` |
| Dry run | Yes | Yes |
| Environment | `release` (implied) | `release` |
| OIDC | `NuGet/login@v1` | `rust-lang/crates-io-auth-action@v1` |
| Branch guard | Main only | Main only |
| Versioning | MinVer (git tags) | `Cargo.toml` version field |

### Versioning Coordination

Per the publishing brainstorm, versions are independent per language but anchored to the spec version:
- **NuGet**: MinVer from git tags (e.g., `v1.0.3`)
- **crates.io**: Manual `version` field in `Cargo.toml` (e.g., `1.0.1`)
- **Compatibility**: Both track Cupel Spec 1.0 via `[package.metadata.cupel] spec_version = "1.0"`

---

## 18. Summary: Dual-Language Stack Additions

| Layer | Choice | Version | Confidence |
|---|---|---|---|
| **Rust edition** | 2024 | — | HIGH |
| **MSRV** | 1.85 (edition 2024 minimum) | — | HIGH |
| **Toolchain management** | `rust-toolchain.toml` at repo root | — | HIGH |
| **CI action** | `actions-rust-lang/setup-rust-toolchain@v1` | Latest | HIGH |
| **Crate publishing** | crates.io Trusted Publishing (OIDC) | — | HIGH |
| **OIDC action** | `rust-lang/crates-io-auth-action@v1` | Latest | HIGH |
| **CI architecture** | Separate workflows + path filters | — | HIGH |
| **Workspace layout** | `crates/assay-cupel/` under repo root | — | HIGH |
| **Workspace root** | `Cargo.toml` with `members = ["crates/*"]` | — | HIGH |
| **Conformance vectors** | Shared `conformance/` dir, path dependency | — | HIGH |

---

## 19. Verification Gaps (Rust/Dual-Language)

1. **First crates.io publish**: The initial `cargo publish` must be done manually with a personal API token before Trusted Publishing can be configured. Plan for this one-time step.
2. **`cargo package --allow-dirty`**: In a monorepo with .NET files, `cargo package` may complain about untracked files. Test whether `--allow-dirty` is needed or if `.gitignore` / Cargo's `exclude` field handles it cleanly.
3. **Category slugs**: Verify `algorithms` and `data-structures` are valid slugs at `https://crates.io/category_slugs` before publishing.
4. **Rust 1.85 on ubuntu-latest**: Verify that `actions-rust-lang/setup-rust-toolchain` can install 1.85 on the current GitHub Actions runner image.
5. **Path filter + required checks**: Test that GitHub branch protection allows "skipped" status checks when a path-filtered workflow doesn't run (e.g., .NET-only PR doesn't trigger Rust CI).
6. **`Cargo.lock` in version control**: For library crates, `Cargo.lock` is conventionally not committed. However, in a monorepo with CI, committing it ensures reproducible CI builds. Decide based on whether the workspace will contain binary targets.

### Sources

- [crates.io development update (Jan 2026)](https://blog.rust-lang.org/2026/01/21/crates-io-development-update/)
- [RFC 3691: Trusted Publishing for crates.io](https://rust-lang.github.io/rfcs/3691-trusted-publishing-cratesio.html)
- [crates.io Trusted Publishing docs](https://crates.io/docs/trusted-publishing)
- [rust-lang/crates-io-auth-action](https://github.com/rust-lang/crates-io-auth-action)
- [actions-rust-lang/setup-rust-toolchain](https://github.com/actions-rust-lang/setup-rust-toolchain)
- [Cargo Book: The Manifest Format](https://doc.rust-lang.org/cargo/reference/manifest.html)
- [Cargo Book: Publishing on crates.io](https://doc.rust-lang.org/cargo/reference/publishing.html)
- [crates.io Category Slugs](https://crates.io/category_slugs)
- [MSRV Policy Discussion (Rust API Guidelines)](https://github.com/rust-lang/api-guidelines/discussions/231)
- [Rust releases](https://releases.rs/)
- [Swatinem/rust-cache](https://github.com/Swatinem/rust-cache)

---

## 20. Rust Diagnostics System — Patterns and Implementation Strategy

**Date**: 2026-03-15
**Scope**: Rust-specific research for adding diagnostics/tracing/reporting to the Cupel crate, mirroring the .NET `ITraceCollector` pattern. Zero external deps in core remains hard constraint.

---

### 20.1 Core Pattern: Trait-Based Internal Observer

The idiomatic Rust approach for internal pipeline diagnostics in a library crate — where the library author, not the distributed tracing ecosystem, owns the event model — is a **sealed trait with two implementations**: a no-op null implementation and a collecting implementation.

This mirrors the .NET `ITraceCollector` / `NullTraceCollector` / `DiagnosticTraceCollector` split exactly, with Rust-native idioms applied.

**Confidence**: HIGH — this pattern is used by `serde` (visitor), `tokio` (runtime hooks), `sqlx` (logger), and the Rust standard library itself (via `fmt::Write`). It avoids the overhead of `tokio-tracing`, which targets distributed observability rather than pipeline introspection.

#### Why NOT the `tracing` crate

The `tracing` crate is the correct choice for distributed observability (spans, structured events routed to subscribers). It is the wrong choice here because:

1. It introduces an optional dependency with its own subscriber model that callers must configure.
2. `tracing` events are structurally opaque to callers who want to inspect them (e.g., to build a dry-run report).
3. Cupel diagnostics are **synchronous, pipeline-scoped, return-value-accessible** — the caller asks the pipeline to run and gets back a report. This is not an observability problem; it is an explainability problem.

#### Recommended Trait Shape

```rust
/// Collects diagnostic events during a pipeline run.
///
/// The trait is sealed: only implementations within this crate
/// are permitted. This allows adding new event methods without
/// breaking downstream code.
pub trait TraceCollector: private::Sealed {
    fn on_classify(&mut self, pinned: usize, scoreable: usize);
    fn on_score(&mut self, item_content_hash: u64, score: f64);
    fn on_deduplicate(&mut self, removed: usize);
    fn on_slice(&mut self, selected: usize, budget_used: i64);
    fn on_exclude(&mut self, item_content_hash: u64, reason: ExclusionReason);
}

mod private {
    pub trait Sealed {}
}
```

**Why `&mut self`**: Collecting implementations need to accumulate state. The `&mut self` receiver on a trait object does not preclude `Box<dyn TraceCollector>`; it just means the caller holds the boxed value mutably. This is correct — a diagnostics collector is stateful by design.

**Seal reasoning**: New pipeline stages or new exclusion reasons will require new methods on the trait. Without sealing, adding a method is a breaking change. With sealing, only the crate can implement the trait, so new methods are non-breaking additions.

---

### 20.2 `ExclusionReason` — the Central Enum

The .NET `ExclusionReason` enum is the key vocabulary of the diagnostics system. In Rust, this maps to a `#[non_exhaustive]` enum.

```rust
/// Reason an item was excluded from the final context window.
#[derive(Debug, Clone, PartialEq, Eq, Hash)]
#[non_exhaustive]
pub enum ExclusionReason {
    /// Item was deduplicated (lower-scored duplicate of another item).
    Deduplicated,
    /// Item did not fit within the token budget.
    BudgetExceeded,
    /// Item was excluded by quota slicer (kind cap reached).
    QuotaCapReached,
    /// Item was excluded by quota slicer (kind was not in quota configuration).
    QuotaKindUnknown,
}
```

**Why `#[non_exhaustive]`**: Adding new exclusion reasons in future milestones (e.g., a filter stage, a classifier extension) must not break downstream code that `match`es this enum. `#[non_exhaustive]` forces a wildcard arm in downstream crates while allowing exhaustive matching within the library.

**Confidence**: HIGH — this is the official Rust API Guidelines recommendation (C-STRUCT-PRIVATE, future-proofing section) and verified against the Rust Reference `#[non_exhaustive]` documentation.

**Interaction with pattern matching**: Within the library, `match reason { ExclusionReason::Deduplicated => ... }` compiles without a wildcard arm. Downstream users must write `_ => {}`. This is intentional.

---

### 20.3 Null Collector — Zero-Overhead Guarantee

The null implementation must compile to no-op branches that LLVM eliminates entirely. Two approaches:

**Option A: Unit struct implementing the trait**

```rust
/// A no-op trace collector. All methods are empty and compile to nothing.
pub struct NullTraceCollector;
impl private::Sealed for NullTraceCollector {}
impl TraceCollector for NullTraceCollector {
    #[inline(always)]
    fn on_classify(&mut self, _: usize, _: usize) {}
    // ...
}
```

`#[inline(always)]` on every method ensures LLVM sees through the trait dispatch when the concrete type is `NullTraceCollector`. When `Pipeline::run` calls `collector.on_classify(...)` with a `&mut NullTraceCollector`, the call disappears after inlining.

**Option B: Generic over the collector type** (higher-performance, zero dispatch cost)

```rust
pub struct Pipeline<C: TraceCollector = NullTraceCollector> {
    collector: C,
    // ...
}
```

This is the approach used by `serde`'s visitor pattern and `hashbrown`'s allocator abstraction. When `C = NullTraceCollector`, the compiler monomorphizes a version of the pipeline with all collector calls fully eliminated. No vtable, no indirect call.

**Recommendation**: Start with Option A (simpler, one `Pipeline` type, Box-friendly). Upgrade to Option B only if profiling shows measurable overhead at the call sites. For typical pipeline sizes (<500 items), the difference is negligible.

**Why not `Option<&mut dyn TraceCollector>`**: Passing an optional reference into `pipeline.run_with_trace(items, budget, Some(&mut collector))` is ergonomically awkward and requires the null case to be spelled out at every call site. A dedicated `NullTraceCollector` keeps the null path ergonomic and explicit.

---

### 20.4 `SelectionReport` / `ContextTrace` — the Report Types

Mirror the .NET report types as plain data structs.

```rust
/// A record of a single item's fate in the pipeline.
#[derive(Debug, Clone)]
pub struct ItemTrace {
    /// First 32 characters of the item's content (for readability without cloning full content).
    pub content_preview: String,
    pub tokens: i64,
    pub score: Option<f64>,
    pub included: bool,
    pub exclusion_reason: Option<ExclusionReason>,
}

/// A full diagnostic report from one pipeline run.
#[derive(Debug, Clone, Default)]
pub struct SelectionReport {
    pub pinned_count: usize,
    pub scoreable_count: usize,
    pub deduplication_removed: usize,
    pub selected_count: usize,
    pub budget_used: i64,
    pub items: Vec<ItemTrace>,
}
```

**API guidelines applied**:
- `#[derive(Debug, Clone, Default)]` — required by C-COMMON-TRAITS. `Default` is valuable for incremental construction in the collecting implementation.
- Fields are `pub` because this is a report/DTO type — consumers need to read them. Private fields would add getter boilerplate with no safety benefit.
- Do NOT mark `SelectionReport` with `#[non_exhaustive]` — it is purely an output type, not a discriminant. Adding fields to it later is fine; callers do not construct it.
- DO mark `ItemTrace` with `#[non_exhaustive]` — callers may pattern-match on it in future. This is debatable; prefer deciding at the time a downstream consumer pattern emerges.

---

### 20.5 `DiagnosticTraceCollector` — the Collecting Implementation

```rust
/// A trace collector that accumulates a full [`SelectionReport`].
#[derive(Debug, Default)]
pub struct DiagnosticTraceCollector {
    report: SelectionReport,
}

impl DiagnosticTraceCollector {
    pub fn new() -> Self { Self::default() }

    /// Consumes the collector and returns the accumulated report.
    #[must_use]
    pub fn into_report(self) -> SelectionReport {
        self.report
    }
}
```

`#[must_use]` on `into_report` (and on `SelectionReport` itself via `#[must_use]` on the struct) ensures callers who call `pipeline.run_with_trace(...)` and create a `DiagnosticTraceCollector` do not silently discard the report.

---

### 20.6 Pipeline Integration — Two API Surface Options

**Option A: Separate `run_with_trace` method**

```rust
impl Pipeline {
    pub fn run(&self, items: &[ContextItem], budget: &ContextBudget)
        -> Result<Vec<ContextItem>, CupelError> { ... }

    pub fn run_with_trace(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        collector: &mut impl TraceCollector,
    ) -> Result<Vec<ContextItem>, CupelError> { ... }
}
```

Pros: Clean separation. No API change to the existing `run` signature.
Cons: Code duplication between `run` and `run_with_trace`, or an internal forwarding indirection.

**Option B: Internal trait routing (preferred)**

Both `run` and `run_with_trace` delegate to an internal `run_inner<C: TraceCollector>` that is generic. `run` passes `&mut NullTraceCollector`. This keeps zero-overhead for the no-trace path at no maintenance cost.

```rust
impl Pipeline {
    pub fn run(&self, items: &[ContextItem], budget: &ContextBudget)
        -> Result<Vec<ContextItem>, CupelError> {
        self.run_inner(items, budget, &mut NullTraceCollector)
    }

    pub fn run_with_trace(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        collector: &mut impl TraceCollector,
    ) -> Result<Vec<ContextItem>, CupelError> {
        self.run_inner(items, budget, collector)
    }

    fn run_inner<C: TraceCollector>(
        &self,
        items: &[ContextItem],
        budget: &ContextBudget,
        collector: &mut C,
    ) -> Result<Vec<ContextItem>, CupelError> { ... }
}
```

**Recommendation**: Option B. Zero duplication, zero overhead for existing callers, clean upgrade path.

**Confidence**: HIGH — this is the same pattern used by `hashbrown` for allocator abstraction and by `bytes` for buffer customization.

---

### 20.7 Dry-Run Support

The .NET `DryRun` capability (run the pipeline for diagnostic purposes without returning a result) maps cleanly to:

```rust
pub fn dry_run(
    &self,
    items: &[ContextItem],
    budget: &ContextBudget,
) -> Result<SelectionReport, CupelError> {
    let mut collector = DiagnosticTraceCollector::new();
    let _ = self.run_with_trace(items, budget, &mut collector)?;
    Ok(collector.into_report())
}
```

This is a thin convenience wrapper — no separate pipeline code path needed. The `?` propagates errors; on success, the caller gets the full report. The items result is discarded.

---

### 20.8 API Design Conventions — `#[must_use]`, `#[non_exhaustive]`, Derives

Verified against the Rust API Guidelines (C-COMMON-TRAITS, C-STRUCT-PRIVATE, future-proofing section):

| Convention | Where to Apply | Rationale |
|---|---|---|
| `#[must_use]` on `into_report()` | `DiagnosticTraceCollector::into_report` | Calling it and discarding the report is always a bug. Clippy `must_use_candidate` lint will flag omissions. |
| `#[must_use]` on `SelectionReport` struct | On the type itself | Propagates to all methods returning `SelectionReport`. |
| `#[non_exhaustive]` | `ExclusionReason` enum | New reasons will be added in future milestones. |
| `#[derive(Debug, Clone, PartialEq, Eq, Hash)]` | `ExclusionReason` | C-COMMON-TRAITS. `Hash` enables using reasons as map keys in reports. |
| `#[derive(Debug, Clone, Default)]` | `SelectionReport`, `DiagnosticTraceCollector` | C-COMMON-TRAITS. `Default` allows incremental construction. |
| No `Copy` on report types | `SelectionReport`, `ItemTrace` | Contains `Vec<ItemTrace>` / `String` — not `Copy`. Correct. |
| Sealed `TraceCollector` | `mod private` pattern | Allows non-breaking method additions. Verified: this is C-SEALED in API guidelines. |
| `#[inline(always)]` | All `NullTraceCollector` methods | Guarantees LLVM eliminates no-op calls. |

**Derive strategy — prefer derivation, avoid manual bounds**:

Per API Guidelines C-STRUCT-BOUNDS: Do NOT write `#[derive(Clone)] struct SelectionReport<T: Clone>`. Instead write `#[derive(Clone)] struct SelectionReport`. The derive macro adds bounds only where needed, not reflexively to the struct definition. This codebase has no generic report types so this constraint does not apply immediately, but is noted for future extension.

---

### 20.9 Testing Patterns for Rust Library Crates

The existing conformance test suite (TOML vector-driven, `tests/conformance/`) is the correct pattern for cross-language invariant verification. For diagnostics-specific tests:

#### Unit tests for trait implementations

Use inline `#[cfg(test)]` modules in the same file as the implementation:

```rust
#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn null_collector_accepts_all_events() {
        let mut c = NullTraceCollector;
        c.on_classify(3, 5);
        c.on_exclude(0, ExclusionReason::BudgetExceeded);
        // Just verifying no panic. Zero assertions needed.
    }

    #[test]
    fn diagnostic_collector_counts_deduplicated() {
        let mut c = DiagnosticTraceCollector::new();
        c.on_classify(2, 8);
        c.on_deduplicate(3);
        let report = c.into_report();
        assert_eq!(report.deduplication_removed, 3);
        assert_eq!(report.pinned_count, 2);
    }
}
```

#### Integration tests — `pipeline.dry_run` roundtrip

Add to `tests/` (integration test crate):

```rust
#[test]
fn dry_run_reports_budget_exceeded_exclusions() {
    let pipeline = Pipeline::builder()
        .scorer(Box::new(RecencyScorer))
        .slicer(Box::new(GreedySlice))
        .placer(Box::new(ChronologicalPlacer))
        .build()
        .unwrap();

    let items = vec![...]; // items summing to more than budget
    let budget = ContextBudget::new(100, 50, 0, HashMap::new(), 0.0).unwrap();
    let report = pipeline.dry_run(&items, &budget).unwrap();

    assert!(report.items.iter().any(|i|
        i.exclusion_reason == Some(ExclusionReason::BudgetExceeded)
    ));
}
```

#### Property testing with `proptest`

`proptest` is appropriate for diagnostics testing to verify invariants such as:
- `report.selected_count + excluded_count == scoreable_count + pinned_count`
- `report.budget_used <= budget.target_tokens()`
- Every excluded item has a non-None `exclusion_reason`

**Is `proptest` justified under the zero-dep constraint?** `proptest` is a **dev-dependency only** — it never appears in the compiled library artifact. This is not a dependency constraint violation. Consuming crates never see it. Using it for diagnostic invariant testing is appropriate.

```toml
[dev-dependencies]
proptest = "1"
proptest-derive = "0.5"
```

**Confidence**: HIGH — `proptest` is a stable, high-reputation crate (Medium source reputation in Context7 but widely used by Rust stdlib, rustc, and major crates).

#### Doc tests

Every public type and method in the diagnostics API requires a `# Examples` doc test. Pattern per API Guidelines (C-EXAMPLE, documentation chapter):

```rust
/// Runs the pipeline and returns a full selection report without returning items.
///
/// # Examples
///
/// ```
/// use std::collections::HashMap;
/// use cupel::{Pipeline, ContextItemBuilder, ContextBudget,
///              RecencyScorer, GreedySlice, ChronologicalPlacer};
///
/// let pipeline = Pipeline::builder()
///     .scorer(Box::new(RecencyScorer))
///     .slicer(Box::new(GreedySlice))
///     .placer(Box::new(ChronologicalPlacer))
///     .build()?;
///
/// let items = vec![ContextItemBuilder::new("msg", 10).build()?];
/// let budget = ContextBudget::new(100, 50, 0, HashMap::new(), 0.0)?;
///
/// let report = pipeline.dry_run(&items, &budget)?;
/// assert_eq!(report.selected_count, 1);
/// # Ok::<(), cupel::CupelError>(())
/// ```
```

The `# Ok::<(), cupel::CupelError>(())` tail is required to make the `?` operator work in doc tests (API Guidelines documentation chapter pattern). Lines starting with `#` are compiled but hidden in rendered docs.

---

### 20.10 Quality Tooling — Clippy, cargo-deny, rustdoc

#### Clippy configuration

Per Clippy docs (stable): Use `[lints.clippy]` in `Cargo.toml` with priority control to avoid blanket `clippy::restriction` (which conflicts with idioms):

```toml
[lints.rust]
missing_docs = "warn"
unsafe_code = "deny"

[lints.clippy]
pedantic = { level = "warn", priority = -1 }
# Selective overrides (where pedantic disagrees with this codebase's idioms):
module_name_repetitions = "allow"   # cupel::scorer::RecencyScorer is intentional
missing_errors_doc = "warn"
missing_panics_doc = "warn"
must_use_candidate = "warn"
# Restriction lints worth enabling individually:
unwrap_used = "warn"
expect_used = "warn"                # In library code (not tests)
```

**Why `missing_docs = "warn"`**: The existing codebase has comprehensive doc comments. Enforcing this at the lint level prevents regressions when adding diagnostics types.

**Why `must_use_candidate = "warn"`**: Clippy will flag methods returning `SelectionReport` or `DiagnosticTraceCollector` that are missing `#[must_use]`, catching omissions automatically.

**Why `module_name_repetitions = "allow"`**: `CupelError`, `ContextItem`, `ContextBudget` would all trigger this lint. The existing API surface already uses this naming convention consistently; changing it would be a breaking change. Allow globally.

#### cargo-deny

`cargo-deny` (version: current stable, `embarkstudios/cargo-deny`) is recommended for supply chain hygiene. For a crate with minimal dependencies (only `chrono`, `thiserror`, optional `serde`), the primary value is:

1. **License compliance**: Verify transitive deps remain MIT/Apache-2.0.
2. **Advisories**: Block publication if a dep has a known CVE.
3. **Ban duplicate versions**: Keep the dependency tree minimal.

```toml
# deny.toml
[advisories]
unmaintained = "warn"

[bans]
multiple-versions = "deny"
wildcards = "deny"

[licenses]
confidence-threshold = 0.93
allow = ["MIT", "Apache-2.0", "Apache-2.0 WITH LLVM-exception", "Unicode-3.0"]

[sources]
unknown-registry = "deny"
unknown-git = "deny"
```

`cargo-deny` is a **dev/CI tool** only — it runs in CI, never compiled into the crate. Zero-dep constraint unaffected.

#### rustdoc quality

- `#![cfg_attr(docsrs, feature(doc_auto_cfg))]` — already present in `lib.rs`. This enables automatic feature flag badges on docs.rs for the `serde` and future `diagnostics` feature.
- `#![deny(rustdoc::broken_intra_doc_links)]` — add to `lib.rs` to catch broken `[TypeName]` links at compile time.
- All public diagnostics types must have module-level doc comments explaining the overall system.

---

### 20.11 Feature Flag Strategy for Diagnostics

The diagnostics system itself should be unconditionally available (no feature flag). Rationale:

1. The `TraceCollector` trait and `NullTraceCollector` compile to zero overhead — there is no cost to including them unconditionally.
2. `DiagnosticTraceCollector` and `SelectionReport` are plain data types with no heavy deps — no reason to gate them.
3. Feature flags on diagnostics types create a split API surface where some call sites compile differently than others, complicating docs and user understanding.
4. Contrast with `serde`: serde integration requires pulling in the `serde` crate, which justifies a feature flag. Diagnostics adds no deps.

**Confidence**: HIGH — consistent with the zero-dep constraint and how the existing `serde` feature is justified.

If `SelectionReport` should optionally implement `Serialize`/`Deserialize` (for structured logging by consumers), that serialization impl belongs under the existing `serde` feature gate, not a new `diagnostics` feature.

---

### 20.12 Zero-Dep Constraint Compliance — Decision Table

| Item | Type | Justifies new dep? | Decision |
|---|---|---|---|
| `TraceCollector` trait | Library code | N/A | Pure Rust trait, no dep |
| `NullTraceCollector` | Library code | N/A | Unit struct, no dep |
| `DiagnosticTraceCollector` | Library code | N/A | Uses `std::collections::Vec`, no dep |
| `ExclusionReason` | Library code | N/A | Enum, no dep |
| `SelectionReport` | Library code | N/A | Plain struct, no dep |
| `proptest` | Dev dep only | Yes (dev) | Add to `[dev-dependencies]` |
| `proptest-derive` | Dev dep only | Yes (dev) | Add to `[dev-dependencies]` if `Arbitrary` needed |
| `cargo-deny` | CI tool | N/A | No Cargo dep, install in CI |

The diagnostics system in its entirety adds **zero new runtime dependencies**. This is the strongest possible outcome under the zero-dep constraint.

---

### 20.13 Conformance Vector Extension

The existing conformance vector format (TOML files under `conformance/required/`) should be extended with diagnostics assertions. This enables cross-language verification that the Rust and .NET diagnostics systems report identical outcomes.

Proposed extension to vector schema:

```toml
[expected.diagnostics]
deduplication_removed = 1
selected_count = 3
excluded = [
    { content = "duplicate message", reason = "Deduplicated" },
    { content = "too large doc", reason = "BudgetExceeded" },
]
```

The .NET suite would read the same vector and assert against `SelectionReport`. The Rust suite would assert against `DiagnosticTraceCollector::into_report()`. Discrepancy = cross-language bug.

**This is the primary quality gate for the diagnostics milestone.**

---

### Sources

- [Rust API Guidelines — Checklist](https://rust-lang.github.io/api-guidelines/checklist.html) — C-COMMON-TRAITS, C-SEALED, C-STRUCT-PRIVATE, C-BUILDER, future-proofing
- [Rust Reference — `#[non_exhaustive]`](https://doc.rust-lang.org/reference/attributes/type_system.html#the-non_exhaustive-attribute)
- [Rust Clippy — `[lints.clippy]` configuration](https://rust-lang.github.io/rust-clippy/stable/index.html)
- [cargo-deny — Configuration](https://embarkstudios.github.io/cargo-deny/)
- [proptest — Getting Started](https://altsysrq.github.io/proptest-book/proptest/getting-started.html)
- [proptest-derive — Arbitrary](https://altsysrq.github.io/proptest-book/proptest-derive/getting-started.html)
- [Rust API Guidelines — Documentation](https://rust-lang.github.io/api-guidelines/documentation.html) — doc tests, `# Errors`, `# Panics` sections
