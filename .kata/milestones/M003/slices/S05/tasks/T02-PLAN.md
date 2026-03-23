---
estimated_steps: 7
estimated_files: 2
---

# T02: Implement StageAndExclusions + Full tiers; complete test coverage

**Slice:** S05 — OTel Bridge Companion Package
**Milestone:** M003

## Description

Extends `CupelOpenTelemetryTraceCollector.Complete()` with the two richer verbosity tiers:

- **StageAndExclusions**: adds `cupel.exclusion.count` attribute on each stage Activity and one `cupel.exclusion` Event per excluded item mapped to that stage.
- **Full**: adds one `cupel.item.included` Event per included item on the Place stage Activity, with kind, tokens, and score.

The ExclusionReason→PipelineStage mapping is internal to the bridge. After this task, TUnit tests cover all 3 tiers plus the graceful null-report fallback.

**Critical ordering constraint:** Events must be added to an Activity BEFORE stopping it. The T01 structure creates stageActivity → sets tags → stops it. T02 must insert event-adding code between the "set stage tags" step and the `SetEndTime`/`Stop()` call.

## Steps

1. **Add `MapReasonToStage` private static method** to `CupelOpenTelemetryTraceCollector`:
   ```csharp
   private static PipelineStage MapReasonToStage(ExclusionReason reason) => reason switch
   {
       ExclusionReason.NegativeTokens => PipelineStage.Classify,
       ExclusionReason.Filtered => PipelineStage.Classify,
       ExclusionReason.ScoredTooLow => PipelineStage.Score,
       ExclusionReason.Deduplicated => PipelineStage.Deduplicate,
       _ => PipelineStage.Slice  // BudgetExceeded, PinnedOverride, CountCapExceeded, QuotaCapExceeded, QuotaRequireDisplaced, CountRequireCandidatesExhausted, unknown future
   };
   ```

2. **Extend `Complete()` for StageAndExclusions tier.** In the stage loop, after setting stage tags and before `SetEndTime`/`Stop()`:
   - If `_verbosity >= CupelVerbosity.StageAndExclusions && report != null`:
     - Collect `stageExclusions = report.Excluded.Where(e => MapReasonToStage(e.Reason) == stage).ToList()`
     - `stageActivity?.SetTag("cupel.exclusion.count", stageExclusions.Count)`
     - For each `excluded` in `stageExclusions`: `stageActivity?.AddEvent(new ActivityEvent("cupel.exclusion", tags: new ActivityTagsCollection { ["cupel.exclusion.reason"] = excluded.Reason.ToString(), ["cupel.exclusion.item_kind"] = excluded.Item.Kind().ToString(), ["cupel.exclusion.item_tokens"] = excluded.Item.Tokens() }))`
   - If report is null and verbosity >= StageAndExclusions: skip per-item events (no crash, no exception); activities still created with stage-level data

3. **Extend `Complete()` for Full tier.** In the stage loop, for the Place stage specifically, after exclusion events and before `SetEndTime`/`Stop()`:
   - If `_verbosity >= CupelVerbosity.Full && report != null && stage == PipelineStage.Place`:
     - For each `included` in `report.Included`: `stageActivity?.AddEvent(new ActivityEvent("cupel.item.included", tags: new ActivityTagsCollection { ["cupel.item.kind"] = included.Item.Kind().ToString(), ["cupel.item.tokens"] = included.Item.Tokens(), ["cupel.item.score"] = included.Score }))`
   - Note: `cupel.item.score` is a `float64` — use the `double` value directly (ActivityTagsCollection accepts object, boxing a double is correct)

4. **Add StageAndExclusions test** in `CupelOpenTelemetryTraceCollectorTests.cs`:
   - Build a pipeline with enough items that at least one exclusion occurs (e.g., 3 items, budget that only fits 1-2)
   - Use `CupelVerbosity.StageAndExclusions`; call `DryRun`, then `Complete(result.Report!, budget)`
   - Assert: the Slice stage Activity has `cupel.exclusion.count` tag ≥ 1
   - Assert: at least one captured Activity has at least one `cupel.exclusion` event with `cupel.exclusion.reason` equal to a known `ExclusionReason` name (e.g., `"BudgetExceeded"`)

5. **Add Full tier test**:
   - Use `CupelVerbosity.Full`; pipeline with items that get included
   - Assert: the Place stage Activity has at least one `cupel.item.included` event
   - Assert: the `cupel.item.score` attribute value is parseable as a `double`

6. **Add null-report graceful degradation test**:
   - Create `new CupelOpenTelemetryTraceCollector(CupelVerbosity.Full)` but do NOT call `Complete()` with a real report — use `Complete(null, budget)` after manually faking the stage buffer by calling `RecordStageEvent` 5 times with dummy `TraceEvent`s
   - Assert: no exception thrown; root Activity exists; 5 stage Activities exist; no `cupel.item.included` events (since report is null)

7. **Verify correct event-add-then-stop ordering** by reviewing the Complete() method: confirm Events are added inside the `using` block or before explicit `Stop()` call, not after.

## Must-Haves

- [ ] `MapReasonToStage` private static method covers all 10 ExclusionReason values with Slice as fallback for unknown
- [ ] `cupel.exclusion.count` tag set on stage Activity before the Activity is stopped
- [ ] `cupel.exclusion` Events added to stage Activity BEFORE `SetEndTime`/`Stop()`
- [ ] `cupel.item.included` Events added to Place stage Activity BEFORE `SetEndTime`/`Stop()`
- [ ] `cupel.exclusion.reason` uses `.ToString()` (canonical variant name), not numeric cast
- [ ] `cupel.item.score` uses the `double` value (float64 per spec), not cast to `float`
- [ ] `Complete(null, budget)` with Full verbosity: no exception; stage Activities still created; per-item events skipped
- [ ] StageAndExclusions test asserts `cupel.exclusion.count` ≥ 1 on at least one stage Activity
- [ ] Full tier test asserts at least one `cupel.item.included` event on Place Activity
- [ ] Null-report fallback test passes

## Verification

- `dotnet test --project tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj` → all tests pass (StageOnly from T01 + 3 new tests from T02)
- `dotnet test` (full solution) → all existing tests still pass (no regressions)
- `grep 'Stop\(\)\|SetEndTime' src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — `SetEndTime` and `Stop()` appear after any `AddEvent` calls in the stage loop

## Observability Impact

- Signals added/changed: `cupel.exclusion` Events and `cupel.exclusion.count` tag visible in StageAndExclusions+ tiers; `cupel.item.included` Events visible in Full tier — these are the signals that differentiate the three verbosity tiers in a live OTel backend
- How a future agent inspects this: collected Activities in TUnit tests expose `.Events` and `.Tags` properties; test failures show which specific event/tag was missing or wrong
- Failure state exposed: if event is added after Activity is stopped, `AddEvent` is a no-op — test will fail "expected at least 1 event, got 0" which is a clear signal of the ordering bug

## Inputs

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` from T01 — the StageOnly implementation to extend
- `src/Wollax.Cupel/Diagnostics/ExclusionReason.cs` — 10 variants; Slice is fallback for all knapsack/quota/count reasons
- `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs` — `Item`, `Score`, `Reason` fields; no Stage field (hence the mapping)
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` — `Item`, `Score`, `Reason` fields
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` from T01 — existing StageOnly test file to extend
- S05-RESEARCH.md: ExclusionReason→PipelineStage mapping table; "Activity created after stopped → AddEvent no-op" pitfall

## Expected Output

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — extended with StageAndExclusions and Full logic (~120-140 total lines)
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — extended with 3 additional tests (StageAndExclusions, Full, null-report fallback); total ≥4 tests
