---
estimated_steps: 4
estimated_files: 5
---

# T02: Add structured completion handoff in core diagnostics

**Slice:** S05 — OTel bridge companion package
**Milestone:** M003

## Description

Add the smallest additive core seam that makes the OTel bridge possible without parsing free-text trace messages. The seam must preserve existing `DiagnosticTraceCollector` behavior while giving any enabled collector structured stage snapshots, budget data, and the final `SelectionReport`.

## Steps

1. Extend `ITraceCollector` with a default no-op completion hook that receives the final `SelectionReport`, the pipeline budget, and a new stage snapshot model suitable for post-run bridge emission.
2. Add `StageTraceSnapshot` (or equivalent) to capture stage name, item-count-in, item-count-out, and precise timing data needed to build `Activity` spans after execution completes.
3. Update `CupelPipeline.ExecuteCore` to accumulate stage snapshots, build `SelectionReport` for enabled collectors (not only `DiagnosticTraceCollector`), and invoke the completion hook while keeping existing stage/item callbacks intact.
4. Update PublicAPI declarations and make the seam tests pass without regressing existing diagnostics behavior.

## Must-Haves

- [ ] The new seam is additive and backward-compatible: existing `DiagnosticTraceCollector` tests and current public entry points still behave as before.
- [ ] Enabled collectors receive structured stage counts/timing and the final `SelectionReport` without any dependence on `TraceEvent.Message` text.
- [ ] `ContextResult.Report` is populated in the enabled-collector path so downstream diagnostics/bridges can inspect the same structured report the tests assert against.

## Verification

- `dotnet test tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj --filter "FullyQualifiedName~OpenTelemetryReportSeamTests|FullyQualifiedName~DiagnosticTraceCollector|FullyQualifiedName~ExplainabilityIntegrationTests"`
- `dotnet build src/Wollax.Cupel/Wollax.Cupel.csproj`

## Observability Impact

- Signals added/changed: Structured end-of-run stage snapshots and final report handoff for enabled collectors.
- How a future agent inspects this: Inspect `ContextResult.Report`, the completion-hook arguments in seam tests, and the stage snapshot values asserted in `OpenTelemetryReportSeamTests.cs`.
- Failure state exposed: Wrong counts, missing budget handoff, missing report population, or accidental regression of existing collector behavior fails focused integration tests immediately.

## Inputs

- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — current collector contract with only stage/item event callbacks.
- `src/Wollax.Cupel/CupelPipeline.cs` — current report-building gate and stage timing/count implementation.
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — existing source of structured included/excluded data.
- `tests/Wollax.Cupel.Tests/Pipeline/OpenTelemetryReportSeamTests.cs` — failing contract tests from T01.

## Expected Output

- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs` — public structured stage snapshot model for bridge consumers.
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — additive completion hook.
- `src/Wollax.Cupel/CupelPipeline.cs` — structured handoff and report population for enabled collectors.
- `src/Wollax.Cupel/PublicAPI.Unshipped.txt` — new public API declarations for the additive seam.
