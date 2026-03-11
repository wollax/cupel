# Cupel Pitfalls Research

**Date**: 2026-03-10
**Scope**: .NET pipeline library, scoring system, NuGet package family
**Confidence key**: HIGH = verified against official docs/Context7, MEDIUM = verified via multiple sources, LOW = single source/unverified

---

## 1. API Design Mistakes

### 1.1 Nullability annotation inconsistency [HIGH]

**The pitfall**: Shipping nullable reference type (NRT) annotations that are wrong or incomplete. Once consumers enable `<Nullable>enable</Nullable>` and depend on your annotations, changing a `string` to `string?` or vice versa is a source-breaking change. The dotnet/runtime team explicitly documents that NRT annotation mistakes happen at scale and require careful rollout.

**Warning signs**: Missing `#nullable enable` in some files but not others. Public API returns `null` at runtime but signature says non-nullable. No null-check tests.

**Prevention**:
- Enable `<Nullable>enable</Nullable>` project-wide from day 1 (never file-level)
- Enable `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` so nullable warnings are build failures
- Test that non-nullable properties actually throw/reject null (especially `ContextItem.Content`)
- Use `ArgumentNullException.ThrowIfNull()` on all public API entry points

**Phase**: Phase 1 (project scaffold). Must be set before any public type ships.

Source: [dotnet/runtime nullability guidelines](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/api-guidelines/nullability.md)

---

### 1.2 No public API surface tracking [HIGH]

**The pitfall**: Accidentally shipping breaking changes (removed method, changed signature, changed return type) without detecting them. This is catastrophic for a NuGet library because consumers pin to your API shape.

**Warning signs**: No tooling that catches "you removed a public method" before merge. Reviewers miss subtle signature changes.

**Prevention**:
- Add `Microsoft.CodeAnalysis.PublicApiAnalyzers` from day 1
- Maintain `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt` files
- Treat analyzer warnings as errors in CI
- Alternative/complement: `PublicApiGenerator` for snapshot-based API approval tests

**Phase**: Phase 1 (project scaffold). The analyzer must exist before any public type ships.

Source: [Preventing breaking changes with PublicApiAnalyzers](https://bartwullems.blogspot.com/2025/01/prevent-breaking-changes-using.html), [Andrew Lock: PublicApiGenerator](https://andrewlock.net/preventing-breaking-changes-in-public-apis-with-publicapigenerator/)

---

### 1.3 Leaking implementation types in public API [MEDIUM]

**The pitfall**: Returning concrete types (`List<T>`, `Dictionary<K,V>`) instead of interfaces/read-only types from public API. Once shipped, changing `List<ContextItem>` to `IReadOnlyList<ContextItem>` is a binary-breaking change.

**Warning signs**: Public properties typed as `List<T>`, mutable collections exposed from result types, internal types accidentally `public`.

**Prevention**:
- `ContextResult.Items` should be `IReadOnlyList<ContextItem>`, not `List<ContextItem>`
- All result/output types should expose read-only interfaces
- `ContextTrace` events: `IReadOnlyList<TraceEvent>`, never `List<TraceEvent>`
- Use `[InternalsVisibleTo]` for test projects rather than making internals public
- Consider `sealed` on all public classes unless designed for inheritance

**Phase**: Phase 1 (core model types). Lock down before any consumer sees the API.

---

### 1.4 Missing `[JsonPropertyName]` causes silent wire-format breaks [HIGH]

**The pitfall**: Without explicit `[JsonPropertyName]` attributes, renaming a C# property changes the JSON wire format. Consumers who serialized `CupelPolicy` to a config file now get deserialization failures after upgrading.

**Warning signs**: Public serializable types without `[JsonPropertyName]`. Property renames in PRs that "look safe" but break serialization.

**Prevention**:
- `[JsonPropertyName("snake_case_name")]` on every public property of serializable types from day 1 (already in project requirements)
- Add serialization round-trip tests: serialize, deserialize, assert equality
- Consider a test that snapshots JSON output and fails on unexpected changes

**Phase**: Phase 1 (concurrent with type definition). The PROJECT.md already mandates this.

---

## 2. Performance Pitfalls

### 2.1 LINQ on hot paths [HIGH]

**The pitfall**: LINQ allocates iterator objects, delegate closures, and intermediate collections on every call. For Cupel's pipeline (<1ms target for <500 items), LINQ in the scoring/slicing loop can dominate execution time.

**Warning signs**: `.Where()`, `.Select()`, `.OrderBy()`, `.ToList()` inside scorer or slicer implementations. BenchmarkDotNet shows unexpected Gen0 collections.

**Prevention**:
- Ban LINQ in `IScorer.Score()`, `ISlicer.Slice()`, `IPlacer.Place()` hot paths
- Use `for` loops over arrays/`Span<T>` instead
- `List<T>` with pre-allocated capacity where collection size is known
- Use `ArrayPool<T>.Shared` for temporary buffers in slicers (especially KnapsackSlice)
- Validate with BenchmarkDotNet: assert zero Gen0/Gen1 allocations for pipeline execution with tracing disabled

**Phase**: Phase 1 (implementation). Establish benchmarks early and run them in CI.

Source: [Microsoft Learn: Avoid memory allocations](https://learn.microsoft.com/en-us/dotnet/csharp/advanced-topics/performance/), [endjin: Let's blame LINQ](https://endjin.com/blog/2023/09/optimising-dotnet-code-3-lets-blame-linq)

---

### 2.2 Closure allocations in scoring lambdas [HIGH]

**The pitfall**: When a lambda captures a local variable, the compiler generates a closure class allocated on the heap. In `CompositeScorer` with nested scorers, each scorer invocation that uses a captured weight/config value allocates.

**Warning signs**: Lambdas referencing `this`, local variables, or parameters inside tight loops. ReSharper/Rider DPA flagging closure allocations.

**Prevention**:
- Prefer instance methods over lambdas for scorer implementations
- If lambdas are needed, ensure they don't capture — use static lambdas (`static (x) => ...`) where possible
- For `CompositeScorer` aggregation: store weights in a pre-allocated array, iterate with index
- Profile with `dotnet-counters` or BenchmarkDotNet `[MemoryDiagnoser]`

**Phase**: Phase 1 (scorer implementation). Design `CompositeScorer` internals to avoid closures from the start.

---

### 2.3 Boxing value types [HIGH]

**The pitfall**: Passing `double` scores through `object`-typed parameters, storing `int` tokens in `Dictionary<string, object>` metadata, or calling `.ToString()` on value types without overrides all cause boxing allocations.

**Warning signs**: `object` in any hot-path signature. `Dictionary<string, object>` for metadata. Enum-to-string conversions in trace events.

**Prevention**:
- `ContextItem.Metadata`: use `Dictionary<string, string>` or a typed metadata bag, never `Dictionary<string, object>`
- Score values should flow as `double` through the entire pipeline, never boxed
- If `ExclusionReason` is an enum, cache `.ToString()` values or use a lookup array
- Avoid `string.Format()` / interpolation in trace event construction — use pre-formatted strings or gated construction

**Phase**: Phase 1 (core model + trace design). The `ContextItem.Metadata` type decision is load-bearing.

---

### 2.4 Trace construction when tracing is disabled [HIGH]

**The pitfall**: Constructing trace event objects, formatting strings, or allocating collections even when no trace collector is active. This is the single biggest performance trap for "optional observability" features.

**Warning signs**: Trace events constructed unconditionally then passed to `NullTraceCollector.Add()` which discards them. String interpolation in trace messages regardless of `IsEnabled`.

**Prevention**:
- Gate ALL trace construction behind `ITraceCollector.IsEnabled` check (already in project requirements)
- Pattern: `if (trace.IsEnabled) trace.Add(new ScoreEvent(...));`
- Never: `trace.Add(new ScoreEvent($"Scored {item.Content[..50]}..."));` — the interpolation and substring happen before the null check
- Consider making `NullTraceCollector` a singleton struct to avoid even the interface dispatch overhead
- Benchmark with and without tracing to verify zero-overhead when disabled

**Phase**: Phase 1 (trace infrastructure). The `ITraceCollector` design must enforce this pattern.

---

### 2.5 Async overhead in synchronous pipeline [MEDIUM]

**The pitfall**: Making pipeline stages `async Task<T>` when no I/O occurs. Each `async` method generates a state machine. For a pure CPU-bound pipeline under 1ms, async overhead is significant and unnecessary.

**Warning signs**: `async`/`await` in `IScorer`, `ISlicer`, or `IPlacer` interfaces. `Task<ContextResult>` return type on the main pipeline method when all operations are synchronous.

**Prevention**:
- Core pipeline interfaces (`IScorer.Score`, `ISlicer.Slice`, `IPlacer.Place`) should be synchronous
- `IContextSource` uses `IAsyncEnumerable<ContextItem>` (I/O boundary — async is correct here)
- Pipeline entry point: synchronous `ContextResult Apply(...)`. If async is needed for source materialization, provide a separate `ApplyAsync` that awaits the source then calls the synchronous pipeline
- Use `ValueTask<T>` only if a future version needs conditional async (not in v1)

**Phase**: Phase 1 (interface design). This is an API-shape decision that can't change after v1.

---

## 3. NuGet Multi-Package Publishing Mistakes

### 3.1 Version drift between packages [HIGH]

**The pitfall**: Publishing `Wollax.Cupel` 1.2.0 but `Wollax.Cupel.Extensions.DependencyInjection` 1.1.0 because they were versioned independently. Consumers get confused about compatibility.

**Warning signs**: Different `<Version>` in each `.csproj`. No automation enforcing version alignment. Changelog mentions features that only exist in some packages.

**Prevention**:
- Single version source: `Directory.Build.props` at solution root with `<Version>` property
- All 4 packages share the same version, always
- CI publishes all packages together, never individually
- Use `<IsPackable>true</IsPackable>` selectively but version universally

**Phase**: Phase 1 (project scaffold). Set up `Directory.Build.props` before first package.

---

### 3.2 Dependency version range too wide or too narrow [HIGH]

**The pitfall**: `Wollax.Cupel.Extensions.DependencyInjection` depending on `Microsoft.Extensions.DependencyInjection.Abstractions >= 6.0.0` pulls in a version that's incompatible with the consumer's host. Or pinning to `= 10.0.0` exactly, preventing consumers on 10.0.1.

**Warning signs**: Consumer gets `NU1605` (package downgrade) or `NU1107` (version conflict). Library works in test project but fails when consumed.

**Prevention**:
- Depend on the **Abstractions** package, not the implementation package (`Microsoft.Extensions.DependencyInjection.Abstractions`, not `Microsoft.Extensions.DependencyInjection`)
- Use minimum viable version: `>= 8.0.0` for broad compat, or `>= 10.0.0` if targeting .NET 10 only
- Never use exact version (`=`) or upper bounds (`<`) for dependencies you don't own
- Test consumption from a separate project that references different Microsoft.Extensions versions

**Phase**: Phase 1 (packaging setup). Dependency declarations must be correct from first publish.

Source: [Microsoft Learn: NuGet dependency resolution](https://learn.microsoft.com/en-us/nuget/concepts/dependency-resolution)

---

### 3.3 Core package accidentally gains dependencies [HIGH]

**The pitfall**: Adding a single `using Microsoft.Extensions.Logging;` or `using System.Text.Json;` to the core `Wollax.Cupel` project silently adds a NuGet dependency. The zero-dependency invariant is violated and consumers pulling only core now transitively get packages they didn't ask for.

**Warning signs**: A PR adds a convenience feature to core that "just needs one little package." `dotnet list package` shows unexpected transitive dependencies.

**Prevention**:
- CI check: `dotnet list Wollax.Cupel package` must show zero dependencies (automate this assertion)
- Code review gate: any `<PackageReference>` added to the core `.csproj` requires explicit justification
- Use `#if` / conditional compilation if BCL-only fallbacks exist for optional features
- The Json, DI, and Tiktoken packages exist specifically to keep dependencies out of core

**Phase**: Phase 1 (CI setup). Add the zero-dependency assertion to the CI pipeline immediately.

---

### 3.4 Missing SourceLink / deterministic builds [MEDIUM]

**The pitfall**: Publishing packages without SourceLink means consumers can't step into Cupel source during debugging. Missing deterministic builds means the same source produces different binaries, breaking reproducibility.

**Warning signs**: Consumers see "Source Not Available" in debugger. NuGet package doesn't show "Source Link" badge.

**Prevention**:
- Add to `Directory.Build.props`:
  ```xml
  <Deterministic>true</Deterministic>
  <EmbedUntrackedSources>true</EmbedUntrackedSources>
  <PublishRepositoryUrl>true</PublishRepositoryUrl>
  ```
- Add `Microsoft.SourceLink.GitHub` package to build infrastructure
- Enable `<IncludeSymbols>true</IncludeSymbols>` and `<SymbolPackageFormat>snupkg</SymbolPackageFormat>`
- Publish symbol packages to NuGet.org symbol server

**Phase**: Phase 1 (project scaffold). Must be configured before first NuGet publish.

---

## 4. Pipeline Pattern Anti-Patterns

### 4.1 Call-next middleware silent failure [HIGH]

**The pitfall**: In middleware/chain-of-responsibility patterns, if a stage forgets to call `next()`, the pipeline silently short-circuits. No error, no log, just missing results. This is the primary reason Cupel chose a fixed pipeline.

**Warning signs**: N/A — Cupel already rejected this pattern. Documenting for reference.

**Prevention**:
- Fixed pipeline (Classify -> Score -> Deduplicate -> Slice -> Place) with no call-next semantics (already decided)
- Each stage is a pluggable implementation behind an interface, not a middleware link
- Pipeline orchestrator calls each stage explicitly in order

**Phase**: Already decided. No action needed, but document in architecture docs why middleware was rejected.

---

### 4.2 Pipeline stage ordering assumptions leak [MEDIUM]

**The pitfall**: Even with a fixed pipeline, individual stage implementations may implicitly depend on side effects from previous stages. If a scorer assumes items are already classified, or a slicer assumes items are sorted by score, these invisible contracts cause subtle bugs when implementations are swapped.

**Warning signs**: Slicer implementation that calls `.OrderByDescending(x => x.Score)` because "scores are already sorted" — but that's not guaranteed by the contract. Scorer that reads `item.Kind` assuming classification has run.

**Prevention**:
- Each stage interface contract explicitly documents its input assumptions and output guarantees
- `IScorer` receives `IReadOnlyList<ContextItem>` — must not assume any ordering
- `ISlicer` receives scored items — must sort internally if ordering matters (e.g., GreedySlice sorts by score)
- Pipeline orchestrator is the only code that knows stage order
- Test each stage in isolation with randomized input ordering

**Phase**: Phase 1 (interface design). Document contracts in XML doc comments on interfaces.

---

### 4.3 Mutable items flowing through pipeline [HIGH]

**The pitfall**: If `ContextItem` is mutable and stages modify it in-place, earlier stages' modifications are visible to later stages in unpredictable ways. Worse: if the same item list is reused across pipeline runs, state from a previous run leaks into the next.

**Warning signs**: Scorer sets `item.Score = 0.5` directly on the input item. Items have setters on score/placement properties. Pipeline result items are the same object references as input items.

**Prevention**:
- `ContextItem` should be immutable (or effectively immutable) — all properties `{ get; init; }` or constructor-only
- Pipeline-internal state (score, placement position, exclusion reason) should live in a separate "pipeline context" struct, not on the item itself
- Consider: `ScoredItem` wrapper that pairs `ContextItem` with its pipeline-assigned score, rather than mutating the item
- Result items can share references with input items (no deep copy needed) as long as items are immutable

**Phase**: Phase 1 (core model design). The `ContextItem` mutability decision is the highest-stakes early choice.

---

## 5. Scoring System Design Mistakes

### 5.1 Floating-point comparison in score ranking [HIGH]

**The pitfall**: Using `==` to compare `double` scores, or relying on exact score equality for deduplication/tiebreaking. `0.1 + 0.2 != 0.3` in IEEE 754. A `CompositeScorer` doing weighted average will produce scores that are not exactly reproducible across runs if weight order changes.

**Warning signs**: `scores.Distinct()` to remove duplicate scores. `if (score == 0.0)` checks. Sorting instability causing different results on different runs.

**Prevention**:
- Never compare scores with `==` — use `<`, `>`, `<=`, `>=` for ranking (ordinal comparison is safe)
- Cupel's ordinal-only invariant is a natural defense: scores are used for ranking, not equality
- Document that scores are `double` and subject to floating-point arithmetic rules
- Use stable sort (`OrderBy` is stable in .NET) so items with equal-ish scores maintain insertion order
- If tiebreaking is needed, use a secondary key (timestamp, insertion index), never score equality

**Phase**: Phase 1 (scorer + slicer implementation). The ordinal invariant helps but doesn't eliminate all FP pitfalls.

Source: [Microsoft Learn: Double.Equals](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-double-equals)

---

### 5.2 Weighted average normalization drift [MEDIUM]

**The pitfall**: `CompositeScorer` with weights that don't sum to 1.0 produces scores outside the 0.0-1.0 convention. Weights `[0.7, 0.7, 0.7]` applied to max-value subscorers yield a composite score of 2.1. Slicers that assume 0.0-1.0 range behave incorrectly.

**Warning signs**: Composite scores > 1.0 or < 0.0. Users configure weights intuitively ("importance 7 out of 10") without understanding normalization.

**Prevention**:
- `CompositeScorer` should normalize weights internally: `weight_i / sum(weights)`
- Document that weights are relative, not absolute — `[7, 3]` is the same as `[0.7, 0.3]`
- `ScaledScorer` wrapper exists specifically for scorers that don't produce 0-1 output — document when to use it
- Consider a debug-mode assertion that warns when individual scorer output exceeds 0.0-1.0

**Phase**: Phase 1 (CompositeScorer implementation). Weight normalization is a design decision, not a bug fix.

---

### 5.3 Empty input edge cases [HIGH]

**The pitfall**: Pipeline receives zero items, or all items are pinned, or budget is zero tokens. Scorers divide by zero when normalizing. Slicers return empty lists. Placers receive nothing to place.

**Warning signs**: `NaN` or `Infinity` in scores. `ArgumentException` from empty array operations. Untested edge case that crashes in production.

**Prevention**:
- Every pipeline stage must handle empty input gracefully (return empty output, not throw)
- `RecencyScorer` with 1 item: score should be 1.0 (only item = most recent), not `NaN` from `(index - min) / (max - min)` where `max == min`
- Budget of 0 tokens: pipeline should return empty `ContextResult` with all items excluded (reason: `BudgetExhausted`)
- All-pinned scenario: scorers receive empty list, slicers receive empty list, placer receives only pinned items
- Add explicit unit tests for: 0 items, 1 item, all pinned, zero budget, budget smaller than smallest item

**Phase**: Phase 1 (implementation). Write edge case tests before implementing each stage.

---

### 5.4 Score determinism across runs [MEDIUM]

**The pitfall**: `RecencyScorer` uses `DateTimeOffset.UtcNow` internally, producing different scores for the same items on different runs. `FrequencyScorer` accumulates state. Non-deterministic scoring makes debugging, testing, and explainability impossible.

**Warning signs**: Same input produces different `ContextResult` on consecutive calls. Trace output shows different scores for identical scenarios.

**Prevention**:
- Scorers must be pure functions of their input: `IScorer.Score(IReadOnlyList<ContextItem>, ...)` with no internal mutable state
- `RecencyScorer` should rank by relative timestamp order within the input set, not by absolute time distance from "now"
- If a scorer needs external context (e.g., "current time"), it should be injected via configuration, not captured at score-time
- `DryRun()` must produce identical results to `Apply()` — same scorer, same input, same output

**Phase**: Phase 1 (scorer interface design). Purity is an interface-level contract.

---

## 6. System.Text.Json Serialization Gotchas

### 6.1 Polymorphic serialization requires explicit type discriminators [HIGH]

**The pitfall**: Serializing `IScorer` (which could be `RecencyScorer`, `CompositeScorer`, etc.) through System.Text.Json requires `[JsonDerivedType]` annotations or a custom `JsonTypeInfo` resolver. Without this, deserialization loses the concrete type. This is a security-by-design choice in STJ.

**Warning signs**: Serialized policy JSON shows `{}` for scorer config. Deserialization returns base type with default values. Cross-assembly derived types are silently dropped.

**Prevention**:
- In the `Wollax.Cupel.Json` package (not core), implement a `JsonTypeInfo` resolver for scorer/slicer/placer types
- Use `$type` discriminator with `[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]`
- Register known types explicitly: `[JsonDerivedType(typeof(RecencyScorer), "recency")]`
- The `RegisterScorer(string name, Func<IScorer> factory)` hook in the project design is exactly right — it enables extensible deserialization
- Do NOT put STJ attributes on core types — that would add a dependency. Attributes go in the Json package.

**Phase**: Phase 2+ (Json package). Core types ship with `[JsonPropertyName]` only (BCL attribute, no dependency). Full polymorphic serialization is a companion package concern.

Source: [Microsoft Learn: STJ polymorphism](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism)

---

### 6.2 Source generator limitations with polymorphism [MEDIUM]

**The pitfall**: STJ source generators (for AOT/trimming support) support polymorphism in metadata-based generation but NOT in fast-path generation. If Cupel advertises AOT support, consumers using source-generated serialization hit silent fallback to reflection or outright failure.

**Warning signs**: AOT-published consumer app throws `NotSupportedException` during policy deserialization. Trimmed app works in debug but fails in release.

**Prevention**:
- Document AOT/trimming support status explicitly (likely "not supported in v1" for the Json package)
- If AOT matters: provide a `JsonSerializerContext` subclass in the Json package that consumers can reference
- Test serialization in a trimmed console app as part of CI (even if just a smoke test)
- Consider shipping `[JsonSerializable]` attributes for non-polymorphic types (ContextBudget, CupelPolicy partial)

**Phase**: Phase 2+ (Json package). Not a v1 blocker but must be documented.

Source: [Microsoft Learn: STJ source generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)

---

### 6.3 `init` properties and deserialization [MEDIUM]

**The pitfall**: STJ can deserialize into `init` properties only when using the parameterized constructor or `[JsonConstructor]`. If the constructor parameter names don't match property names (case-insensitive), deserialization silently produces default values.

**Warning signs**: Deserialized `ContextBudget` has `MaxTokens = 0` even though JSON contains `"max_tokens": 4096`. Constructor parameter is named `maxTokens` but `[JsonPropertyName]` says `max_tokens`.

**Prevention**:
- Use `[JsonConstructor]` explicitly on types with `init` properties
- Ensure constructor parameter names match the C# property names (not the JSON names — STJ maps constructor params to properties, then properties to JSON names)
- Add deserialization tests for every serializable type: roundtrip JSON -> object -> JSON -> assert equality

**Phase**: Phase 1 (core model types with `[JsonPropertyName]`). Roundtrip tests catch this early.

---

## 7. Microsoft.Extensions.DI Integration Mistakes

### 7.1 Using wrong namespace for extension methods [HIGH]

**The pitfall**: Putting `AddCupel()` extension method in `Microsoft.Extensions.DependencyInjection` namespace. This works but violates Microsoft's guidance: non-Microsoft packages should NOT use this namespace. It also pollutes IntelliSense for all `IServiceCollection` usages.

**Warning signs**: Extension method appears in every `IServiceCollection` IntelliSense completion regardless of whether Cupel is relevant.

**Prevention**:
- Use `Wollax.Cupel.Extensions.DependencyInjection` as the namespace
- Consumers add a single `using` statement to get the extension methods
- Follow Microsoft's explicit guidance: "DO NOT use the `Microsoft.Extensions.DependencyInjection` namespace for non-official Microsoft packages"

**Phase**: Phase 1 (DI package setup).

Source: [Microsoft Learn: Options pattern for library authors](https://learn.microsoft.com/en-us/dotnet/core/extensions/options-library-authors)

---

### 7.2 Captive dependency (lifetime mismatch) [HIGH]

**The pitfall**: Registering `CupelPipeline` as singleton but it depends on a scoped `ITraceCollector`. The trace collector for one request leaks into subsequent requests. Or registering scorer factories as transient when they should be singleton (stateless scorers don't need per-request instances).

**Warning signs**: Trace data from one pipeline execution appears in another's results. Memory grows unbounded because transient disposables are captured by the singleton container.

**Prevention**:
- `CupelPipeline` / pipeline factory: scoped or transient (not singleton, because trace collector is per-execution)
- Scorers, slicers, placers: singleton if stateless (they should be — purity invariant from 5.4)
- `ITraceCollector`: transient (one per pipeline execution)
- Enable `validateScopes: true` in development
- Provide `AddCupel()` overload patterns per Microsoft guidance: parameterless, `Action<CupelOptions>`, and `IConfiguration` section

**Phase**: Phase 1 (DI package implementation).

Source: [Microsoft Learn: DI guidelines](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/guidelines)

---

### 7.3 Requiring DI for basic usage [HIGH]

**The pitfall**: Making the DI package the only way to construct a pipeline. Consumers who don't use Microsoft.Extensions.DI (console apps, game engines, source generators) can't use Cupel.

**Warning signs**: `CupelPipeline` constructor is `internal` and only accessible via `IServiceProvider`. No way to instantiate without a DI container.

**Prevention**:
- Core `Wollax.Cupel` must be fully usable without DI: `CupelPipeline.CreateBuilder()` fluent API
- DI package is convenience, not requirement
- All types constructable via `new` or builder pattern
- The DI package calls the same public APIs that manual construction uses

**Phase**: Phase 1 (core API design). The fluent builder is the non-DI entry point.

---

## 8. Testing Strategy Pitfalls

### 8.1 Testing internals instead of public API surface [MEDIUM]

**The pitfall**: Heavy use of `[InternalsVisibleTo]` to test every private method. Tests become tightly coupled to implementation. Refactoring internals breaks dozens of tests that test nothing visible to consumers.

**Warning signs**: Test files mirror source file names 1:1 with tests for private/internal methods. `[InternalsVisibleTo]` in every project. Refactoring a single class breaks 30 tests.

**Prevention**:
- Primary test strategy: test through the public API (`CupelPipeline.Apply()` and `ContextResult`)
- Test scorers through `IScorer.Score()`, slicers through `ISlicer.Slice()`, etc.
- `[InternalsVisibleTo]` only for testing genuinely complex internal algorithms (e.g., knapsack solver)
- Integration tests: full pipeline with realistic `ContextItem` sets
- Property-based tests: "pipeline never drops pinned items", "total tokens never exceeds budget", "every included item has a score"

**Phase**: Phase 1 (test infrastructure). Establish testing philosophy before writing tests.

---

### 8.2 No consumption tests [HIGH]

**The pitfall**: All tests run against source projects (`<ProjectReference>`). The published NuGet package has different behavior due to: missing transitive dependencies, incorrect `<PackagePath>` for content files, different assembly loading, or trimmed internals.

**Warning signs**: Tests pass in CI but consumers report `FileNotFoundException` or `MissingMethodException` after installing the package.

**Prevention**:
- Add a "consumption test" project that references Cupel via `<PackageReference>` to a local NuGet feed
- CI pipeline: `dotnet pack` -> add to local feed -> build consumption test project -> run smoke tests
- Test all 4 packages independently and in combination
- Verify the zero-dependency invariant by inspecting the packed `.nupkg`

**Phase**: Phase 1 (CI pipeline). Set up before first NuGet publish.

---

### 8.3 Flaky tests from non-deterministic scoring [MEDIUM]

**The pitfall**: Tests that depend on exact score values (`Assert.Equal(0.75, score)`) break when scoring algorithm is refined. Tests that depend on specific item ordering break when sort stability or tiebreaking changes.

**Warning signs**: Tests intermittently fail. Score assertions use exact `double` equality. Tests break on algorithm improvement PRs even though behavior is correct.

**Prevention**:
- Assert ordinal relationships, not exact values: "item A scores higher than item B"
- Assert structural properties: "top 5 items are included", "pinned items are always included"
- Use epsilon comparison only when exact values genuinely matter (unlikely for ordinal scoring)
- Property-based tests with FsCheck or similar: generate random items, assert invariants hold

**Phase**: Phase 1 (test implementation). Establish assertion patterns in the first scorer tests.

---

### 8.4 Missing benchmark regression tests [MEDIUM]

**The pitfall**: Performance degrades gradually over many PRs. No one notices until a consumer reports the pipeline takes 10ms instead of <1ms.

**Warning signs**: No benchmarks in the repo. "We'll add benchmarks later." Performance requirements exist in docs but aren't enforced.

**Prevention**:
- BenchmarkDotNet project with benchmarks for: full pipeline (500 items), individual stages, with/without tracing
- CI runs benchmarks and stores results (not necessarily as a gate, but as a trend)
- Key metrics: execution time (p50, p99) and allocations (bytes, Gen0 count)
- Consider `[MemoryDiagnoser]` assertions in tests: "pipeline allocates < X bytes for 500 items with tracing disabled"

**Phase**: Phase 1 (implementation). Write benchmarks alongside the first pipeline implementation.

---

## Summary: Phase Assignment

| Phase | Pitfalls to Address |
|-------|-------------------|
| **Phase 1 (Scaffold)** | 1.1 (nullable), 1.2 (API tracking), 3.1 (version source), 3.3 (zero-dep CI check), 3.4 (SourceLink), 7.1 (namespace) |
| **Phase 1 (Core Model)** | 1.3 (read-only types), 1.4 (JsonPropertyName), 4.3 (immutable items), 5.3 (empty input), 6.3 (init + JSON) |
| **Phase 1 (Interface Design)** | 2.5 (sync interfaces), 4.2 (stage contracts), 5.4 (scorer purity), 7.3 (no DI requirement) |
| **Phase 1 (Implementation)** | 2.1 (no LINQ hot path), 2.2 (no closures), 2.3 (no boxing), 2.4 (gated tracing), 5.1 (FP comparison), 5.2 (weight normalization) |
| **Phase 1 (Testing/CI)** | 8.1 (public API tests), 8.2 (consumption tests), 8.3 (ordinal assertions), 8.4 (benchmarks) |
| **Phase 1 (DI Package)** | 7.2 (lifetime correctness) |
| **Phase 1 (Packaging)** | 3.2 (dependency ranges) |
| **Phase 2+ (Json Package)** | 6.1 (polymorphic serialization), 6.2 (source gen / AOT) |

---

*Research completed 2026-03-10. Sources verified against Microsoft Learn official documentation and dotnet/runtime repository.*
