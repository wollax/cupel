# S05: OTel Bridge Companion Package — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: All observable outcomes are mechanically checkable via `dotnet test`, `dotnet build`, `ls`, and `grep`. The OTel integration is verified by a TUnit test harness that registers a real `ActivityListener` and captures real `System.Diagnostics.Activity` objects — no live OTel backend (Jaeger, Honeycomb, Aspire) is required to confirm correct attribute names and event structure. Human gut-check adds no signal beyond what the test harness already confirms.

## Preconditions

- .NET 10 SDK installed
- `cd /Users/wollax/Git/personal/cupel`
- `dotnet restore` has been run (or solution is already restored)

## Smoke Test

```bash
dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/
```

Expected: `total: 4, failed: 0, succeeded: 4`

## Test Cases

### 1. All 3 verbosity tiers produce correct Activities and Events

```bash
dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/ -v n
```

1. Run the command above.
2. **Expected:** 4 tests pass — `StageOnly_ProducesRootAndFiveStageActivities`, `StageAndExclusions_ProducesExclusionEvents`, `Full_ProducesIncludedItemEvents`, `NullReport_GracefulDegradation`

### 2. Full solution remains green

```bash
dotnet test
```

1. Run the full solution test.
2. **Expected:** `total: 712, failed: 0, succeeded: 712`

### 3. Package artifact exists

```bash
ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
```

1. Run the command.
2. **Expected:** File listed (e.g. `Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg`)

### 4. ActivitySource name matches spec

```bash
grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
```

1. Run the command.
2. **Expected:** Line containing `SourceName = "Wollax.Cupel"` is printed

### 5. PublicAPI compliance — zero RS0016 errors

```bash
dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj 2>&1 | grep "RS0016" | wc -l
```

1. Run the command.
2. **Expected:** `0`

### 6. Core package independence — no OTel reference in Wollax.Cupel

```bash
grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo "VIOLATION" || echo "OK"
```

1. Run the command.
2. **Expected:** `OK`

## Edge Cases

### Null report with Full verbosity — no crash

```bash
dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/ --filter "NullReport"
```

1. Run the filter.
2. **Expected:** Test passes — stage Activities are produced; no exception thrown; per-item events silently skipped

### Local feed populated for consumption tests

```bash
ls ./tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
```

1. Run the command.
2. **Expected:** File listed

## Failure Signals

- Any `RS0016` errors in `dotnet build` output → `PublicAPI.Unshipped.txt` is missing a public member
- `total: 0` in OTel test run → test binary not built; run `dotnet build` first
- `ActivityListener` captures 0 Activities → `CupelActivitySource.Source` is not being started; check that `AddSource("Wollax.Cupel")` is in the listener's `ShouldListenTo` filter
- `cupel.exclusion` Events missing on StageAndExclusions test → `AddEvent` may have been called after `Stop()`; check ordering in `CupelOpenTelemetryTraceCollector.cs`
- Tests seeing unexpected Activity counts or wrong verbosity → parallel test contamination; verify `[NotInParallel]` is still on the test class
- `VIOLATION` from core independence check → OTel NuGet dep leaked into `Wollax.Cupel.csproj`

## Requirements Proved By This UAT

- R022 (OpenTelemetry bridge) — `Wollax.Cupel.Diagnostics.OpenTelemetry` companion package produces real `System.Diagnostics.Activity` objects at all 3 verbosity tiers (StageOnly, StageAndExclusions, Full) with exact `cupel.*` attribute names matching spec; verified by TUnit test harness with real `ActivityListener`; BCL-only dependency confirmed; cardinality warning and DryRun requirement present in README; nupkg artifact present

## Not Proven By This UAT

- Live OTel backend integration (Jaeger, Honeycomb, Aspire, OTLP collector) — verified by test harness only; real export to a backend is caller's concern
- `AddCupelInstrumentation()` fluent extension on `TracerProviderBuilder` — not implemented in this slice; callers use `AddSource(CupelActivitySource.SourceName)` directly
- Consumption test via `PackageReference` from local feed — nupkg copied but not exercised with a consumption test in this slice
- Release pipeline publish (`release-dotnet.yml`) — pack/publish workflow not updated in this slice (deferred)

## Notes for Tester

- The test harness requires `[NotInParallel]` on the test class because `ActivitySource.AddActivityListener` is process-global. If you run tests interactively in an IDE that uses parallel test execution, you may see spurious failures from cross-contamination between test cases. Run tests via `dotnet test` (sequential per class) for reliable results.
- `Complete(report, budget)` emits Activities retroactively using calculated start times based on `UtcNow - sum(stage durations)`. Activities appear in OTel backends with correct relative timing but the absolute start time is an approximation.
- `CupelActivitySource.Source` is static and shared across all `CupelOpenTelemetryTraceCollector` instances. Only dispose when you are certain all tracing for the process is complete.
