---
estimated_steps: 8
estimated_files: 9
---

# T01: Create package + test project scaffolding; implement StageOnly tier

**Slice:** S05 — OTel Bridge Companion Package
**Milestone:** M003

## Description

Creates the `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet package project and its test project from scratch, establishes the `CupelVerbosity` enum, `CupelActivitySource` static class, and the core `CupelOpenTelemetryTraceCollector` class implementing the StageOnly verbosity tier. After this task, a TUnit test can register an `ActivityListener`, call `pipeline.DryRun(items)`, call `tracer.Complete(result.Report!, budget)`, and assert that a root `"cupel.pipeline"` Activity and 5 child stage Activities exist with correct `cupel.stage.*` attributes.

The package uses ONLY BCL `System.Diagnostics.ActivitySource` and `System.Diagnostics.Activity` (both in .NET 5+ BCL) — no `OpenTelemetry.Api` NuGet dependency. This satisfies D039 (zero-dep core) and avoids adding a new entry to `Directory.Packages.props` for now. The `CupelActivitySource.SourceName` constant is the mechanism by which callers add the source to their OpenTelemetry builder.

## Steps

1. **Create `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` directory and csproj.** Clone the `Wollax.Cupel.Testing.csproj` structure exactly: `IsPackable=true`; `ProjectReference` to `../Wollax.Cupel/Wollax.Cupel.csproj`; `PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" PrivateAssets="All"`; `AdditionalFiles` for both PublicAPI files. Add a `<Description>` element.

2. **Create `PublicAPI.Shipped.txt`** with content `#nullable enable` (single line). Create `PublicAPI.Unshipped.txt` empty (will be populated in T03 after initial build surfaces RS0016 errors).

3. **Create `CupelVerbosity.cs`** in the package directory:
   ```csharp
   namespace Wollax.Cupel.Diagnostics.OpenTelemetry;
   public enum CupelVerbosity { StageOnly = 0, StageAndExclusions = 1, Full = 2 }
   ```

4. **Create `CupelActivitySource.cs`**:
   ```csharp
   using System.Diagnostics;
   namespace Wollax.Cupel.Diagnostics.OpenTelemetry;
   public static class CupelActivitySource
   {
       public const string SourceName = "Wollax.Cupel";
       internal static readonly ActivitySource Source = new(SourceName);
   }
   ```

5. **Create `CupelOpenTelemetryTraceCollector.cs`** — StageOnly implementation:
   - `public sealed class CupelOpenTelemetryTraceCollector : ITraceCollector, IDisposable`
   - Private fields: `_verbosity` (CupelVerbosity), `_stages` (List of `(PipelineStage Stage, TimeSpan Duration, int ItemCountOut)`)
   - `IsEnabled` returns `true`
   - `RecordStageEvent(TraceEvent e)`: appends `(e.Stage, e.Duration, e.ItemCount)` to `_stages`
   - `RecordItemEvent(TraceEvent e)`: no-op (timing-only path)
   - `Complete(SelectionReport? report, ContextBudget budget)`: 
     - Compute `rootEnd = DateTimeOffset.UtcNow`, `totalDuration = sum of stage durations`, `rootStart = rootEnd - totalDuration`
     - Start root Activity: `CupelActivitySource.Source.StartActivity("cupel.pipeline", ActivityKind.Internal, default(ActivityContext), startTime: rootStart)`
     - Set root tags: `cupel.budget.max_tokens = budget.MaxTokens`, `cupel.verbosity = _verbosity.ToString()`
     - Loop through `_stages` building `offset`:
       - `stageStart = rootStart + offset`; `stageName = stage.ToString().ToLowerInvariant()`
       - Start stage Activity with parent = root context, startTime = stageStart
       - Set `cupel.stage.name`, `cupel.stage.item_count_out = stageData.ItemCountOut`
       - Compute `item_count_in`: for Classify (first stage), use `report?.TotalCandidates ?? stageData.ItemCountOut`; for others, use previous stage's `ItemCountOut`
       - Set `cupel.stage.item_count_in`
       - Call `stageActivity.SetEndTime(stageStart + stageData.Duration)` then `stageActivity.Stop()`
       - Advance offset by `stageData.Duration`
     - After loop: `rootActivity.SetEndTime(rootEnd)` then `rootActivity.Stop()`
     - Use null-conditional `?.` on all Activity method calls (StartActivity can return null when no listener)
   - `Dispose()`: calls `CupelActivitySource.Source.Dispose()`

6. **Create test project csproj** at `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/`: `OutputType=Exe`, `IsPackable=false`, `PackageReference Include="TUnit"`, `ProjectReference` to both the new package and `Wollax.Cupel`.

7. **Write `CupelOpenTelemetryTraceCollectorTests.cs`** with a StageOnly test:
   - Register `ActivityListener` with `ShouldListenTo` returning `true` when `source.Name == "Wollax.Cupel"`, `Sample` returning `ActivitySamplingResult.AllData`; collect all stopped Activities in a `List<Activity>`
   - Build a minimal `CupelPipeline` (e.g., `TagScorer` or `ReflexiveScorer`, `GreedySlice`, `LinearPlacer`, small budget)
   - Create `new CupelOpenTelemetryTraceCollector(CupelVerbosity.StageOnly)`, call `pipeline.DryRun(items)`, call `tracer.Complete(result.Report!, budget)`
   - Assert: collected activities contains one named `"cupel.pipeline"`; contains activities named `"cupel.stage.classify"`, `"cupel.stage.score"`, `"cupel.stage.deduplicate"`, `"cupel.stage.slice"`, `"cupel.stage.place"` (exactly 5 stage activities)
   - Assert: classify stage activity has `"cupel.stage.name"` tag = `"classify"`

8. **Update `Cupel.slnx`**: add `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` under the `/src/` folder; add `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` under the `/tests/` folder.

## Must-Haves

- [ ] `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` exists with `IsPackable=true` and correct references
- [ ] `CupelActivitySource.SourceName` constant equals `"Wollax.Cupel"` exactly
- [ ] `CupelOpenTelemetryTraceCollector.IsEnabled` returns `true`
- [ ] `RecordStageEvent` buffers `(PipelineStage, TimeSpan, int)` tuples
- [ ] `Complete()` creates a root Activity named `"cupel.pipeline"` with `cupel.budget.max_tokens` and `cupel.verbosity` tags
- [ ] `Complete()` creates exactly 5 stage Activities with names following `cupel.stage.{lowercase}` pattern
- [ ] All Activity method calls use null-conditional `?.` (StartActivity can return null)
- [ ] Activities are stopped AFTER all tags are set (not before)
- [ ] TUnit test registers `ActivityListener` and asserts root + 5 stage Activities captured
- [ ] Both new projects added to `Cupel.slnx`
- [ ] No OTel NuGet package dependency added

## Verification

- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/` — may show RS0016 (expected, fixed in T03); no CS errors
- `grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` → matches
- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` → StageOnly test passes
- `grep "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj` → no match (core is untouched)

## Observability Impact

- Signals added/changed: `System.Diagnostics.Activity` objects captured by registered `ActivityListener`; `cupel.pipeline` root Activity and 5 `cupel.stage.*` Activities emitted per `Complete()` call
- How a future agent inspects this: `dotnet test -v n` shows test names; TUnit output includes assertion failures with field values; `ActivityListener.ActivityStopped` callback captures all Activities for inspection
- Failure state exposed: if `StartActivity` returns null (no listener registered), all tag-setting calls are no-ops — test catches this because the collected activities list will be empty, causing an assertion failure immediately

## Inputs

- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — template for new package project structure (IsPackable, PublicApiAnalyzers pattern)
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — interface `CupelOpenTelemetryTraceCollector` must implement
- `src/Wollax.Cupel/Diagnostics/TraceEvent.cs` — fields available during `RecordStageEvent`
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — enum values (Classify=0 through Place=4)
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `TotalCandidates` used for Classify `item_count_in`
- S04 Forward Intelligence: `./packages` is the local feed (not `./nupkg`); two-pass PublicAPI workflow

## Expected Output

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — new NuGet package project
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs` — enum with 3 values
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` — static class with `SourceName` constant and internal `ActivitySource`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — StageOnly implementation (~80 lines)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` — `#nullable enable` only
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — empty initially
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — test runner project
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — StageOnly test (1 test)
- `Cupel.slnx` — updated with both new projects
