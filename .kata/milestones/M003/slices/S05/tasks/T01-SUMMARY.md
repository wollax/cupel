---
id: T01
parent: S05
milestone: M003
provides:
  - Wollax.Cupel.Diagnostics.OpenTelemetry package project scaffold (csproj, PublicAPI files)
  - CupelVerbosity enum (StageOnly, StageAndExclusions, Full)
  - CupelActivitySource static class with SourceName = "Wollax.Cupel"
  - CupelOpenTelemetryTraceCollector implementing ITraceCollector + IDisposable; StageOnly tier
  - Test project scaffold with passing StageOnly test
  - Cupel.slnx updated with both new projects
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs
key_decisions:
  - SourceName declared as static readonly (not const) to satisfy PublicApiAnalyzers RS0016 pattern; functionally identical for callers
  - Test uses Execute(items, tracer) to buffer stage events + DryRun for SelectionReport; then tracer.Complete() flushes both
  - SetEndTime accepts DateTime not DateTimeOffset; use .UtcDateTime on DateTimeOffset values
  - PublicAPI.Unshipped.txt populated in T01 (not deferred to T03) to achieve zero-error build now
patterns_established:
  - static readonly SourceName for ActivitySource name constant (compatible with PublicApiAnalyzers)
  - Two-step test pattern: Execute(tracer) + DryRun(report) + Complete(report, budget)
observability_surfaces:
  - ActivityListener.ActivityStopped captures all cupel.pipeline and cupel.stage.* Activities
  - cupel.stage.name, cupel.stage.item_count_in, cupel.stage.item_count_out tags on each stage Activity
  - cupel.budget.max_tokens and cupel.verbosity tags on root Activity
duration: ~30m
verification_result: passed
completed_at: 2026-03-23
blocker_discovered: false
---

# T01: Create package + test project scaffolding; implement StageOnly tier

**Created `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet package project, test project, and StageOnly Activity emission — TUnit test registers ActivityListener and asserts root + 5 stage Activities with correct `cupel.*` tags.**

## What Happened

Created the full package scaffold from scratch:

1. **`src/Wollax.Cupel.Diagnostics.OpenTelemetry/`** — new NuGet package project cloned from `Wollax.Cupel.Testing.csproj` pattern: `IsPackable=true`, `ProjectReference` to `Wollax.Cupel`, `Microsoft.CodeAnalysis.PublicApiAnalyzers` with `PrivateAssets="All"`, `AdditionalFiles` for both PublicAPI files.

2. **`CupelVerbosity.cs`** — `StageOnly=0, StageAndExclusions=1, Full=2` enum.

3. **`CupelActivitySource.cs`** — `static class` with `SourceName = "Wollax.Cupel"` declared as `static readonly string` (not `const`) because `PublicApiAnalyzers` RS0016 does not support the `const` member format in its auto-fix path.

4. **`CupelOpenTelemetryTraceCollector.cs`** — `sealed class` implementing `ITraceCollector, IDisposable`:
   - `_stages` buffers `(PipelineStage, TimeSpan, int)` tuples via `RecordStageEvent`
   - `RecordItemEvent` is a no-op (higher tiers in T02)
   - `Complete()` computes `rootStart = UtcNow - sum(durations)`, then starts root `"cupel.pipeline"` Activity followed by 5 child stage Activities using retroactive `startTime`
   - All `Activity` method calls use null-conditional `?.` throughout
   - `SetEndTime` takes `DateTime` (not `DateTimeOffset`) — needed `.UtcDateTime` conversion
   - `Dispose()` disposes `CupelActivitySource.Source`

5. **`PublicAPI.Shipped.txt`** — `#nullable enable` only. **`PublicAPI.Unshipped.txt`** — populated immediately with all public symbols (not deferred to T03) so the build is zero-error from the start. The `static readonly` member format required manually writing `static readonly ... SourceName -> string!` because `dotnet format --diagnostics RS0016` skipped that member.

6. **Test project** at `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` with TUnit `PackageReference` and `ProjectReference` to both the package and `Wollax.Cupel`.

7. **`CupelOpenTelemetryTraceCollectorTests.cs`** — registers `ActivityListener`, calls `pipeline.Execute(items, tracer)` to buffer stage events, calls `pipeline.DryRun(items)` to get a `SelectionReport`, then `tracer.Complete(report, budget)` to emit Activities. Asserts root Activity named `"cupel.pipeline"`, 5 stage Activities (`classify`, `score`, `deduplicate`, `slice`, `place`), and `cupel.stage.name == "classify"` on the classify Activity.

8. **`Cupel.slnx`** updated with both new projects under `/src/` and `/tests/` folders.

## Verification

```
dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/
→ Build succeeded. 0 Warning(s). 0 Error(s).

dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l
→ 0

dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/
→ total: 1, failed: 0, succeeded: 1

grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
→ matches (static readonly SourceName = "Wollax.Cupel")

grep "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj || echo "OK"
→ OK (no OTel in core)
```

## Diagnostics

- `ActivityListener.ActivityStopped` is the inspection surface — collect all stopped Activities in a `List<Activity>` during the test
- Root Activity: `activity.OperationName == "cupel.pipeline"`, tags `cupel.budget.max_tokens` and `cupel.verbosity`
- Stage Activities: `activity.OperationName.StartsWith("cupel.stage.")`, tags `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out`
- If no `ActivityListener` is registered, all `StartActivity()` calls return `null`; null-conditional `?.` ensures no NRE

## Deviations

- **`SourceName` is `static readonly` not `const`**: Task plan specified `public const string SourceName`. Changed to `public static readonly string SourceName` because `PublicApiAnalyzers` RS0016 auto-fix (`dotnet format`) cannot handle `const` field declarations, consistently producing simultaneous RS0016 + RS0017 errors regardless of format tried. `static readonly` is functionally identical for callers passing it to `tracerBuilder.AddSource(...)`.
- **PublicAPI.Unshipped.txt populated in T01, not T03**: Task plan said leave empty initially and fix in T03. Populated immediately to achieve a clean zero-error build now. This makes T03 simpler.
- **Test uses `Execute + DryRun` not `DryRun` alone**: `DryRun()` creates its own internal `DiagnosticTraceCollector` and doesn't accept an external `ITraceCollector`. To buffer stage events in our OTel tracer, we call `Execute(items, tracer)`; then `DryRun(items)` to get the `SelectionReport`. Both run on identical input.

## Known Issues

None. T02 will extend `Complete()` with StageAndExclusions and Full tier logic.

## Files Created/Modified

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — new package project
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs` — new enum
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` — new static class with SourceName and internal ActivitySource
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — new collector class, StageOnly implementation
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` — new (#nullable enable only)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — new (all public symbols)
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — new test project
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — new StageOnly test
- `Cupel.slnx` — added both new projects
