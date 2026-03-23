---
estimated_steps: 7
estimated_files: 4
---

# T03: Populate PublicAPI, write README, pack, wire solution

**Slice:** S05 — OTel Bridge Companion Package
**Milestone:** M003

## Description

Completes the release-readiness gate for the `Wollax.Cupel.Diagnostics.OpenTelemetry` package:

1. Populate `PublicAPI.Unshipped.txt` via the two-pass RS0016 workflow (build → capture errors → fill file → rebuild clean)
2. Write the package README with usage snippet, pre-stability disclaimer, verbosity tier table, cardinality warning, and DryRun requirement note
3. Pack the package and copy artifact to `./packages` for local feed consumption
4. Run full solution test suite to confirm nothing is broken
5. Append new decisions to `.kata/DECISIONS.md`

This task has no new code to write — it is purely the release-readiness and documentation completion step. After T03, the slice is fully done.

## Steps

1. **Two-pass PublicAPI population.** Run:
   ```bash
   dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj 2>&1 | grep RS0016
   ```
   For each RS0016 error, copy the public member signature into `PublicAPI.Unshipped.txt`. Expected members include at minimum:
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity` (enum declaration)
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity.StageOnly = 0 -> Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity.StageAndExclusions = 1 -> Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity.Full = 2 -> Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelActivitySource` (class declaration)
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelActivitySource.SourceName -> string`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector` (class declaration)
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector.CupelOpenTelemetryTraceCollector(Wollax.Cupel.Diagnostics.OpenTelemetry.CupelVerbosity verbosity = ...) -> void`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector.IsEnabled.get -> bool`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector.RecordStageEvent(Wollax.Cupel.Diagnostics.TraceEvent traceEvent) -> void`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector.RecordItemEvent(Wollax.Cupel.Diagnostics.TraceEvent traceEvent) -> void`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector.Complete(Wollax.Cupel.Diagnostics.SelectionReport? report, Wollax.Cupel.ContextBudget budget) -> void`
   - `Wollax.Cupel.Diagnostics.OpenTelemetry.CupelOpenTelemetryTraceCollector.Dispose() -> void`
   Rebuild to confirm `0` RS0016 errors and `0` warnings.

2. **Write `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md`** with these sections:
   - **Overview**: companion package for Cupel pipeline → OpenTelemetry bridge; zero-dep on OTel SDK; uses BCL `System.Diagnostics.ActivitySource`
   - **Usage** (C# snippet): `new CupelOpenTelemetryTraceCollector(CupelVerbosity.StageOnly)`; `pipeline.DryRun(items)`; `tracer.Complete(result.Report!, budget)`; registering `AddSource(CupelActivitySource.SourceName)` in the OTel builder
   - **Pre-stability disclaimer**: `cupel.*` attribute names are pre-stable; do not hard-code in dashboards or alerts
   - **Verbosity tiers**: table of StageOnly/StageAndExclusions/Full with recommended environments
   - **Cardinality warning**: Full verbosity can emit up to ~1000 events/run; do not enable in production without sampling
   - **Important**: DryRun() must be used (not Execute()) to get a non-null SelectionReport for StageAndExclusions and Full tiers; Execute() returns null report when not using DiagnosticTraceCollector

3. **Pack the package**:
   ```bash
   dotnet pack src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj --output ./nupkg
   ```
   Confirm `./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` exists.

4. **Copy to local feed** (per D095):
   ```bash
   cp ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg ./packages/
   ```

5. **Run full solution test suite**:
   ```bash
   dotnet test
   ```
   All tests must pass (708+ from prior slices + new S05 tests).

6. **Verify core package is untouched** — Wollax.Cupel must still have zero OTel references:
   ```bash
   grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo "VIOLATION" || echo "OK"
   ```

7. **Append decisions to `.kata/DECISIONS.md`**:
   - D097: BCL-only ActivitySource approach (no OpenTelemetry.Api NuGet); `AddCupelInstrumentation` extension not provided; callers use `AddSource(CupelActivitySource.SourceName)` directly
   - D098: S05 verification strategy — integration-level; real `ActivityListener` in TUnit tests captures Activities produced by `Complete()`; no live OTel backend required
   - D099: `Complete()` null-report graceful degradation for StageAndExclusions/Full — skip per-item events, still produce stage Activities; document in README as explicit behavior
   - D100: `Dispose()` disposes the static `ActivitySource` — callers who create multiple instances should not call Dispose() until done; document this in README or XML doc

## Must-Haves

- [ ] `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/` → 0 errors, 0 warnings
- [ ] `PublicAPI.Unshipped.txt` lists all public members (RS0016 count = 0 after population)
- [ ] `README.md` contains `AddSource(CupelActivitySource.SourceName)` usage, pre-stability disclaimer, cardinality warning, and DryRun requirement
- [ ] `./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` exists after pack
- [ ] `./packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` exists (copied for local feed)
- [ ] `dotnet test` (full solution) → all tests pass, 0 failures
- [ ] `grep OpenTelemetry src/Wollax.Cupel/Wollax.Cupel.csproj` → no match
- [ ] D097–D100 appended to `.kata/DECISIONS.md`

## Verification

- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l` → 0
- `ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` → file found
- `ls ./packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` → file found
- `dotnet test` → full solution passes (no failures)
- `grep "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo FAIL || echo OK` → OK
- `grep "DryRun" src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` → match

## Observability Impact

- Signals added/changed: `README.md` documents cardinality characteristics; `PublicAPI.Unshipped.txt` serves as the authoritative public surface contract for the package (RS0016 error on missing entry = immediate build signal)
- How a future agent inspects this: `dotnet build 2>&1 | grep RS0016` → lists any missing PublicAPI entries; `dotnet test` → all tests visible with names; `ls ./nupkg/*.nupkg` → confirms artifact existence
- Failure state exposed: RS0016 error lists the exact missing member name and signature; `dotnet pack` failure message includes the project path and error code

## Inputs

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` from T01+T02 — complete implementation with all public types
- `src/Wollax.Cupel.Testing/PublicAPI.Unshipped.txt` — reference example of populated PublicAPI format
- D095 — `./packages` is the correct local feed path, not `./nupkg`
- `spec/src/integrations/opentelemetry.md` — pre-stability disclaimer and cardinality table text to reference in README

## Expected Output

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — populated with all public member signatures
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — new; usage, disclaimer, cardinality warning, DryRun note
- `./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` — packed NuGet artifact
- `./packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` — local feed copy
- `.kata/DECISIONS.md` — D097–D100 appended
