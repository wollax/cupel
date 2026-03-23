# S05: OTel Bridge Companion Package

**Goal:** `Wollax.Cupel.Diagnostics.OpenTelemetry` NuGet companion package ships and produces real Activity/Event output at all three verbosity tiers (StageOnly, StageAndExclusions, Full) with exact `cupel.*` attribute names matching the spec, verified by a TUnit test harness.
**Demo:** A TUnit test in `Wollax.Cupel.Diagnostics.OpenTelemetry.Tests` registers an `ActivityListener`, calls `pipeline.DryRun(items)`, then calls `tracer.Complete(result.Report!, budget)`, and asserts that: the root Activity is `"cupel.pipeline"`, five child Activities are `"cupel.stage.classify"` through `"cupel.stage.place"`, StageAndExclusions tier emits `cupel.exclusion` Events, and Full tier emits `cupel.item.included` Events.

## Must-Haves

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — `IsPackable=true`, `ProjectReference` to Wollax.Cupel, PublicApiAnalyzers, PublicAPI.Shipped.txt + PublicAPI.Unshipped.txt
- `CupelVerbosity` enum with `StageOnly`, `StageAndExclusions`, `Full`
- `CupelOpenTelemetryTraceCollector` implements `ITraceCollector` and `IDisposable`; `IsEnabled` returns `true`; `RecordStageEvent` buffers stage data; `RecordItemEvent` is a no-op (timing-only path not used)
- `Complete(SelectionReport?, ContextBudget)` creates all Activities retroactively from buffered stage data using BCL `ActivitySource` (no OpenTelemetry.Api NuGet dependency)
- `ActivitySource` name is exactly `"Wollax.Cupel"`; helper constant `CupelActivitySource.SourceName = "Wollax.Cupel"` exported so callers can use it in their `AddSource()` call
- StageOnly tier: root `cupel.pipeline` Activity with `cupel.budget.max_tokens` + `cupel.verbosity`; 5 stage Activities each with `cupel.stage.name`, `cupel.stage.item_count_in`, `cupel.stage.item_count_out`
- StageAndExclusions tier: all StageOnly attributes + `cupel.exclusion.count` on stage Activity + one `cupel.exclusion` Event per excluded item with `cupel.exclusion.reason`, `cupel.exclusion.item_kind`, `cupel.exclusion.item_tokens`; ExclusionReason→PipelineStage internal mapping
- Full tier: all StageAndExclusions data + one `cupel.item.included` Event per included item on the Place stage Activity with `cupel.item.kind`, `cupel.item.tokens`, `cupel.item.score`
- `PublicAPI.Unshipped.txt` populated to silence all RS0016 errors (two-pass build workflow)
- `Cupel.slnx` updated with both new projects
- `dotnet pack` produces `./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg`; artifact copied to `./packages`
- `dotnet test` (full solution) passes with all new tests included

## Proof Level

- This slice proves: integration — real `System.Diagnostics.Activity` objects are produced with correct attribute names and values; verified via `ActivityListener` in a TUnit test harness
- Real runtime required: no (test harness is sufficient; no live OTel backend needed)
- Human/UAT required: no

## Verification

```bash
# All new tests pass
dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj

# Full solution green
dotnet test

# Package artifact exists
ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg

# ActivitySource name correct in source
grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs

# PublicAPI compliance: no RS0016 errors
dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj 2>&1 | grep "RS0016" | wc -l
# → 0

# Wollax.Cupel core has no OTel reference
grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj && echo "VIOLATION" || echo "OK"
```

Test file: `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`
- Tests cover all 3 verbosity tiers (StageOnly, StageAndExclusions, Full)
- Each test registers `ActivityListener`, calls `DryRun`, calls `Complete`, asserts Activity/Event structure
- Failure-path tests: `Complete()` with null report + Full verbosity (graceful degradation or clear error)

## Observability / Diagnostics

- Runtime signals: `System.Diagnostics.Activity` objects with exact `cupel.*` attribute names (inspectable via `ActivityListener.ActivityStopped` callback in tests); `SelectionReportAssertionException` surfaces assertion failures
- Inspection surfaces: `CupelOpenTelemetryTraceCollector` exposes `_stages` buffer state (internal, inspectable in tests via `GetType().GetField(...)` if needed); `dotnet test -v n` shows all TUnit test names and pass/fail
- Failure visibility: Activities returned as `null` when no listener is registered → test will assert non-null immediately and fail with "expected non-null root Activity"; RS0016 build error lists missing PublicAPI members by name
- Redaction constraints: none (no secrets or PII in OTel trace attributes; item content is never emitted — only kind, token count, score)

## Integration Closure

- Upstream surfaces consumed: `ITraceCollector` from `Wollax.Cupel.Diagnostics`, `SelectionReport`/`ExcludedItem`/`IncludedItem`/`ExclusionReason` from `Wollax.Cupel.Diagnostics`, `ContextBudget.MaxTokens` from `Wollax.Cupel`, `CupelPipeline.DryRun()` used in test harness
- New wiring introduced in this slice: `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` package csproj with `ProjectReference` to `Wollax.Cupel`; `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` with `ProjectReference` to both; both added to `Cupel.slnx`; `dotnet pack` output to `./nupkg` + copy to `./packages`
- What remains before the milestone is truly usable end-to-end: S06 (budget simulation + tiebreaker + spec alignment)

## Tasks

- [ ] **T01: Create package + test project scaffolding; implement StageOnly tier** `est:25m`
  - Why: Establishes the package project structure (csproj, PublicAPI files, ActivitySource), the `CupelVerbosity` enum, and `CupelOpenTelemetryTraceCollector` with the `RecordStageEvent` buffer + `Complete()` producing correct root + stage Activities for the StageOnly tier. Creates the test project with a first passing test.
  - Files: `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt`, `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`, `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`, `Cupel.slnx`
  - Do: (1) Create `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` — clone `Wollax.Cupel.Testing.csproj` structure exactly (IsPackable=true, ProjectReference to Wollax.Cupel, PublicApiAnalyzers PrivateAssets="All", AdditionalFiles for PublicAPI files); add Description element. (2) Create `CupelVerbosity.cs` enum with `StageOnly=0, StageAndExclusions=1, Full=2`. (3) Create `CupelActivitySource.cs` with `public static class CupelActivitySource { public const string SourceName = "Wollax.Cupel"; internal static readonly ActivitySource Source = new(SourceName); }`. (4) Create `CupelOpenTelemetryTraceCollector.cs`: `public sealed class CupelOpenTelemetryTraceCollector : ITraceCollector, IDisposable`; field `_verbosity`; field `_stages = new List<(PipelineStage Stage, TimeSpan Duration, int ItemCountOut)>()`; `IsEnabled = true`; `RecordStageEvent` appends to `_stages`; `RecordItemEvent` is no-op; `Complete(SelectionReport? report, ContextBudget budget)` creates root Activity then 5 stage Activities retroactively (StageOnly logic only in T01). Root attributes: `cupel.budget.max_tokens = budget.MaxTokens`, `cupel.verbosity = _verbosity.ToString()`. Stage attributes: `cupel.stage.name`, `cupel.stage.item_count_in` (previous stage count out; Classify uses TotalCandidates if report non-null else ItemCountOut), `cupel.stage.item_count_out`. Dispose() disposes `CupelActivitySource.Source`. (5) Create `PublicAPI.Shipped.txt` with only `#nullable enable`; create `PublicAPI.Unshipped.txt` empty initially. (6) Create test project csproj (clone Wollax.Cupel.Testing.Tests pattern: OutputType=Exe, IsPackable=false, TUnit PackageReference, ProjectReference to the new package and to Wollax.Cupel). (7) Write `CupelOpenTelemetryTraceCollectorTests.cs` with a StageOnly test: register `ActivityListener`, create a minimal pipeline, call `DryRun`, call `tracer.Complete(result.Report!, budget)`, assert root Activity name = `"cupel.pipeline"`, assert 5 child Activities, assert `cupel.stage.name` attribute on classify stage = `"classify"`. (8) Update `Cupel.slnx` to add both new projects under appropriate folders.
  - Verify: `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/` builds (may have RS0016 errors — expected, resolved in T03); `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` → StageOnly test passes
  - Done when: StageOnly test passes; package compiles; `CupelActivitySource.SourceName == "Wollax.Cupel"` confirmed by grep

- [ ] **T02: Implement StageAndExclusions + Full tiers; complete test coverage** `est:25m`
  - Why: Extends `Complete()` with exclusion event emission (StageAndExclusions) and inclusion event emission (Full), implementing the ExclusionReason→PipelineStage mapping. Adds tests covering all 3 tiers including failure-path (null report + Full verbosity).
  - Files: `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs`, `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs`
  - Do: (1) Add `private static PipelineStage MapReasonToStage(ExclusionReason reason)` internal static method with the mapping from the research doc (NegativeTokens→Classify, Deduplicated→Deduplicate, ScoredTooLow→Score, Filtered→Classify, all others→Slice as fallback). (2) In `Complete()`, when `_verbosity >= StageAndExclusions && report != null`: for each stage Activity, collect `report.Excluded` items whose `MapReasonToStage(reason) == stage`; set `cupel.exclusion.count` attribute on the stage Activity; add `cupel.exclusion` Events (before stopping the Activity) with `cupel.exclusion.reason = item.Reason.ToString()`, `cupel.exclusion.item_kind = item.Item.Kind.ToString()`, `cupel.exclusion.item_tokens = item.Item.Tokens()`. (3) In `Complete()`, when `_verbosity >= Full && report != null`: after creating the Place stage Activity (but before stopping it), add one `cupel.item.included` Event per `report.Included` item with `cupel.item.kind`, `cupel.item.tokens`, `cupel.item.score` (use `score.ToString("G17")` or double overload). (4) Verify Activity-then-Events-then-Stop ordering (critical: events added to a stopped Activity are silently dropped). (5) Add test: StageAndExclusions tier — verify `cupel.exclusion.count` tag on at least one stage Activity; verify at least one `cupel.exclusion` Event has `cupel.exclusion.reason` matching a known `ExclusionReason` string. (6) Add test: Full tier — verify `cupel.item.included` Events present on the Place Activity; verify `cupel.item.score` attribute parses as double. (7) Add failure-path test: `Complete(null, budget)` with `StageOnly` verbosity still produces Activities (no crash); `Complete(null, budget)` with `Full` verbosity produces StageOnly-level Activities only (null report → skip per-item events gracefully, no exception).
  - Verify: `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` → all tier tests pass (StageOnly + StageAndExclusions + Full + null-report graceful)
  - Done when: All 4+ test cases pass; `dotnet test` shows no new failures in the rest of the solution

- [ ] **T03: Populate PublicAPI, write README, pack, wire solution** `est:15m`
  - Why: Completes the release-readiness gate: PublicAPI.Unshipped.txt populated (silences RS0016), README written with cardinality warning, package packed, artifact copied to `./packages`, full solution green.
  - Files: `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt`, `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md`, `.kata/DECISIONS.md`
  - Do: (1) Run `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/` and capture RS0016 errors; populate `PublicAPI.Unshipped.txt` with every listed public member (including `CupelVerbosity` enum values and the `CupelActivitySource.SourceName` constant). (2) Rebuild to confirm 0 RS0016 errors and 0 warnings (TreatWarningsAsErrors is project-wide). (3) Write `README.md` in the package directory: include `AddSource("Wollax.Cupel")` usage example, pre-stability disclaimer (per spec), verbosity tier descriptions, cardinality warning for Full verbosity, note that `DryRun()` must be used (not `Execute()`) for StageAndExclusions and Full tiers. (4) Run `dotnet pack src/Wollax.Cupel.Diagnostics.OpenTelemetry/ --output ./nupkg` and confirm `.nupkg` file produced. (5) Copy `.nupkg` to `./packages` (per D095): `cp ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg ./packages/`. (6) Run `dotnet test` (full solution) and confirm all pass. (7) Append new decisions (D097–D100) to `.kata/DECISIONS.md`.
  - Verify: `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/ 2>&1 | grep RS0016 | wc -l` → 0; `ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` → file found; `dotnet test` → full solution green
  - Done when: `dotnet build` 0 errors 0 warnings; `.nupkg` in `./nupkg`; `dotnet test` full solution passes

## Files Likely Touched

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` (new)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelVerbosity.cs` (new)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` (new)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` (new)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Shipped.txt` (new)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/PublicAPI.Unshipped.txt` (new)
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` (new)
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` (new)
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` (new)
- `Cupel.slnx`
- `.kata/DECISIONS.md`
