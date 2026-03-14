# Phase 10: Companion Packages & Release - Research

**Researched:** 2026-03-14
**Domain:** .NET DI integration, tokenizer bridging, NuGet publishing, CI/CD
**Confidence:** HIGH

## Summary

Phase 10 delivers four work streams: (1) a DI integration package wrapping CupelOptions/CupelPipeline into Microsoft.Extensions.DependencyInjection, (2) a Tiktoken bridge package using Microsoft.ML.Tokenizers, (3) consumption tests against packed .nupkg files, and (4) CI/CD workflows (PR build+test, manual-dispatch publish with OIDC trusted publishing).

The project already uses MinVer for versioning, Central Package Management, SourceLink, and has a stub `release.yml` workflow. The existing `CupelOptions` class is a natural fit for `IOptions<T>` binding. Keyed services (.NET 8+) map cleanly to named policies. Microsoft.ML.Tokenizers v1.0.1 (stable) provides `TiktokenTokenizer.CreateForModel()` and `CountTokens()` -- exactly what the bridge needs.

**Primary recommendation:** Use the established .NET DI extension patterns (AddCupel overloads with Action<CupelOptions>, IOptions<CupelOptions>, keyed CupelPipeline per named policy) and Microsoft.ML.Tokenizers v1.0.1 stable. Leverage NuGet Trusted Publishing via `NuGet/login@v1` action for OIDC-based publish without stored API keys.

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.x | DI registration surface (`IServiceCollection`) | Only abstractions package needed; avoids pulling full container |
| Microsoft.Extensions.Options | 10.0.x | `IOptions<CupelOptions>` / `IOptionsSnapshot<T>` binding | Standard pattern for .NET library DI registration |
| Microsoft.ML.Tokenizers | 1.0.1 | Tiktoken/BPE tokenization (CountTokens, EncodeToIds) | Official Microsoft tokenizer; replaces SharpToken/DeepDev |
| Microsoft.ML.Tokenizers.Data.O200kBase | 1.0.0 | Embedded BPE data for o200k_base encoding (GPT-4o+) | Required data package for offline tiktoken model support |
| Microsoft.ML.Tokenizers.Data.Cl100kBase | 1.0.2 | Embedded BPE data for cl100k_base encoding (GPT-4) | Required data package for offline tiktoken model support |
| MinVer | 7.0.0 | Git-tag-based versioning | Already configured globally in Directory.Packages.props |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| NuGet/login@v1 | latest | GitHub Action for OIDC -> NuGet temp API key | In publish workflow only |
| actions/setup-dotnet@v4 | latest | .NET SDK setup in CI | All workflows |
| softprops/action-gh-release@v2 | latest | Create GitHub release with tag | After NuGet publish succeeds |
| TUnit | 1.19.22 | Test framework (already in use) | Consumption tests |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|-----------|-----------|----------|
| Microsoft.ML.Tokenizers | SharpToken | SharpToken is unmaintained; MS.ML.Tokenizers is official |
| MinVer | Nerdbank.GitVersioning | MinVer already configured; simpler tag-based model |
| Keyed services | String dictionary in CupelOptions | Keyed services are the .NET standard; already have policy registry |
| NuGet Trusted Publishing | NUGET_API_KEY secret | OIDC eliminates long-lived secrets; industry best practice |

**Installation (new packages for Directory.Packages.props):**
```xml
<PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0" />
<PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.0" />
<PackageVersion Include="Microsoft.ML.Tokenizers" Version="1.0.1" />
<PackageVersion Include="Microsoft.ML.Tokenizers.Data.O200kBase" Version="1.0.0" />
<PackageVersion Include="Microsoft.ML.Tokenizers.Data.Cl100kBase" Version="1.0.2" />
```

## Architecture Patterns

### Recommended Project Structure
```
src/
├── Wollax.Cupel/                                          # Core (exists)
├── Wollax.Cupel.Json/                                     # JSON serialization (exists)
├── Wollax.Cupel.Extensions.DependencyInjection/           # NEW: DI integration
│   ├── CupelServiceCollectionExtensions.cs                # AddCupel() overloads
│   ├── PublicAPI.Shipped.txt
│   ├── PublicAPI.Unshipped.txt
│   └── Wollax.Cupel.Extensions.DependencyInjection.csproj
├── Wollax.Cupel.Tiktoken/                                 # NEW: Tokenizer bridge
│   ├── TiktokenTokenCounter.cs                            # ITokenCounter adapter
│   ├── TiktokenServiceCollectionExtensions.cs             # Optional DI extension
│   ├── PublicAPI.Shipped.txt
│   ├── PublicAPI.Unshipped.txt
│   └── Wollax.Cupel.Tiktoken.csproj
tests/
├── Wollax.Cupel.Extensions.DependencyInjection.Tests/     # NEW: DI tests
├── Wollax.Cupel.Tiktoken.Tests/                           # NEW: Tiktoken tests
└── Wollax.Cupel.ConsumptionTests/                         # NEW: nupkg consumption
    ├── nuget.config                                       # Points to local ./packages/ source
    └── Wollax.Cupel.ConsumptionTests.csproj               # PackageReference (not ProjectReference)
.github/
└── workflows/
    ├── ci.yml                                             # NEW: PR build+test
    └── release.yml                                        # REWRITE: manual dispatch publish
```

### Pattern 1: AddCupel() DI Registration

**What:** Extension methods on IServiceCollection following .NET conventions.
**When to use:** Any host-based .NET application wanting DI-managed pipelines.

```csharp
// Source: .NET DI conventions + existing CupelOptions API
public static class CupelServiceCollectionExtensions
{
    // Basic: configure options via delegate
    public static IServiceCollection AddCupel(
        this IServiceCollection services,
        Action<CupelOptions> configure)
    {
        services.Configure(configure);
        services.TryAddSingleton<IOptions<CupelOptions>>();
        return services;
    }

    // Named pipeline: register keyed CupelPipeline per intent
    public static IServiceCollection AddCupelPipeline(
        this IServiceCollection services,
        string intent,
        ContextBudget budget)
    {
        services.AddKeyedTransient<CupelPipeline>(intent, (sp, key) =>
        {
            var options = sp.GetRequiredService<IOptions<CupelOptions>>().Value;
            var policy = options.GetPolicy((string)key!);
            return CupelPipeline.CreateBuilder()
                .WithPolicy(policy)
                .WithBudget(budget)
                .Build();
        });
        return services;
    }
}
```

**Lifetime conventions:**
- `CupelOptions` → Singleton (via IOptions<T>, which is singleton by default)
- `CupelPipeline` → Transient (cheap to create, contains no mutable state worth caching)
- `ITraceCollector` (DiagnosticTraceCollector) → Transient (per-request, accumulates state)
- Individual scorers/slicers/placers → effectively singleton (created inside pipeline, but pipeline is transient -- scorers are stateless so this is fine)

### Pattern 2: Tiktoken Bridge

**What:** An adapter that wraps Microsoft.ML.Tokenizers.TiktokenTokenizer to count tokens for ContextItem.Content.
**When to use:** When consumers want accurate token counts instead of estimating.

```csharp
// Source: Microsoft.ML.Tokenizers official docs
using Microsoft.ML.Tokenizers;

public sealed class TiktokenTokenCounter
{
    private readonly Tokenizer _tokenizer;

    private TiktokenTokenCounter(Tokenizer tokenizer)
    {
        _tokenizer = tokenizer;
    }

    public static TiktokenTokenCounter CreateForModel(string modelName)
        => new(TiktokenTokenizer.CreateForModel(modelName));

    public static TiktokenTokenCounter CreateForEncoding(string encodingName)
        => new(TiktokenTokenizer.CreateForEncoding(encodingName));

    public int CountTokens(string text)
        => _tokenizer.CountTokens(text);

    // Extension: count tokens for a ContextItem, returning a new item with Tokens set
    public ContextItem WithTokenCount(ContextItem item)
        => item with { Tokens = CountTokens(item.Content) };
}
```

### Pattern 3: Consumption Tests Against .nupkg

**What:** Tests that install packed NuGet packages from a local source, not ProjectReference.
**When to use:** Verifying the package content, dependencies, and API surface ship correctly.

```xml
<!-- nuget.config for consumption tests -->
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="../../artifacts/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

```xml
<!-- ConsumptionTests.csproj: PackageReference not ProjectReference -->
<ItemGroup>
  <PackageReference Include="Wollax.Cupel" Version="*" />
  <PackageReference Include="Wollax.Cupel.Json" Version="*" />
  <PackageReference Include="Wollax.Cupel.Extensions.DependencyInjection" Version="*" />
  <PackageReference Include="Wollax.Cupel.Tiktoken" Version="*" />
</ItemGroup>
```

Build flow: `dotnet pack --output artifacts/packages` → `dotnet test tests/Wollax.Cupel.ConsumptionTests/`

### Pattern 4: NuGet Trusted Publishing via OIDC

**What:** Keyless NuGet publishing using GitHub Actions OIDC tokens.
**When to use:** The publish workflow.

```yaml
# Source: https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing
jobs:
  publish:
    runs-on: ubuntu-latest
    permissions:
      id-token: write   # Required for OIDC
      contents: write   # Required for GitHub Release
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0  # Required for MinVer

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.x'

      - run: dotnet pack --configuration Release --output ./nupkg

      - name: NuGet login (OIDC)
        uses: NuGet/login@v1
        id: nuget-login
        with:
          user: wollax

      - run: dotnet nuget push ./nupkg/*.nupkg --api-key ${{ steps.nuget-login.outputs.NUGET_API_KEY }} --source https://api.nuget.org/v3/index.json --skip-duplicate
```

### Anti-Patterns to Avoid
- **Registering CupelPipeline as Singleton:** Pipeline holds ITraceCollector state if tracing is enabled. Must be transient.
- **Taking a dependency on the full DI container package:** Use `Microsoft.Extensions.DependencyInjection.Abstractions` only, not the concrete container.
- **Embedding tokenizer data in the bridge package:** Let consumers reference the specific `Microsoft.ML.Tokenizers.Data.*` package for their model. The bridge should only depend on `Microsoft.ML.Tokenizers`.
- **Using ProjectReference in consumption tests:** Defeats the purpose. Must use PackageReference against packed .nupkg files.
- **Storing NUGET_API_KEY as a repository secret:** Use Trusted Publishing OIDC instead for short-lived, single-use keys.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Token counting | Custom BPE implementation | Microsoft.ML.Tokenizers | Battle-tested, Microsoft-maintained, exact parity with OpenAI tiktoken |
| Git-based versioning | Manual version in .csproj | MinVer (already configured) | Automatic from git tags, deterministic, CI-friendly |
| DI Options binding | Custom configuration class | IOptions<CupelOptions> pattern | Standard .NET; works with IConfiguration, validation, etc. |
| Package validation | Manual API surface checks | PublicApiAnalyzers + dotnet-validate | Automated, catches accidental API changes |
| Keyed service resolution | Custom service locator | .NET 8+ AddKeyed* methods | Built into the framework since .NET 8 |

**Key insight:** The DI and tokenizer packages are thin wrappers over well-established .NET patterns. The value is in correct lifetime management and clean API design, not novel implementation.

## Common Pitfalls

### Pitfall 1: MinVer Requires Full Git History in CI
**What goes wrong:** MinVer calculates version from git tags. Shallow clones (`fetch-depth: 1`) produce `0.0.0-alpha.0`.
**Why it happens:** GitHub Actions defaults to `fetch-depth: 1`.
**How to avoid:** Use `fetch-depth: 0` in checkout step.
**Warning signs:** All packages versioned as 0.0.0-alpha.0.

### Pitfall 2: Consumption Tests Resolve Wrong Package Version
**What goes wrong:** NuGet restore picks cached or nuget.org versions instead of locally packed ones.
**Why it happens:** Default NuGet sources include nuget.org, which may have older versions.
**How to avoid:** Use `<clear />` in consumption test nuget.config, then add local source first. Use floating version `*` or exact version from pack output.
**Warning signs:** Tests pass but test different code than what was packed.

### Pitfall 3: Keyed Services Require .NET 8+
**What goes wrong:** `AddKeyedSingleton`/`AddKeyedTransient` don't exist on older TFMs.
**Why it happens:** Keyed services were added in .NET 8.
**How to avoid:** Target net10.0 only (project already does). If multi-targeting is needed later, conditionally compile keyed service support.
**Warning signs:** Compilation errors on older TFMs.

### Pitfall 4: TiktokenTokenizer.CreateForModel Downloads Data
**What goes wrong:** `CreateForModel("gpt-4o")` downloads BPE data from the internet on first call.
**Why it happens:** Without a data package reference, the tokenizer fetches vocab files remotely.
**How to avoid:** Reference `Microsoft.ML.Tokenizers.Data.O200kBase` or `Cl100kBase` data packages. These embed the BPE data, making creation offline and fast.
**Warning signs:** Network calls during tests, CI failures behind firewalls.

### Pitfall 5: PublicAPI.Shipped.txt Must Be Populated Before Tagging v1.0
**What goes wrong:** PublicAPI.Unshipped.txt has all APIs; Shipped.txt is empty. After v1.0, any API change shows as "added" not "moved from unshipped".
**Why it happens:** Nobody moves APIs from Unshipped to Shipped during development.
**How to avoid:** Before the v1.0 tag, move all Unshipped entries to Shipped for all four packages. This is the "freeze" step.
**Warning signs:** RS0016 warnings after tagging, no baseline for breaking change detection.

### Pitfall 6: NuGet Trusted Publishing Policy Must Match Exact Workflow File
**What goes wrong:** OIDC token exchange fails with "no matching policy" error.
**Why it happens:** Policy on nuget.org specifies exact workflow filename (e.g., `release.yml`). If you rename the file, the policy breaks.
**How to avoid:** Set up the policy on nuget.org matching the exact repo owner, repo name, and workflow filename.
**Warning signs:** 401/403 from NuGet push despite correct workflow setup.

### Pitfall 7: Private Repo Trusted Publishing Has 7-Day Activation Window
**What goes wrong:** Policy becomes inactive if no publish happens within 7 days of creation.
**Why it happens:** NuGet needs the first publish to capture GitHub repo/owner IDs for resurrection attack prevention.
**How to avoid:** Publish within 7 days of creating the policy. If it lapses, restart the activation window from the nuget.org UI.
**Warning signs:** Previously working policy suddenly rejects publishes.

## Code Examples

### DI Registration (Consumer-Facing API)

```csharp
// Source: .NET conventions + Cupel API
services.AddCupel(options =>
{
    options.AddPolicy("chat", CupelPresets.Chat());
    options.AddPolicy("rag", CupelPresets.Rag());
});

// Resolve a named pipeline
var pipeline = serviceProvider.GetRequiredKeyedService<CupelPipeline>("chat");
var result = pipeline.Execute(items);
```

### Tiktoken Token Counting

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/ai/how-to/use-tokenizers
var counter = TiktokenTokenCounter.CreateForModel("gpt-4o");

var items = rawItems.Select(item => counter.WithTokenCount(item)).ToList();
// Now each item has accurate Tokens value
```

### Publish Workflow (workflow_dispatch)

```yaml
# Source: NuGet Trusted Publishing docs
name: Publish to NuGet
on:
  workflow_dispatch:
    inputs:
      dry-run:
        description: 'Dry run (skip NuGet push)'
        required: false
        default: 'false'
        type: boolean
```

### Consumption Test Structure

```csharp
// Source: .NET testing patterns
[Test]
public void CanCreatePipelineFromNuGetPackage()
{
    // This test only compiles if PackageReference to Wollax.Cupel resolves
    var pipeline = CupelPipeline.CreateBuilder()
        .WithPolicy(CupelPresets.Chat())
        .WithBudget(new ContextBudget(maxTokens: 4000, targetTokens: 3500))
        .Build();

    Assert.That(pipeline, Is.Not.Null);
}

[Test]
public void CanSerializePolicyFromJsonPackage()
{
    var json = CupelJsonSerializer.Serialize(CupelPresets.Chat());
    var roundTripped = CupelJsonSerializer.Deserialize(json);
    Assert.That(roundTripped.Scorers.Count, Is.EqualTo(CupelPresets.Chat().Scorers.Count));
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|-------------|-----------------|--------------|--------|
| NUGET_API_KEY secrets | Trusted Publishing OIDC | 2024-2025 | No stored secrets; short-lived tokens |
| SharpToken / DeepDev.TokenizerLib | Microsoft.ML.Tokenizers | 2024 (v1.0 stable) | Official Microsoft library; better perf |
| Manual version in .csproj | MinVer from git tags | Already configured | Automatic versioning from tags |
| IServiceProvider.GetService by type | Keyed services (.NET 8+) | .NET 8 (Nov 2023) | Built-in support for named registrations |
| TiktokenTokenizer downloads BPE data | Data packages embed BPE vocab | ML.Tokenizers 1.0 | Offline support, faster init |

**Deprecated/outdated:**
- `SharpToken`: Unmaintained; migrate to Microsoft.ML.Tokenizers
- `DeepDev.TokenizerLib`: Unmaintained; official migration guide exists
- Long-lived NuGet API keys: Replaced by OIDC Trusted Publishing

## Open Questions

1. **Multi-target net8.0+net10.0 for DI package?**
   - What we know: Project currently targets net10.0 only. Keyed services require .NET 8+. The DI abstractions package supports netstandard2.0.
   - What's unclear: Whether consumers on .NET 8/9 are a target audience for v1.0.
   - Recommendation: Start with net10.0 only (matching existing project convention). Multi-targeting can be added in a patch release if demand exists.

2. **Should Tiktoken bridge depend on DI package or be standalone?**
   - What we know: The bridge is useful without DI (direct instantiation). DI integration is a convenience.
   - What's unclear: Whether to ship AddTiktoken() in the Tiktoken package or require the DI package.
   - Recommendation: Make the Tiktoken package standalone (no DI dependency). Provide optional `AddTiktokenTokenCounter()` extension method that depends on the DI abstractions package (same dependency the DI package already takes). This keeps the package graph clean.

3. **Which tokenizer data packages to recommend/depend on?**
   - What we know: `O200kBase` covers GPT-4o/GPT-5. `Cl100kBase` covers GPT-4/GPT-3.5. Users may need other encodings.
   - What's unclear: Whether to take a hard dependency on data packages or leave it to consumers.
   - Recommendation: Do NOT depend on data packages from the Tiktoken bridge. Let consumers add the data package for their model. Document required data packages clearly.

4. **Consumption tests in publish workflow vs dedicated CI step?**
   - What we know: CONTEXT.md says "publish workflow only" for consumption tests.
   - Recommendation: Run consumption tests as a job in the publish workflow, after pack but before push. This validates the exact artifacts being published.

## Sources

### Primary (HIGH confidence)
- Microsoft.ML.Tokenizers official docs: https://learn.microsoft.com/en-us/dotnet/ai/how-to/use-tokenizers (updated 2026-01-29)
- TiktokenTokenizer API reference: https://learn.microsoft.com/en-us/dotnet/api/microsoft.ml.tokenizers.tiktokentokenizer
- NuGet Trusted Publishing: https://learn.microsoft.com/en-us/nuget/nuget-org/trusted-publishing (updated 2026-02-02)
- .NET DI Keyed Services: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/dependency-injection (official docs)
- Existing codebase: Directory.Build.props, Directory.Packages.props, release.yml, CupelOptions.cs, CupelPipeline.cs, PipelineBuilder.cs

### Secondary (MEDIUM confidence)
- Andrew Lock blog on Trusted Publishing: https://andrewlock.net/easily-publishing-nuget-packages-from-github-actions-with-trusted-publishing/
- Andrew Lock blog on keyed services: https://andrewlock.net/exploring-the-dotnet-8-preview-keyed-services-dependency-injection-support/

### Tertiary (LOW confidence)
- Consumption test patterns: Assembled from multiple blog posts and community practices; no single authoritative source.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - Official Microsoft libraries with stable releases, verified via official docs
- Architecture (DI): HIGH - Standard .NET patterns, verified existing CupelOptions API compatibility
- Architecture (Tiktoken): HIGH - Microsoft.ML.Tokenizers v1.0.1 API verified via official docs
- Architecture (CI/CD): HIGH - NuGet Trusted Publishing verified via official Microsoft docs (updated Feb 2026)
- Pitfalls: MEDIUM - Mix of documented issues (MinVer, OIDC) and experience-based patterns

**Research date:** 2026-03-14
**Valid until:** 2026-04-14 (stable libraries; 30-day validity)
