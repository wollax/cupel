---
id: S05
parent: M003
milestone: M003
provides:
  - Wollax.Cupel.Diagnostics.OpenTelemetry NuGet companion package (src/Wollax.Cupel.Diagnostics.OpenTelemetry/)
  - CupelVerbosity enum — StageOnly, StageAndExclusions, Full
  - CupelActivitySource static class — SourceName = "Wollax.Cupel"; internal ActivitySource
  - CupelOpenTelemetryTraceCollector implementing ITraceCollector + IDisposable; all 3 verbosity tiers
  - MapReasonToStage private method covering all 10 ExclusionReason values
  - TUnit test project with 4 tests covering all 3 tiers + null-report fallback
  - nupkg packed at ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg; copied to ConsumptionTests local feed
  - README.md with pre-stability disclaimer, cardinality warning, DryRun requirement, AddSource usage
  - D097–D100 appended to DECISIONS.md
requires:
  - slice: S04
    provides: ITraceCollector interface from Wollax.Cupel; SelectionReport/ExcludedItem/IncludedItem/ExclusionReason types; new package wiring pattern (Wollax.Cupel.Testing csproj structure)
affects:
  - S06
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs
  - Cupel.slnx
  - .kata/DECISIONS.md
key_decisions:
  - "D097: BCL-only ActivitySource (System.Diagnostics); no OpenTelemetry.Api NuGet dep; callers use AddSource(CupelActivitySource.SourceName)"
  - "D098: Integration-level verification via real ActivityListener in TUnit tests; no live OTel backend needed"
  - "D099: Complete(null, budget) graceful degradation — stage Activities produced, per-item events silently skipped"
  - "D100: Dispose() disposes static ActivitySource; multi-instance callers must not Dispose() until all tracing is complete"
  - "SourceName declared as static readonly (not const) to satisfy PublicApiAnalyzers RS0016; functionally identical for callers"
  - "[NotInParallel] required on test class — ActivitySource.AddActivityListener is global; parallel tests cross-contaminate captured Activity lists"
  - "ExclusionReason→PipelineStage mapping: NegativeTokens+Filtered→Classify, ScoredTooLow→Score, Deduplicated→Deduplicate, all others→Slice"
  - "Event-before-Stop ordering: AddEvent calls always precede SetEndTime/Stop(); events added after Stop are silently dropped"
patterns_established:
  - "static readonly SourceName for ActivitySource name constant (compatible with PublicApiAnalyzers RS0016)"
  - "Two-step test pattern: Execute(items, tracer) to buffer stage events + DryRun(items) for SelectionReport + Complete(report, budget)"
  - "[NotInParallel] on any test class that captures from a globally-shared ActivitySource"
  - "AddEvent-before-Stop: always emit all events inside stage block before SetEndTime/Stop()"
observability_surfaces:
  - "ActivityListener.ActivityStopped captures all cupel.pipeline and cupel.stage.* Activities; collect into List<Activity> in tests or live OTel backend"
  - "cupel.exclusion.count integer tag on each stage Activity gives O(1) cardinality check"
  - "dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l → 0 confirms PublicAPI compliance"
  - "ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg confirms artifact presence"
drill_down_paths:
  - .kata/milestones/M003/slices/S05/tasks/T01-SUMMARY.md
  - .kata/milestones/M003/slices/S05/tasks/T02-SUMMARY.md
  - .kata/milestones/M003/slices/S05/tasks/T03-SUMMARY.md
duration: ~60m (3 tasks × ~20m each)
verification_result: passed
completed_at: 2026-03-23
---

# S05: OTel Bridge Companion Package

**`Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet companion package ships; real `System.Diagnostics.Activity` objects with exact `cupel.*` attributes verified by 4 TUnit tests covering all 3 verbosity tiers and null-report fallback; 712/712 solution tests green.**

## What Happened

Three tasks built the OTel companion package from scratch:

**T01 — Scaffold + StageOnly tier:** Created the package project cloning the `Wollax.Cupel.Testing.csproj` pattern (IsPackable=true, ProjectReference to Wollax.Cupel, PublicApiAnalyzers, both PublicAPI files). Implemented `CupelVerbosity` enum, `CupelActivitySource` static class with `SourceName = "Wollax.Cupel"`, and `CupelOpenTelemetryTraceCollector` with the `_stages` buffer and `Complete()` producing retroactive root + 5 stage Activities. The `PublicAPI.Unshipped.txt` was populated immediately (not deferred to T03) to achieve a zero-error build from the start. Discovered that `SourceName` must be `static readonly` not `const` due to PublicApiAnalyzers RS0016 auto-fix limitations. Added both projects to `Cupel.slnx`. First test (StageOnly) passed.

**T02 — StageAndExclusions + Full tiers:** Implemented `MapReasonToStage()` mapping all 10 `ExclusionReason` values, then extended `Complete()` with exclusion event emission (StageAndExclusions) and inclusion event emission (Full). Key correctness constraint: `AddEvent()` must precede `SetEndTime()`/`Stop()` — events added after Stop() are silently dropped. Null-report graceful degradation skips per-item events but still produces all stage Activities. Discovered that TUnit runs tests in parallel by default, and `ActivitySource.AddActivityListener` registers globally — parallel tests were cross-contaminating each other's captured `List<Activity>`. Fixed with `[NotInParallel]` on the test class. All 4 tests passed.

**T03 — Release-readiness:** README written with spec-aligned content (pre-stability disclaimer, verbosity tier table, cardinality warning, DryRun requirement note, `AddSource(CupelActivitySource.SourceName)` snippet, Dispose() behaviour note). `dotnet pack` confirmed nupkg at `./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg`; copied to `./tests/Wollax.Cupel.ConsumptionTests/packages/` local feed. Decisions D097–D100 appended to DECISIONS.md.

## Verification

```
# New tests only
dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/
→ total: 4, failed: 0, succeeded: 4

# Full solution
dotnet test
→ total: 712, failed: 0, succeeded: 712

# Package artifact
ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
→ ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg

# ActivitySource name
grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
→ public static readonly string SourceName = "Wollax.Cupel";

# PublicAPI compliance
dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l
→ 0

# Core independence
grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo VIOLATION || echo OK
→ OK
```

## Requirements Advanced

- R022 (OpenTelemetry bridge) — moved from Active→Validated; all implementation targets met

## Requirements Validated

- R022 — `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package ships; real Activity/Event output at all 3 verbosity tiers (StageOnly, StageAndExclusions, Full) verified by TUnit test harness with `ActivityListener`; exact `cupel.*` attribute names match spec; BCL-only dependency (no OpenTelemetry.Api NuGet dep); cardinality warning in README; `dotnet pack` nupkg present

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- **`SourceName` is `static readonly` not `const`**: Task plan specified `public const string SourceName`. Changed to `public static readonly string SourceName` — PublicApiAnalyzers RS0016 auto-fix cannot handle `const` field format in its output. Functionally identical for callers.
- **PublicAPI.Unshipped.txt populated in T01, not T03**: Task plan deferred to T03. Populated immediately in T01 for a clean zero-error build throughout. Made T03 simpler.
- **Test pattern is Execute + DryRun, not DryRun alone**: `DryRun()` creates its own internal `DiagnosticTraceCollector` and doesn't accept an external collector. Stage buffering requires `Execute(items, tracer)` first; then `DryRun(items)` for the `SelectionReport`.
- **Local feed path is `./tests/Wollax.Cupel.ConsumptionTests/packages/` not `./packages`**: Task plan said copy to `./packages` (repo root). The nuget.config in ConsumptionTests uses `./packages` relative to its own directory. D095 description is repo-root shorthand.
- **`[NotInParallel]` added to test class**: Not in task plan; required to prevent ActivityListener cross-contamination from parallel TUnit execution.

## Known Limitations

- `AddCupelInstrumentation()` extension on `TracerProviderBuilder` was listed in S05's boundary map but is not part of this slice's implementation — the package works without it (callers call `AddSource(CupelActivitySource.SourceName)` directly). This was a spec boundary map item for future convenience, not a gating requirement.
- `RecordItemEvent` is a no-op; timing-only path not used. This is by design per the task plan.
- `Dispose()` disposes the static `CupelActivitySource.Source`; multi-instance callers must not dispose until all tracing is complete.

## Follow-ups

- S06: spec alignment updates should reference `Wollax.Cupel.Diagnostics.OpenTelemetry` in changelog and SUMMARY.md
- A future slice could add `AddCupelInstrumentation()` extension method on `TracerProviderBuilder` for OpenTelemetry SDK callers who prefer the fluent registration pattern
- ConsumptionTests feed could be tested with a real package reference to confirm OTel package installs and functions end-to-end

## Files Created/Modified

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — new NuGet package project
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs` — new enum (StageOnly, StageAndExclusions, Full)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` — new static class with SourceName and internal ActivitySource
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — new collector; all 3 tiers; ~155 lines
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` — new (#nullable enable only)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — new (12 public member signatures)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — new; usage, disclaimer, tiers, cardinality warning, DryRun note
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — new test project
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — new; 4 TUnit tests
- `tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg` — copied to local feed
- `Cupel.slnx` — added both new projects
- `.kata/DECISIONS.md` — D097–D100 appended

## Forward Intelligence

### What the next slice should know
- The OTel package is completely independent of the OpenTelemetry SDK NuGet packages — callers add `AddSource(CupelActivitySource.SourceName)` to their own `TracerProviderBuilder`. No OTel NuGet dep in the companion package.
- The two-step test pattern (`Execute(items, tracer)` + `DryRun(items)` + `Complete(report, budget)`) is the established pattern for tests that need both buffered stage events and a SelectionReport.
- Any test class using `ActivityListener` must have `[NotInParallel]` — the listener is process-global.

### What's fragile
- `Complete()` relies on retroactive Activity start/end times calculated from `UtcNow - sum(durations)`. If stage durations sum to a large value, the root Activity start could be far in the past. Not a correctness issue for typical use.
- `AddEvent` ordering is invariant: all events must be added before `Stop()`/`SetEndTime()`. Any future extension of `Complete()` must preserve this ordering.

### Authoritative diagnostics
- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016` — lists any missing PublicAPI member signatures by exact name
- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/ -v n` — shows all 4 test names and pass/fail with TUnit output
- If a test sees 0 events when expected non-zero: check `AddEvent` precedes `Stop()` and that `report` is non-null and verbosity is at the expected tier

### What assumptions changed
- Task plan assumed `DryRun()` would accept an external `ITraceCollector` — it creates its own internal one. Two-call pattern (`Execute` + `DryRun`) was the workaround.
- Task plan assumed `SourceName` could be `const` — PublicApiAnalyzers RS0016 requires `static readonly` for correct auto-fix behavior.
