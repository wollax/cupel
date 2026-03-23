# S05: OTel Bridge Companion Package — UAT

**Milestone:** M003
**Written:** 2026-03-23

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: the slice is a library/package integration with no UI and no long-running service. Its success criteria are fully observable through real `System.Diagnostics.Activity` capture in a TUnit harness, build/package checks, and dependency-boundary checks. A live Jaeger/Honeycomb/Aspire backend would only test caller-owned exporter wiring, not the Cupel bridge contract itself.

## Preconditions

- Working directory: `/Users/wollax/Git/personal/cupel`
- .NET 10 SDK installed
- Solution restore succeeds (`dotnet restore` already done or can be rerun)
- The repo contains the packed artifact in `./nupkg/` and the copied local-feed artifact in `./tests/Wollax.Cupel.ConsumptionTests/packages/`

## Smoke Test

```bash
rtk proxy dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj
```

**Expected:** `total: 4, failed: 0, succeeded: 4`

## Test Cases

### 1. All OTel verbosity tiers emit the expected Activity/Event structure

1. Run:
   ```bash
   rtk proxy dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj -v n
   ```
2. Confirm these tests all pass:
   - `StageOnly_ProducesRootAndFiveStageActivities`
   - `StageAndExclusions_ProducesExclusionEvents`
   - `Full_ProducesIncludedItemEvents`
   - `NullReport_GracefulDegradation`
3. **Expected:** the harness captures a root `cupel.pipeline` Activity, five child stage Activities, exclusion events in StageAndExclusions, included-item events in Full, and no crash when `Complete(null, budget)` is used.

### 2. Full solution remains green after the slice lands

1. Run:
   ```bash
   rtk proxy dotnet test
   ```
2. **Expected:** `total: 712, failed: 0, succeeded: 712`

### 3. Package artifact is present and consumable from the local feed

1. Run:
   ```bash
   rtk ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
   ```
2. Run:
   ```bash
   rtk ls ./tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg
   ```
3. **Expected:** both commands list the same package version, proving pack output exists and was copied into the local-feed directory.

### 4. Public API and dependency boundaries remain correct

1. Run:
   ```bash
   rtk proxy dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj 2>&1 | grep "RS0016" | wc -l
   ```
2. Run:
   ```bash
   rtk grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs
   ```
3. Run:
   ```bash
   rtk grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo VIOLATION || echo OK
   ```
4. **Expected:** `0`, then a `SourceName = "Wollax.Cupel"` match, then `OK`.

## Edge Cases

### Null report with Full verbosity

1. Run:
   ```bash
   rtk proxy dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj --filter "NullReport"
   ```
2. **Expected:** the test passes; Activities are still produced, but per-item events are skipped gracefully.

### Parallel-test contamination regression

1. Run the OTel test project normally.
2. If failures appear with inflated Activity counts or the wrong verbosity observed, inspect `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`.
3. **Expected:** the test class remains marked `[NotInParallel]`; otherwise global `ActivityListener` state can cross-contaminate cases.

## Failure Signals

- `RS0016` count > 0 → `PublicAPI.Unshipped.txt` is out of sync with the compiled public surface
- OTel test project reports fewer than 4 tests or any failure → the bridge contract regressed
- No `.nupkg` in `./nupkg/` or local feed → package packing/copy step regressed
- Core project check prints `VIOLATION` → OpenTelemetry dependencies leaked into `Wollax.Cupel`
- StageAndExclusions or Full tests observe zero events when Activities exist → event emission order likely regressed (`AddEvent()` after `Stop()` is silently dropped)
- Unexpected cross-test Activity counts → `[NotInParallel]` was removed or bypassed

## Requirements Proved By This UAT

- R022 — proves that `Wollax.Cupel.Diagnostics.OpenTelemetry` exists as a companion package, emits real BCL `Activity`/`ActivityEvent` output at all three verbosity tiers with the spec-defined `cupel.*` attribute names, keeps the core package free of OTel dependencies, and produces a packable local-feed artifact

## Not Proven By This UAT

- Live export into a specific backend such as Jaeger, Honeycomb, Aspire, or OTLP collector
- An `AddCupelInstrumentation()` convenience extension on `TracerProviderBuilder` (not implemented in this slice)
- Release workflow publishing changes in `release-dotnet.yml`
- End-to-end installation by an external sample app outside this repo; this UAT proves package creation and local-feed availability, not a separate consumer repository

## Notes for Tester

- The bridge is intentionally BCL-only. Consumers integrate by adding/listening to `CupelActivitySource.SourceName`; no `OpenTelemetry.Api` dependency is required in the Cupel package itself.
- The test harness uses `Execute(items, tracer)` plus `DryRun(items)` before `Complete(report, budget)`. That pattern is intentional because `DryRun()` creates its own internal diagnostic collector and cannot directly populate the OTel bridge buffer.
- `Dispose()` tears down a shared static `ActivitySource`. In normal one-shot usage that is fine, but long-lived multi-instance scenarios should coordinate disposal carefully.
