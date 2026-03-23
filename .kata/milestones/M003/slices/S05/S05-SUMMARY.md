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
  - D097–D105 appended to DECISIONS.md
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
  - .kata/REQUIREMENTS.md
key_decisions:
  - "D097: BCL-only ActivitySource (System.Diagnostics); no OpenTelemetry.Api NuGet dep; callers use AddSource(CupelActivitySource.SourceName)"
  - "D098: Integration-level verification via real ActivityListener in TUnit tests; no live OTel backend needed"
  - "D099: Complete(null, budget) graceful degradation — stage Activities produced, per-item events silently skipped"
  - "D100: Dispose() disposes static ActivitySource; multi-instance callers must not Dispose() until all tracing is complete"
  - "D101: CupelActivitySource.SourceName is public static readonly (not const) to satisfy PublicApiAnalyzers RS0016 without changing caller ergonomics"
  - "D102: OTel tests use Execute(items, tracer) + DryRun(items) + Complete(report, budget) because DryRun() does not accept an external collector"
  - "D103: ActivityListener-based tests must be [NotInParallel] because listeners are process-global"
  - "D104: AddEvent must always occur before SetEndTime/Stop(); otherwise BCL silently drops events"
  - "D105: ExclusionReason→PipelineStage mapping is NegativeTokens+Filtered→Classify, ScoredTooLow→Score, Deduplicated→Deduplicate, all other current reasons→Slice"
patterns_established:
  - "static readonly SourceName for ActivitySource name constant (compatible with PublicApiAnalyzers RS0016)"
  - "Two-step trace test pattern: Execute(items, tracer) to buffer stage events + DryRun(items) for SelectionReport + Complete(report, budget)"
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

**`Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet companion package ships; real `System.Diagnostics.Activity` objects with exact `cupel.*` attributes are verified by TUnit across StageOnly, StageAndExclusions, Full, and null-report fallback; all slice verification checks now pass.**

## What Happened

This slice built the OTel companion package from scratch and proved it mechanically.

**T01 — package scaffold + StageOnly tier:** the slice created `src/Wollax.Cupel.Diagnostics.OpenTelemetry/`, cloned the proven package wiring pattern from `Wollax.Cupel.Testing`, added `CupelVerbosity`, `CupelActivitySource`, and the first `CupelOpenTelemetryTraceCollector` implementation. `Complete()` buffered stage timing data and emitted a root `cupel.pipeline` Activity plus the five stage Activities (`classify`, `score`, `deduplicate`, `slice`, `place`). The package was added to `Cupel.slnx`, and the first TUnit test passed.

**T02 — StageAndExclusions + Full tiers:** `Complete()` was extended with exclusion events and included-item events. `MapReasonToStage()` now maps all current `ExclusionReason` values onto the user-visible pipeline stages. The implementation also locked an important BCL invariant: all `AddEvent()` calls must happen before `SetEndTime()` / `Stop()`, or the runtime silently drops the events. During test expansion, the slice discovered that `ActivitySource.AddActivityListener` is process-global; TUnit parallelism caused cross-test contamination, so the class was marked `[NotInParallel]`. Four tests now cover StageOnly, StageAndExclusions, Full, and null-report graceful fallback.

**T03 — release readiness:** the slice confirmed PublicAPI cleanliness (`RS0016 = 0`), wrote a package README with the pre-stability disclaimer, verbosity tier guidance, cardinality warning, `AddSource(CupelActivitySource.SourceName)` example, and DryRun requirement note, then packed the `.nupkg` and copied it into the ConsumptionTests local feed. The decisions register was updated with the companion-package architecture and the testing/runtime invariants discovered during implementation.

After implementation, the slice reran every slice-level verification command from the plan and all passed.

## Verification

```bash
rtk proxy dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj
# → total: 4, failed: 0, succeeded: 4

rtk proxy dotnet test
# → total: 712, failed: 0, succeeded: 712

rtk ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
# → ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg

rtk grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
# → public static readonly string SourceName = "Wollax.Cupel";

rtk proxy dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj 2>&1 | grep "RS0016" | wc -l
# → 0

rtk grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo VIOLATION || echo OK
# → OK
```

Additional artifact check already retained from T03: the packed nupkg is also present in `tests/Wollax.Cupel.ConsumptionTests/packages/` for local-feed consumption.

## Requirements Advanced

- R022 — completed the mapped M003/S05 implementation and moved it from active/mapped to validated/proven

## Requirements Validated

- R022 — `Wollax.Cupel.Diagnostics.OpenTelemetry` now ships as a companion package, emits real `System.Diagnostics.Activity` output at StageOnly, StageAndExclusions, and Full verbosity, uses the spec-defined `cupel.*` attribute names, keeps the core package free of OTel dependencies, passes dedicated TUnit integration tests, packs successfully, and leaves a consumable `.nupkg` artifact in the local feed

## New Requirements Surfaced

- None

## Requirements Invalidated or Re-scoped

- None

## Deviations

- `CupelActivitySource.SourceName` is `public static readonly string`, not `const`. This was required to satisfy PublicApiAnalyzers RS0016 behavior without degrading caller ergonomics.
- `PublicAPI.Unshipped.txt` was populated in T01 instead of waiting until T03 so the project stayed warning/error-clean throughout the slice.
- The trace test pattern is `Execute(items, tracer)` + `DryRun(items)` + `Complete(report, budget)`, not `DryRun()` alone, because `DryRun()` creates its own internal collector and cannot buffer an external OTel tracer.
- The local feed path is `tests/Wollax.Cupel.ConsumptionTests/packages/`, not repo-root `./packages`; the latter was shorthand in earlier notes.
- `[NotInParallel]` was added to the test class because Activity listeners are global and parallel test execution caused false failures.

## Known Limitations

- The slice does not add an `AddCupelInstrumentation()` convenience extension; callers use `AddSource(CupelActivitySource.SourceName)` directly.
- `RecordItemEvent` remains a no-op by design; the collector emits its richer data during `Complete()`.
- `Dispose()` tears down the shared static `ActivitySource`; multi-instance callers must avoid disposing until all tracing work is finished.
- Live export to Jaeger/Honeycomb/Aspire is not proven here; the slice proves BCL Activity emission, not exporter-specific behavior.

## Follow-ups

- S06 should update milestone/spec-facing summary surfaces so the OTel companion package is reflected in release-alignment docs alongside the other M003 outputs.
- A future slice could add an optional fluent registration extension if callers explicitly want an OpenTelemetry SDK dependency.
- A future consumption test could exercise the package via `PackageReference` from the local feed end-to-end.

## Files Created/Modified

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — new companion package project
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs` — new verbosity enum
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` — exported source name + shared `ActivitySource`
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — collector implementation for all three verbosity tiers
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` — PublicApiAnalyzers shipped surface seed
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — public member manifest with zero RS0016 errors
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — usage, disclaimer, verbosity, cardinality, DryRun guidance
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` — new TUnit test project
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — four integration tests covering all tiers and null-report fallback
- `tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg` — copied package artifact for local-feed consumption
- `Cupel.slnx` — solution wiring for package + tests
- `.kata/DECISIONS.md` — D097–D105 appended
- `.kata/REQUIREMENTS.md` — R022 status advanced to validated
- `.kata/milestones/M003/M003-ROADMAP.md` — S05 marked complete

## Forward Intelligence

### What the next slice should know
- The OTel companion package is intentionally BCL-only. There is no `OpenTelemetry.Api` dependency; callers integrate by listening to or exporting the `Wollax.Cupel` ActivitySource themselves.
- The reliable test pattern for any future Activity-based verification is `Execute(items, tracer)` to buffer stage timings, `DryRun(items)` to obtain `SelectionReport`, then `Complete(report, budget)` to flush Activities and events.
- Any TUnit class that uses `ActivitySource.AddActivityListener` must be `[NotInParallel]`.

### What's fragile
- Event ordering inside `Complete()` — adding events after `Stop()` causes silent data loss, so any future refactor must preserve the current emission order.
- Shared static source lifetime — disposing one collector affects all future collectors in-process.

### Authoritative diagnostics
- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/ -v n` — definitive proof of Activity/event structure across all verbosity tiers
- `dotnet build ... | grep RS0016` — definitive proof that PublicAPI files match the compiled public surface
- `ActivityListener.ActivityStopped` in the test harness — definitive runtime inspection surface for emitted Activities and their tags/events

### What assumptions changed
- Original assumption: `DryRun()` alone could drive the trace test harness. Actual outcome: the collector needs a two-call pattern because `DryRun()` owns its own internal collector.
- Original assumption: `CupelActivitySource.SourceName` could be `const`. Actual outcome: `static readonly` is the stable PublicApiAnalyzers-compatible shape.
