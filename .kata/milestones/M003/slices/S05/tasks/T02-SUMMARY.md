---
id: T02
parent: S05
milestone: M003
provides:
  - MapReasonToStage private method covering all 10 ExclusionReason values (Slice fallback for budget/quota/count reasons)
  - StageAndExclusions tier in Complete() — cupel.exclusion.count tag + cupel.exclusion Events before Stop()
  - Full tier in Complete() — cupel.item.included Events on Place stage Activity before Stop()
  - Null-report graceful degradation — Complete(null, budget) with Full verbosity produces Activities without crashing
  - 3 new TUnit tests (StageAndExclusions, Full, NullReport) + [NotInParallel] isolation for all 4 tests
key_files:
  - src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs
  - tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs
key_decisions:
  - "[NotInParallel] on test class required because ActivityListener is global — all listeners capture Activities from all ActivitySources in the process; parallel tests cross-contaminate each other's captured Activity lists"
  - "ExclusionReason→PipelineStage mapping: NegativeTokens+Filtered→Classify, ScoredTooLow→Score, Deduplicated→Deduplicate, all others (BudgetExceeded, PinnedOverride, CountCapExceeded, QuotaCapExceeded, QuotaRequireDisplaced, CountRequireCandidatesExhausted)→Slice"
  - "cupel.item.score stored as double (object-boxed) in ActivityTagsCollection — boxing a double is correct per spec (float64); tests verify scoreTag.Value is double"
  - "AddEvent must precede SetEndTime/Stop — events added after Stop are silently dropped; test failure would show 0 events, not an exception"
patterns_established:
  - "Event-before-Stop ordering: AddEvent calls in Complete() are always inside the stage block before SetEndTime/Stop(); verified by grep"
  - "ActivityListener isolation: [NotInParallel] on any test class that captures from a globally-shared ActivitySource"
observability_surfaces:
  - "Activity.Events on each stage Activity expose cupel.exclusion and cupel.item.included events with typed tags; inspectable via ActivityListener.ActivityStopped in tests or live OTel backend"
  - "cupel.exclusion.count integer tag on each stage Activity gives O(1) cardinality check without enumerating events"
duration: 20m
verification_result: passed
completed_at: 2026-03-23T00:00:00Z
blocker_discovered: false
---

# T02: Implement StageAndExclusions + Full tiers; complete test coverage

**Extended Complete() with exclusion + inclusion event emission across all three verbosity tiers; 4 TUnit tests now cover StageOnly, StageAndExclusions, Full, and null-report fallback.**

## What Happened

Extended `CupelOpenTelemetryTraceCollector.Complete()` with two richer verbosity tiers:

**StageAndExclusions**: Added `MapReasonToStage()` private method mapping all 10 `ExclusionReason` values to the stage that produced them. In the stage loop, after setting base tags and before `SetEndTime`/`Stop()`, the code sets `cupel.exclusion.count` on the stage Activity and emits one `cupel.exclusion` ActivityEvent per excluded item with reason, kind, and token count.

**Full tier**: On the Place stage specifically, after exclusion events and before `Stop()`, emits one `cupel.item.included` ActivityEvent per `report.Included` item with kind, tokens, and score (as `double`).

Both tiers gracefully skip per-item events when `report` is `null` — the Activities are still created with stage-level data, but no events are added.

The tests discovered a significant issue: TUnit runs tests in parallel by default and `ActivitySource.AddActivityListener` registers into a global listener list. All listeners receive Activities from all sources in the process. Tests were cross-contaminating each other's captured lists, producing failures like "expected verbosity=StageOnly but got Full" and "expected 5 stage Activities but found 20". Fixed by adding `[NotInParallel]` to the test class.

## Verification

- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/` → 4/4 passed
- `dotnet test` (full solution) → 712/712 passed
- `ls ./nupkg/Wollax.Cupel.Diagnostics.OpenTelemetry.*.nupkg` → `Wollax.Cupel.Diagnostics.OpenTelemetry.0.0.0-alpha.0.78.nupkg` present
- `grep '"Wollax.Cupel"' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelActivitySource.cs` → SourceName confirmed
- `dotnet build ... | grep RS0016 | wc -l` → 0
- `grep -r "OpenTelemetry" src/Wollax.Cupel/Wollax.Cupel.csproj` → OK (no violation)
- `grep 'Stop\(\)\|SetEndTime' CupelOpenTelemetryTraceCollector.cs` → SetEndTime/Stop appear after all AddEvent calls

## Diagnostics

- Stage Activities with `cupel.exclusion.count` tag and `cupel.exclusion` / `cupel.item.included` events are visible via `ActivityListener.ActivityStopped` callback — collect into `List<Activity>` and inspect `.Events` and `.Tags`
- If an event shows 0 count when expected non-zero: check that `AddEvent` is called before `Stop()` (silent no-op after stop); check that `report` is non-null; check that `_verbosity` is at the expected tier
- Test isolation failure mode: if tests start seeing unexpected Activity counts or wrong verbosity values, check `[NotInParallel]` is still present on the class

## Deviations

- Plan referenced `excluded.Item.Kind().ToString()` and `excluded.Item.Tokens()` as method calls; these are properties (`Kind` and `Tokens`), not methods. Used `.Kind.ToString()` and `.Tokens` directly.
- Added `[NotInParallel]` to the test class — not mentioned in the task plan but required to prevent ActivityListener cross-contamination from parallel test execution.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — Added MapReasonToStage, StageAndExclusions tier, Full tier; ~155 total lines
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — Added [NotInParallel], CreateListener helper, 3 new tests (StageAndExclusions, Full, NullReport); 4 tests total
