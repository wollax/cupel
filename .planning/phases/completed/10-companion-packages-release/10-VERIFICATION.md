# Phase 10 Verification — Companion Packages & Release

**Status:** `gaps_found`
**Score:** 8/9 must-haves verified (1 gap, 1 note)
**Date:** 2026-03-14

---

## Must-Have Checks

### 1. DI integration package exists with AddCupel, AddCupelPipeline, AddCupelTracing

**Result: PASS**

`src/Wollax.Cupel.Extensions.DependencyInjection/CupelServiceCollectionExtensions.cs` contains all three methods:
- `AddCupel(IServiceCollection, Action<CupelOptions>)` — registers `IOptions<CupelOptions>` via `services.Configure(configure)`
- `AddCupelPipeline(IServiceCollection, string intent, ContextBudget budget)` — registers keyed transient `CupelPipeline` resolved from named policy
- `AddCupelTracing(IServiceCollection)` — registers transient `ITraceCollector` as `DiagnosticTraceCollector` via `TryAddTransient`

`IOptions<CupelOptions>` is accessible via `provider.GetRequiredService<IOptions<CupelOptions>>()` (verified by DI tests).

**Lifetime gap (sub-criterion from ROADMAP):** The ROADMAP success criterion mentions "singleton scorers/slicers/placers". The implementation does NOT register scorers/slicers/placers in the DI container at all — they are instantiated inside `CupelPipeline.CreateBuilder().WithPolicy(policy).Build()`. This means they are owned by the pipeline instance (created per-resolve since the pipeline is transient), not registered as singletons. The architectural choice is defensible (stateless scorers/slicers/placers are cheap to create), but it technically diverges from the stated criterion.

---

### 2. Tiktoken bridge package with TiktokenTokenCounter (CreateForModel, CountTokens, WithTokenCount)

**Result: PASS**

`src/Wollax.Cupel.Tiktoken/TiktokenTokenCounter.cs` contains:
- `CreateForModel(string modelName)` — delegates to `TiktokenTokenizer.CreateForModel`
- `CreateForEncoding(string encodingName)` — delegates to `TiktokenTokenizer.CreateForEncoding`
- `CountTokens(string text)` — int return
- `CountTokens(ReadOnlySpan<char> text)` — int return
- `WithTokenCount(ContextItem item)` — returns `item with { Tokens = CountTokens(item.Content) }`

Targets `net10.0`, uses `Microsoft.ML.Tokenizers` version 1.0.1. All methods present in `PublicAPI.Shipped.txt`.

---

### 3. CI workflow runs build+test on push/PR

**Result: PASS**

`.github/workflows/ci.yml` triggers on `push` and `pull_request` to `main`. Runs:
1. `dotnet build --configuration Release`
2. `dotnet test --configuration Release --no-build`

Uses `dotnet-version: '10.x'` and `fetch-depth: 0` (required for MinVer tag resolution).

---

### 4. Release workflow is manual dispatch, packs, runs consumption tests, publishes via OIDC

**Result: PASS**

`.github/workflows/release.yml`:
- Triggered by `workflow_dispatch` with optional `dry-run` boolean input
- Job `pack`: builds → tests → packs → uploads artifact
- Job `consumption-tests`: downloads nupkg artifact → copies to local source dir → runs `dotnet test tests/Wollax.Cupel.ConsumptionTests/`
- Job `publish`: only runs if `dry-run != true`; uses `NuGet/login@v1` for OIDC-based Trusted Publishing; pushes with `--skip-duplicate`; creates GitHub Release via `softprops/action-gh-release@v2`

`permissions: id-token: write` is set at workflow level for OIDC.

---

### 5. Consumption test project uses PackageReference (not ProjectReference) for all 4 packages

**Result: PASS**

`tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj` uses `PackageReference` with `Version="*-*"` for all four packages:
- `Wollax.Cupel`
- `Wollax.Cupel.Json`
- `Wollax.Cupel.Extensions.DependencyInjection`
- `Wollax.Cupel.Tiktoken`

`nuget.config` clears default sources and adds `./packages` (local) + `nuget.org`. This ensures CI consumption tests resolve from packed `.nupkg` files, not project references.

**Note:** The `ConsumptionTests` project is intentionally excluded from `Cupel.slnx`. It is only invoked directly in the release workflow (`dotnet test tests/Wollax.Cupel.ConsumptionTests/`), not as part of the regular solution build/test. This is correct by design.

---

### 6. All PublicAPI.Shipped.txt files have entries; all PublicAPI.Unshipped.txt files are empty

**Result: PASS**

| Package | Shipped.txt | Unshipped.txt |
|---------|-------------|---------------|
| `Wollax.Cupel` | 302-line full API surface | `#nullable enable` only |
| `Wollax.Cupel.Json` | 19 entries (CupelJsonSerializer, CupelJsonOptions) | `#nullable enable` only |
| `Wollax.Cupel.Extensions.DependencyInjection` | 5 entries (class + 3 methods) | `#nullable enable` only |
| `Wollax.Cupel.Tiktoken` | 7 entries (class + 5 members) | `#nullable enable` only |

All four packages have finalized Shipped.txt and empty Unshipped.txt — no pending API changes.

---

### 7. Solution builds with zero warnings

**Result: PASS**

```
rtk dotnet build --configuration Release
→ ok (build succeeded)
```

`TreatWarningsAsErrors` is set in `Directory.Build.props`, so any warning would fail the build. Zero warnings confirmed.

---

### 8. All tests pass

**Result: PASS**

```
rtk dotnet test --configuration Release --no-build
→ total: 589 | failed: 0 | succeeded: 589 | skipped: 0
```

All four test assemblies pass:
- `Wollax.Cupel.Tests` (295ms)
- `Wollax.Cupel.Json.Tests` (218ms)
- `Wollax.Cupel.Extensions.DependencyInjection.Tests` (189ms)
- `Wollax.Cupel.Tiktoken.Tests` (530ms)

---

### 9. MinVer is configured as GlobalPackageReference in Directory.Packages.props

**Result: PASS**

`Directory.Packages.props` contains:
```xml
<GlobalPackageReference Include="MinVer" Version="7.0.0" PrivateAssets="All" />
```

`MinVer` is applied to all projects globally. `SourceLink.GitHub` is also present in `Directory.Build.props` as a `PackageReference` with `PrivateAssets="All"`. Both CI and release workflows use `fetch-depth: 0` to ensure full tag history is available for MinVer version computation.

---

## Summary

| # | Must-Have | Status |
|---|-----------|--------|
| 1 | DI package: AddCupel/AddCupelPipeline/AddCupelTracing + IOptions | PASS (with sub-criterion gap — see below) |
| 2 | Tiktoken: TiktokenTokenCounter with CreateForModel/CountTokens/WithTokenCount | PASS |
| 3 | CI workflow: build+test on push/PR | PASS |
| 4 | Release workflow: manual dispatch, pack, consumption tests, OIDC publish | PASS |
| 5 | Consumption tests use PackageReference for all 4 packages | PASS |
| 6 | All PublicAPI.Shipped.txt finalized, Unshipped.txt empty | PASS |
| 7 | Zero-warning Release build | PASS |
| 8 | All 589 tests pass | PASS |
| 9 | MinVer GlobalPackageReference in Directory.Packages.props | PASS |

## Gap: Singleton Scorers/Slicers/Placers Not Registered in DI

The ROADMAP success criterion 1 states "correct lifetimes (transient pipeline/trace, **singleton scorers/slicers/placers**)". The actual implementation does not register scorers, slicers, or placers in the DI container individually — they are constructed internally by the pipeline builder from the `CupelPolicy`. As a result, each transient `CupelPipeline` resolve creates new scorer/slicer/placer instances. Since these are stateless, the behavior is correct, but the DI lifetime model diverges from what the ROADMAP specified.

**Action needed:** Decide whether this is acceptable (architectural simplification) or whether scorers/slicers/placers should be registered as singletons and injected into the pipeline builder. If accepted as-is, update the ROADMAP success criterion to reflect the actual design.
