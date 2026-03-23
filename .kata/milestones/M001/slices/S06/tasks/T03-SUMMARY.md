---
id: T03
parent: S06
milestone: M001
provides:
  - ITraceCollector.IsEnabled doc with constancy contract (callers may cache; implementations must not toggle mid-run)
  - ISlicer.Slice method-level summary states sort precondition (candidates sorted score descending)
  - ContextResult.Report doc uses behavioral nullability language ("null when tracing is disabled") without naming NullTraceCollector
  - SelectionReport class summary references ITraceCollector instead of DiagnosticTraceCollector
key_files:
  - src/Wollax.Cupel/Diagnostics/ITraceCollector.cs
  - src/Wollax.Cupel/ISlicer.cs
  - src/Wollax.Cupel/ContextResult.cs
  - src/Wollax.Cupel/Diagnostics/SelectionReport.cs
key_decisions:
  - none (doc-only changes; no logic or API changes)
patterns_established:
  - Interface contract docs describe behavioral conditions and use interface types (ITraceCollector), not concrete implementations (DiagnosticTraceCollector, NullTraceCollector)
observability_surfaces:
  - none (documentation only)
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T03: Clarify interface contract documentation

**Four targeted XML doc improvements: IsEnabled constancy contract, ISlicer sort precondition in method summary, ContextResult.Report behavioral nullability, SelectionReport references ITraceCollector not DiagnosticTraceCollector.**

## What Happened

All four doc-only changes applied as planned:

1. **ITraceCollector.IsEnabled** — Added two sentences to the existing property doc: callers may cache the value for the duration of a pipeline run; implementations must not toggle mid-run.

2. **ISlicer.Slice method summary** — Added "Candidates must be provided sorted by score descending." to the method-level `<summary>`. The sort precondition was already present in the class `<remarks>` and in the `scoredItems` param doc; it now also appears in the most prominent location (method summary).

3. **ContextResult.Report** — Changed "Null when tracing was not requested for this pipeline execution." to "`<c>null</c>` when tracing is disabled for the pipeline run." — pure behavioral language with no concrete type reference. (The original doc did not reference `NullTraceCollector` either, but the updated wording is sharper and consistent with the task plan's prescribed language.)

4. **SelectionReport class summary** — Changed "Populated when a `<see cref="DiagnosticTraceCollector"/>` is used." to "Populated when an `<see cref="ITraceCollector"/>` with tracing enabled is used." — now describes the contract in terms of the interface.

## Verification

- `dotnet build` — zero errors, zero warnings
- `dotnet test` — 653/653 passed, 0 failed, 0 skipped

## Diagnostics

None — documentation only. No runtime behavior changed.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — IsEnabled doc with constancy requirement
- `src/Wollax.Cupel/ISlicer.cs` — sort precondition added to Slice method summary
- `src/Wollax.Cupel/ContextResult.cs` — Report doc uses behavioral nullability language
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — class summary references ITraceCollector
