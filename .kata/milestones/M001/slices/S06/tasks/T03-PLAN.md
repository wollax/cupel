---
estimated_steps: 4
estimated_files: 4
---

# T03: Clarify interface contract documentation

**Slice:** S06 — .NET Quality Hardening
**Milestone:** M001

## Description

Four targeted XML documentation improvements to public interface contracts. These are doc-only changes — no logic changes, no new public API. Each fix closes a concrete ambiguity: `ITraceCollector.IsEnabled` lacks the constancy contract, `ISlicer` buries its sort precondition in a param doc, `ContextResult.Report` references a concrete type where a behavioral description belongs, and `SelectionReport` names `DiagnosticTraceCollector` where it should name `ITraceCollector`.

## Steps

1. **ITraceCollector.IsEnabled constancy** (`src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`). Amend or add the `IsEnabled` property doc to state: "Callers may cache this value for the duration of a pipeline run. Implementations must not toggle this property mid-run; once the pipeline has read `IsEnabled`, the value is treated as fixed for that invocation."

2. **ISlicer sort precondition** (`src/Wollax.Cupel/ISlicer.cs`). Move (or duplicate) the sort-precondition note to the interface `<summary>`. The current param doc for `scoredItems` states the sort requirement; it should also appear in the type-level or method-level summary so callers reading only the summary understand the contract. A concise sentence like "Candidates must be provided sorted by score descending." in the method `<summary>` is sufficient.

3. **ContextResult.Report nullability** (`src/Wollax.Cupel/ContextResult.cs`). Change the `Report` property XML doc from any reference to `NullTraceCollector` as a concrete type to behavioral language: "null when tracing is disabled for the pipeline run." The reader of this doc should not need to know that `NullTraceCollector` is the mechanism — they need to know the condition.

4. **SelectionReport ITraceCollector reference** (`src/Wollax.Cupel/Diagnostics/SelectionReport.cs`). Find any reference to `DiagnosticTraceCollector` in the class or method XML docs and replace with `ITraceCollector`. The doc should describe how to obtain a `SelectionReport` in terms of the interface contract, not the concrete implementation.

5. Run `dotnet build` — must produce zero errors and zero warnings. Run `dotnet test` — must pass all tests with no regressions.

## Must-Haves

- [ ] `ITraceCollector.IsEnabled` doc states: callers may cache; implementations must not toggle mid-run
- [ ] `ISlicer` method-level `<summary>` (or equivalent prominent location) states the sort precondition (candidates sorted score desc)
- [ ] `ContextResult.Report` doc uses "null when tracing is disabled" without naming `NullTraceCollector` as a concrete type
- [ ] `SelectionReport` XML doc references `ITraceCollector` not `DiagnosticTraceCollector`
- [ ] `dotnet build` — zero errors, zero warnings
- [ ] `dotnet test` — zero regressions (all 649+ tests pass)

## Verification

- `dotnet build` — zero errors, zero warnings
- `dotnet test` — all tests pass (doc-only changes should not affect test results)
- Manual spot-check: read each updated doc comment in the source to confirm the language is behavioral and does not reference a concrete type where an interface or behavioral condition should be used

## Observability Impact

- Signals added/changed: None (documentation only)
- How a future agent inspects this: `dotnet build` output; the doc comments are directly readable in source
- Failure state exposed: None — this task only improves developer-facing contract clarity

## Inputs

- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — read current `IsEnabled` doc before editing
- `src/Wollax.Cupel/ISlicer.cs` — read current `<summary>` and param docs to understand where precondition currently lives
- `src/Wollax.Cupel/ContextResult.cs` — read current `Report` property doc
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — find `DiagnosticTraceCollector` reference in XML docs

## Expected Output

- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — `IsEnabled` doc with constancy requirement
- `src/Wollax.Cupel/ISlicer.cs` — sort precondition in method `<summary>`
- `src/Wollax.Cupel/ContextResult.cs` — behavioral nullability doc for `Report`
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — `ITraceCollector` reference in XML doc
