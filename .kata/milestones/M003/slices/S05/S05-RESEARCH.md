# S05 — OTel Bridge Companion Package — Research

**Date:** 2026-03-23

## Summary

S05 ships `Wollax.Cupel.Diagnostics.OpenTelemetry` — a companion NuGet package that bridges the
existing `ITraceCollector` abstraction to .NET `System.Diagnostics.ActivitySource`. The package
must produce a 6-Activity tree (1 root + 5 stage Activities) with `cupel.*` attributes at three
verbosity tiers (StageOnly, StageAndExclusions, Full), verified by a TUnit test harness.

The project wiring pattern is now fully established from S04 (Wollax.Cupel.Testing). The primary
risk from the milestone roadmap — "new NuGet package project structure" — is retired. The
remaining risks are **architectural**: bridging `ITraceCollector` to ActivitySource has a
structural impedance mismatch that must be resolved carefully.

The critical constraint: the pipeline creates a `SelectionReport` (which carries per-item
`ExcludedItem`/`IncludedItem` data needed for StageAndExclusions/Full) **only** when it detects
`trace is DiagnosticTraceCollector`. `CupelOpenTelemetryTraceCollector` cannot inherit from
`DiagnosticTraceCollector` (sealed). Therefore the bridge must use a two-phase design: buffer
stage-level timing during Execute(), then complete the trace using a `SelectionReport` from a
separate `DryRun()` call OR by having the test harness pass the report explicitly.

**Recommended design**: `CupelOpenTelemetryTraceCollector implements ITraceCollector, IDisposable`.
It buffers stage-level data via RecordStageEvent. After execution, the caller calls
`Complete(SelectionReport?, ContextBudget)` which creates all Activities retroactively with
correct start/end timestamps derived from buffered durations. For StageOnly, `Complete()` works
without a report. For StageAndExclusions/Full, the test harness uses `DryRun()` to guarantee a
non-null report.

## Recommendation

**Two-phase bridge + explicit `Complete()` call** is the only design that satisfies all constraints:
1. `CupelOpenTelemetryTraceCollector` implements `ITraceCollector` (required by roadmap boundary map)
2. BufferStageData during Execute/RecordStageEvent (pipeline calls this with timing info)
3. `Complete(SelectionReport?, ContextBudget)` creates all Activities from buffered data
4. Activities are created retroactively using `ActivitySource.StartActivity(startTime: ...)` and
   immediately stopped with correct end time — real OTel Activities, not synthetic

For the test harness: use `pipeline.DryRun(items)` which always returns `ContextResult.Report` 
(non-null), then call `tracer.Complete(result.Report, budget)`.

`AddCupelInstrumentation(TracerProviderBuilder)` simply calls `builder.AddSource("Wollax.Cupel")`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| NuGet package project structure | Clone `Wollax.Cupel.Testing.csproj` exactly | IsPackable, PublicApiAnalyzers, SourceLink all inherit from Directory.Build.props — proven in S04 |
| PublicAPI.Unshipped.txt population | Two-pass workflow: initial build → RS0016 errors → populate | Established D077 pattern; BuildApiAnalyzers enforces it with TreatWarningsAsErrors |
| Local feed wiring for consumption tests | Copy `.nupkg` → `./packages` (not `./nupkg`) per D095 | `nuget.config` declares `./packages` as source; wrong path = silent NuGet resolution failure |
| ActivitySource creation | `new ActivitySource("Wollax.Cupel")` as a static field | ActivitySource must use the exact name from spec; callers call `AddSource("Wollax.Cupel")` |
| Activity retroactive timing | `ActivitySource.StartActivity(name, kind, parentId, tags: null, links: null, startTime: startTime)` overload | Correct OTel pattern for creating spans with known timing; immediately call `SetEndTime()` and `Stop()` |

## Existing Code and Patterns

- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — The interface the bridge must implement; `IsEnabled` must return `true`; has `RecordStageEvent(TraceEvent)` and `RecordItemEvent(TraceEvent)`; TraceEvent carries Stage+Duration+ItemCount+Message ONLY (no ExclusionReason, no ContextItem)
- `src/Wollax.Cupel/Diagnostics/TraceEvent.cs` — `readonly record struct`; Stage=PipelineStage, Duration=TimeSpan, ItemCount=int, Message=string?; this is the ONLY data ITraceCollector gets during execution
- `src/Wollax.Cupel/Diagnostics/PipelineStage.cs` — Classify=0, Score=1, Deduplicate=2, Slice=3, Place=4; spec Activity names: classify, score, deduplicate, slice, place (lowercase)
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — carries Item+Score+Reason+DeduplicatedAgainst; **NO Stage field** — bridge must map ExclusionReason → PipelineStage (see Pitfalls section)
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — carries Item+Score+Reason; used for Full verbosity cupel.item.included Events
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — Included, Excluded, TotalCandidates, TotalTokensConsidered, CountRequirementShortfalls; this is the source of all per-item data
- `src/Wollax.Cupel/CupelPipeline.cs` — `Execute()` creates SelectionReport ONLY if `trace is DiagnosticTraceCollector` (critical constraint; CupelOpenTelemetryTraceCollector is NOT a DiagnosticTraceCollector); `DryRun()` always creates SelectionReport; RecordStageEvent called 5× with elapsed time AFTER each stage completes
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — **Template for new package**: IsPackable=true, ProjectReference to core, PublicApiAnalyzers with PrivateAssets="All", AdditionalFiles for PublicAPI.Shipped.txt + PublicAPI.Unshipped.txt
- `src/Wollax.Cupel.Testing/PublicAPI.Shipped.txt` — New packages start with only `#nullable enable`; Unshipped.txt populated after initial build
- `Cupel.slnx` — Must add both `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` and `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` entries
- `Directory.Packages.props` — Central version management; must add `OpenTelemetry` + `OpenTelemetry.Api` version entries here
- `release.yml` — `dotnet pack` globs `./nupkg/*.nupkg`; the new package is auto-included if pack output goes to `./nupkg`; no per-package pack step needed

## Architectural Detail: The ITraceCollector Impedance Mismatch

This is the most critical design constraint for S05. The pipeline's Execute() flow:

```
pipeline.Execute(items, traceCollector)
  → RecordStageEvent(Classify, elapsed, itemCount)  // bridge gets called here
  → RecordStageEvent(Score, elapsed, itemCount)
  → RecordStageEvent(Deduplicate, elapsed, itemCount)
  → RecordStageEvent(Slice, elapsed, itemCount)
  → RecordStageEvent(Place, elapsed, itemCount)
  → if (trace is DiagnosticTraceCollector) → build SelectionReport  // bridge NEVER triggers this
  → return ContextResult { Report = null }  // bridge gets null report!
```

**Resolution**: The test harness uses `DryRun()` instead of `Execute()`, which always produces a
non-null `ContextResult.Report`:

```csharp
var tracer = new CupelOpenTelemetryTraceCollector(CupelVerbosity.Full);
// DryRun creates its own DiagnosticTraceCollector internally → always returns Report
var result = pipeline.DryRun(items);
tracer.Complete(result.Report!, budget); // emits all Activities
```

For StageOnly, DryRun is also usable (or Execute with a separate DiagnosticTraceCollector).

## Activity Timing Strategy

RecordStageEvent is called AFTER each stage completes. The bridge buffers:
- `(PipelineStage stage, TimeSpan duration, int itemCountOut)[]`

In `Complete()`, the bridge creates Activities retroactively:
```csharp
var rootEnd = DateTimeOffset.UtcNow;
var totalDuration = sum of all stage durations;
var rootStart = rootEnd - totalDuration;

// Root Activity
using var root = _source.StartActivity("cupel.pipeline", ActivityKind.Internal, default, 
    startTime: rootStart);
// ...set root attributes...

// Stage Activities — build from cumulative offsets
var offset = TimeSpan.Zero;
foreach (var (stage, duration, countOut) in _stages)
{
    var stageStart = rootStart + offset;
    var stageName = stage.ToString().ToLowerInvariant();
    using var stageActivity = _source.StartActivity($"cupel.stage.{stageName}",
        ActivityKind.Internal, root?.Context ?? default, startTime: stageStart);
    // set attributes, add events from report
    stageActivity?.SetEndTime(stageStart + duration);
    offset += duration;
}

root?.SetEndTime(rootEnd);
```

## ExclusionReason → PipelineStage Mapping

`ExcludedItem` has no `Stage` field. The bridge must map `ExclusionReason → PipelineStage`:

| ExclusionReason | Stage |
|---|---|
| NegativeTokens | Classify |
| Deduplicated | Deduplicate |
| BudgetExceeded | Slice |
| PinnedOverride | Slice |
| CountCapExceeded | Slice |
| CountRequireCandidatesExhausted | Slice |
| QuotaCapExceeded | Slice |
| QuotaRequireDisplaced | Slice |
| ScoredTooLow | Score |
| Filtered | Classify |
| _(unknown/future)_ | Slice (fallback) |

This mapping is internal to the bridge and not part of the public API.

## item_count_in Derivation

`RecordStageEvent` provides `ItemCount` = items OUT of the stage. `item_count_in` for stage[n]
= `item_count_out` for stage[n-1]. For Classify (first stage): `item_count_in` =
`SelectionReport.TotalCandidates`. For StageOnly (no report), Classify's `item_count_in` can
default to `item_count_out` or `0`.

## OTel NuGet Package Dependencies

For net10.0, use `OpenTelemetry.Api` (not the full `OpenTelemetry` package) since the companion
only needs `ActivitySource` and `Activity`. `ActivitySource` and `Activity` are actually in
`System.Diagnostics.DiagnosticSource` which is part of the BCL for net10.0 — **no NuGet
dependency may be needed at all for creating Activities**.

Check: `System.Diagnostics.Activity` and `System.Diagnostics.ActivitySource` are in the BCL
since .NET 5. For net10.0, `Activity`, `ActivitySource`, `ActivityKind`, `ActivityTagsCollection`
are all available without any NuGet package.

The `AddCupelInstrumentation(TracerProviderBuilder)` extension method requires a reference to the
OpenTelemetry SDK's `TracerProviderBuilder`. The minimal package for this extension is
`OpenTelemetry.Api`. However: if the package takes zero NuGet deps, the `AddCupelInstrumentation`
extension must live elsewhere OR the package takes a soft dependency.

**Pragmatic recommendation**: Take `OpenTelemetry.Api` as a dependency. Companion packages are
explicitly allowed to take SDK dependencies (boundary map: "Wollax.Cupel.Diagnostics.OpenTelemetry
may take OpenTelemetry SDK dependency — this is expected for a companion package"). Look up the
current stable version at research time — approximately 1.10.x for early 2026.

**Alternative if zero-dep preferred**: Implement `CupelOpenTelemetryTraceCollector` using only
BCL `System.Diagnostics.ActivitySource` (no OpenTelemetry.Api), and provide `AddCupelInstrumentation`
as a static method that returns `string` ("Wollax.Cupel") — callers use it in their own
`AddSource()` call. This avoids all SDK dependencies. This is the safer approach given the
zero-dep culture in this codebase.

## Constraints

- `Wollax.Cupel` core has ZERO compile-time dependency on OpenTelemetry SDK (D039, R032); violation blocks the build with a project reference cycle
- `DiagnosticTraceCollector` is `sealed` — bridge cannot inherit; `CupelOpenTelemetryTraceCollector` must compose or wrap differently
- ActivitySource name MUST be exactly `"Wollax.Cupel"` (spec conformance note)
- Stage Activity names MUST be `cupel.stage.{lowercase_stage_name}` — use `stage.ToString().ToLowerInvariant()` not hardcoded strings
- `cupel.exclusion.reason` values MUST be the canonical `ExclusionReason` variant name string — use `.ToString()` not numeric cast
- `PublicAPI.Unshipped.txt` must list every public member including `CupelVerbosity` enum values; run initial build → capture RS0016 errors → populate
- `TreatWarningsAsErrors` is project-wide (Directory.Build.props); any compiler warning is a build failure
- Central package version management: new NuGet dependencies must be added to `Directory.Packages.props`; `<PackageVersion Include="..." Version="..."/>` entry required before `<PackageReference>` in csproj

## Common Pitfalls

- **DiagnosticTraceCollector sealed → null Report from Execute()** — Always use `DryRun()` in the test harness for StageAndExclusions/Full. Document this clearly. Do not pass a `CupelOpenTelemetryTraceCollector` to `Execute()` expecting a non-null Report.
- **Activity created after it's stopped** — Once `Activity.Stop()` or `Dispose()` is called, `AddEvent()` is a no-op. Create Activity, add all Events, THEN stop it. In the Complete() loop: create stageActivity → add all events for that stage → stop stageActivity.
- **ActivitySource.StartActivity returns null** — When no listener is attached to the source, `StartActivity` returns null. All `activity?.SetTag(...)` calls must use null-conditional. The test harness must call `ActivitySource.AddActivityListener()` to get non-null Activities.
- **Stage name case** — `PipelineStage.Deduplicate.ToString()` = "Deduplicate" → `.ToLowerInvariant()` = "deduplicate". Do not hardcode "dedup". Spec says `cupel.stage.deduplicate`.
- **`cupel.item.score` attribute type** — Spec says `float64`; use `activity?.AddTag("cupel.item.score", score.ToString("G17"))` or the `double` overload of `AddTag`. Do not cast to `float`.
- **NuGet version not in Directory.Packages.props** — If you add `<PackageReference Include="OpenTelemetry.Api"/>` in csproj without a corresponding `<PackageVersion>` entry in Directory.Packages.props, the build fails with "NETSDK1138: The target framework" or similar CPM error. Add the version to Directory.Packages.props first.
- **nuget.config path for consumption tests** — The consumption test (`Wollax.Cupel.ConsumptionTests`) uses `./packages` as local feed (not `./nupkg`). Copy the new `.nupkg` there. This is per D095.
- **PublicAPI files not created** — New packages must have both `PublicAPI.Shipped.txt` (content: `#nullable enable`) and `PublicAPI.Unshipped.txt` (content: populated after first build). Missing either file blocks the build with AdditionalFiles error.

## Open Risks

- **OpenTelemetry.Api version** — `Directory.Packages.props` has no OTel entry yet. Need to decide version. Using BCL Activity/ActivitySource directly (no OTel NuGet dep) is viable and cleaner for net10.0 if `AddCupelInstrumentation` is optional or returns a string constant. Check if `TracerProviderBuilder` is needed.
- **ActivitySource listener in tests** — TUnit tests must set up an `ActivityListener` before calling Complete() or Activities will be null. Pattern: `var source = new ActivitySource("Wollax.Cupel"); ActivitySource.AddActivityListener(listener)`. This is boilerplate the test must provide; document it.
- **ExclusionReason without Stage on ExcludedItem** — The stage→reason mapping is an implementation assumption (NegativeTokens→Classify, Deduplicated→Deduplicate, everything else→Slice). If future pipeline versions exclude items at different stages, the mapping becomes incorrect. Document this as a known limitation.
- **DryRun vs Execute usage for StageAndExclusions/Full** — The two-phase design requires DryRun for item-level data. Some callers may want to use Execute (for performance). The bridge can support Execute for StageOnly and DryRun for richer tiers; document this distinction clearly in README and code.
- **CountRequirementShortfalls in OTel** — The spec doesn't mention shortfalls in the OTel attribute table. CountRequireCandidatesExhausted reason appears in ExclusionReason→Stage mapping (Slice). No special handling needed beyond the standard ExclusionReason mapping.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| .NET / C# | (no dedicated skill) | none found |
| OpenTelemetry .NET | (no dedicated skill) | none found |

## Sources

- `spec/src/integrations/opentelemetry.md` — Canonical spec: 5-Activity hierarchy, 3 verbosity tiers, exact `cupel.*` attribute table, cardinality table (source: local spec file)
- `src/Wollax.Cupel/CupelPipeline.cs` — Pipeline execution flow, `trace is DiagnosticTraceCollector` check, RecordStageEvent call sites (source: codebase)
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — Interface contract; TraceEvent fields (source: codebase)
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — Package project template (source: codebase)
- `Directory.Packages.props` — Central version management; currently has no OTel entries (source: codebase)
- `release.yml` — Auto-includes all packages from `./nupkg` glob; no per-package job needed (source: codebase)
- D043, D068 — OTel attribute namespace pre-stable, 5 Activities (Sort omitted) (source: DECISIONS.md)
