---
id: T03
parent: S05
milestone: M003
provides:
  - PublicAPI.Unshipped.txt populated with all 12 public member signatures (RS0016 = 0)
  - README.md with pre-stability disclaimer, verbosity tier table, cardinality warning, DryRun requirement, AddSource usage snippet, Dispose behaviour note
  - ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg packed and present
  - ./tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg copied to local feed
  - D097–D100 appended to DECISIONS.md
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md
  - .kata/DECISIONS.md
key_decisions:
  - "D097: BCL-only ActivitySource; no OpenTelemetry.Api NuGet dep; callers use AddSource(CupelActivitySource.SourceName) directly"
  - "D098: S05 integration-level verification via real ActivityListener in TUnit tests; no live OTel backend needed"
  - "D099: Complete(null, budget) graceful degradation — stage Activities produced, per-item events silently skipped"
  - "D100: Dispose() disposes static ActivitySource; multi-instance callers must not Dispose() until all tracing is complete"
patterns_established:
  - "Two-pass RS0016 workflow: build → grep RS0016 → populate PublicAPI.Unshipped.txt → rebuild clean"
  - "Local feed copy target is ./tests/Wollax.Cupel.ConsumptionTests/packages/ (nuget.config uses ./packages relative to that dir)"
observability_surfaces:
  - "dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l → 0 confirms PublicAPI compliance"
  - "ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg → confirms artifact"
  - "ls ./tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg → confirms local feed"
duration: 10min
verification_result: passed
completed_at: 2026-03-23
blocker_discovered: false
---

# T03: Populate PublicAPI, write README, pack, wire solution

**PublicAPI.Unshipped.txt fully populated (0 RS0016 errors), README written with spec-aligned cardinality table + DryRun requirement, nupkg packed and copied to local ConsumptionTests feed; 712/712 tests green.**

## What Happened

All three release-readiness steps completed cleanly:

1. **PublicAPI.Unshipped.txt** was already populated in the previous session (D097 two-pass workflow: build → capture RS0016 errors → fill file → rebuild clean). Rebuild confirms 0 errors, 0 warnings.

2. **README.md** written at `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` covering: pre-stability disclaimer (matching spec verbatim), verbosity tier table (StageOnly/StageAndExclusions/Full with recommended environments), cardinality warning for Full tier, `AddSource(CupelActivitySource.SourceName)` usage snippet, DryRun requirement note explaining why `Execute()` returns null report, and Dispose() behaviour note for multi-instance callers.

3. **nupkg** already present from previous session at `./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg`. Copied to `./tests/Wollax.Cupel.ConsumptionTests/packages/` — the actual local feed path per nuget.config (`./packages` is relative to that directory, not the repo root; D095 description was repo-root-relative shorthand).

4. **Decisions D097–D100** already appended in the previous session.

## Verification

- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l` → **0**
- `ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` → **found** (0.0.0-alpha.0.78)
- `ls ./tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` → **found** (0.0.0-alpha.0.78)
- `dotnet test` → **712 passed, 0 failed**
- `grep "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo VIOLATION || echo OK` → **OK**
- `grep "DryRun" src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` → **match**
- README contains `AddSource(CupelActivitySource.SourceName)` → **confirmed**
- README contains pre-stability disclaimer → **confirmed**
- README contains cardinality warning → **confirmed**

## Diagnostics

- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016` — lists any missing PublicAPI entries by exact member signature
- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` — shows all 4 TUnit tests with pass/fail
- `ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` — confirms artifact existence

## Deviations

- **Local feed path**: Task plan said copy to `./packages` (repo root). Actual path is `./tests/Wollax.Cupel.ConsumptionTests/packages/` — the nuget.config in the ConsumptionTests project uses `./packages` relative to its own directory. D095 "Consumption test local NuGet feed is ./packages" is repo-root shorthand; the actual path is the ConsumptionTests subdirectory.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — new; usage, disclaimer, verbosity tiers, cardinality warning, DryRun requirement, Dispose behaviour
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` — populated with 12 public member signatures (done in prior session, confirmed clean this session)
- `tests/Wollax.Cupel.ConsumptionTests/packages/Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg` — copied for local feed
- `.kata/DECISIONS.md` — D097–D100 appended (done in prior session)
