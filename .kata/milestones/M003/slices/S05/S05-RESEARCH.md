# S05: OTel bridge companion package — Research

**Date:** 2026-03-23

## Summary

S05 owns the only active requirement in the repo right now: **R022 — OpenTelemetry bridge**. The spec is clear about the external contract: separate NuGet package, canonical `ActivitySource` name `"Wollax.Cupel"`, exact `cupel.*` attributes, and three verbosity tiers (`StageOnly`, `StageAndExclusions`, `Full`). The packaging side of this slice is low-risk because S04 already proved the reusable pattern for a new packable companion package plus separate consumption-test wiring.

The real risk is **not packaging** — it is the **current diagnostics seam in core**. Today `ITraceCollector` only receives `TraceEvent { Stage, Duration, ItemCount, Message }`. That is not enough to emit spec-accurate exclusion events or included-item events, and `CupelPipeline` only builds `SelectionReport` when the collector is specifically a `DiagnosticTraceCollector`. `DiagnosticTraceCollector` is also `sealed`, so an OTel collector cannot subclass it to piggyback on the current report-building path.

Primary recommendation: treat S05 as **two layers**. First, make the smallest additive core seam needed so an OTel collector can access structured final-report data and reliable stage counts without parsing free-text messages. Then implement `Wollax.Cupel.Diagnostics.OpenTelemetry` as a separate package following the S04 project/test/release pattern, with tests using the OpenTelemetry SDK plus the in-memory exporter.

## Recommendation

Take this approach:

1. **Clone the S04 package pattern** for `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` and a matching test project.
2. **Do not parse `TraceEvent.Message`** to synthesize `cupel.*` attributes. The current message text is warning-oriented and not a stable contract.
3. **Add a minimal additive core seam before implementing the collector.** One of these is required:
   - **Preferred:** make `CupelPipeline.Execute` populate `ContextResult.Report` for any enabled collector, not just `DiagnosticTraceCollector`, while separately accumulating trace events locally for report construction; or
   - extend `TraceEvent` with optional structured fields needed by the OTel spec (`item_count_in`, `item_count_out`, excluded item kind/tokens/reason, included item kind/tokens/score, possibly budget max tokens).
4. Implement the companion collector against **`System.Diagnostics.ActivitySource` + `ActivityEvent`**, and keep the OpenTelemetry SDK dependency only for the registration extension (`AddCupelInstrumentation(this TracerProviderBuilder)`) and tests.
5. Verify with **real SDK capture**, not a fake sink: `Sdk.CreateTracerProviderBuilder().AddSource("Wollax.Cupel")...AddInMemoryExporter(...)`.

Why this approach:
- It satisfies R022 without violating the core zero-dependency rule.
- It reuses the already-proven packaging/release workflow from S04.
- It avoids building the bridge on top of unstable free-text messages.
- It keeps the OTel-specific dependency surface in the companion package instead of leaking it into `Wollax.Cupel`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Span/trace primitives | `System.Diagnostics.ActivitySource`, `Activity`, `ActivityEvent` | This is the native .NET tracing API and the exact substrate OpenTelemetry .NET uses. No custom span model needed. |
| SDK-based trace capture in tests | `OpenTelemetry` + `OpenTelemetry.Exporter.InMemory` | Produces real exported trace data through the SDK; avoids fake collectors that can drift from real OTel behavior. |
| New package/release wiring | S04 package pattern (`Wollax.Cupel.Testing`, `release.yml`, consumption tests local feed) | Already proven in this repo. Reinventing the project/pack/test flow adds risk for no gain. |

## Existing Code and Patterns

- `spec/src/integrations/opentelemetry.md` — authoritative spec for S05: exact source name, 5-Activity hierarchy, exact `cupel.*` attributes, 3 verbosity tiers, cardinality guidance.
- `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs` — current collector contract; only stage/item `TraceEvent` callbacks exist.
- `src/Wollax.Cupel/Diagnostics/TraceEvent.cs` — current payload is too thin for spec-accurate OTel emission (`Stage`, `Duration`, `ItemCount`, `Message` only).
- `src/Wollax.Cupel/CupelPipeline.cs` — key seam. Stage events are emitted **after** each stage completes; `ContextResult.Report` is only built when the collector is a `DiagnosticTraceCollector`.
- `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs` — already has the structured included/excluded data S05 needs (`IncludedItem`, `ExcludedItem`, reasons, scores), but it is only used on the DiagnosticTraceCollector path.
- `src/Wollax.Cupel/Diagnostics/SelectionReport.cs` — final report shape already contains most of the structured data needed for `StageAndExclusions`/`Full` emission.
- `src/Wollax.Cupel/Diagnostics/IncludedItem.cs` and `ExcludedItem.cs` — source of exact included-item score/kind/tokens and exclusion reason/kind/tokens.
- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — reusable template for a new packable companion package with PublicApiAnalyzers.
- `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj` — reusable template for a package-specific TUnit test project.
- `tests/Wollax.Cupel.ConsumptionTests/nuget.config` — local feed is `./packages`, not `./nupkg`.
- `.github/workflows/release.yml` — actual release workflow in this repo. The roadmap/context mentions `release-dotnet.yml`, but the real file is `release.yml`.
- `Directory.Packages.props` — central package management is enabled; new OpenTelemetry packages belong here.
- `Cupel.slnx` — new packable/test projects must be added here or they will be missed by root `dotnet build/test/pack` flows.

## Constraints

- **Requirement ownership:** S05 owns **R022**; this research should optimize for proving that requirement, not just adding another NuGet project.
- **Separate package only:** `Wollax.Cupel` core must remain zero-dependency with respect to OpenTelemetry SDK assemblies.
- **Canonical source name:** `ActivitySource` name must be exactly `"Wollax.Cupel"`.
- **Verbosity surface:** spec requires 3 tiers (`StageOnly`, `StageAndExclusions`, `Full`); current core `TraceDetailLevel` has only 2 values (`Stage`, `Item`). The companion should use its own verbosity enum instead of trying to reuse `TraceDetailLevel`.
- **Current report-building gate:** `ContextResult.Report` is null unless the collector is `DiagnosticTraceCollector` or the caller uses `DryRun()`. This blocks a pure `ITraceCollector`-only OTel implementation from seeing the final included/excluded sets.
- **Current stage payload shape:** stage callbacks carry a single `ItemCount`, not explicit `item_count_in` / `item_count_out`.
- **Consumption-test flow:** release copies `.nupkg` files into `tests/Wollax.Cupel.ConsumptionTests/packages/` before testing. New package installability proof must follow the same path.
- **Central versions:** new package dependencies must be added to `Directory.Packages.props` rather than inline-versioning the csproj.

## Common Pitfalls

- **Parsing `TraceEvent.Message` as data** — current messages are human-readable warnings, not a stable schema. Use structured report data or add structured fields.
- **Assuming any enabled collector gets a `SelectionReport`** — false today. Only `DiagnosticTraceCollector` and `DryRun()` yield one.
- **Assuming the repo has `release-dotnet.yml`** — it does not. The real workflow is `.github/workflows/release.yml`.
- **Forgetting the local-feed copy step** — `dotnet pack` outputs to `./nupkg`, but consumption tests restore from `tests/Wollax.Cupel.ConsumptionTests/packages/`.
- **Calling `AddOpenTelemetry()` from the library package** — OpenTelemetry’s own guidance says host code should call `AddOpenTelemetry`; the library/companion should expose `TracerProviderBuilder` registration helpers instead.
- **Overusing per-item events in production tests** — the spec already warns that `Full` is high-cardinality and dev-only. Tests should verify it, but docs should keep the warning prominent.

## Open Risks

- **Primary blocker:** current `ITraceCollector` + `TraceEvent` surface does not expose enough structured data to implement the full OTel spec cleanly.
- **API seam decision risk:** if S05 needs a core additive seam, that work must update PublicAPI files and may require adjusting tests that implicitly assume only `DiagnosticTraceCollector` produces reports.
- **Stage count fidelity:** `item_count_in` / `item_count_out` are not emitted explicitly today. Some values can be reconstructed, but not from a formal contract.
- **Budget attribute fidelity:** root attribute `cupel.budget.max_tokens` is specified, but current trace callbacks do not carry budget data. Either the collector constructor must take budget input, or core must expose it.
- **Package dependency choice:** the package project likely only needs `OpenTelemetry` at runtime, while tests likely also want `OpenTelemetry.Exporter.InMemory`. Confirm the minimum package set during implementation.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| OpenTelemetry on .NET | `aaronontheweb/dotnet-skills@opentelemetry-net-instrumentation` | Available, not installed. Most directly relevant result from `npx skills find "OpenTelemetry .NET"` (install: `npx skills add aaronontheweb/dotnet-skills@opentelemetry-net-instrumentation`). |
| OpenTelemetry (generic) | `bobmatnyc/claude-mpm-skills@opentelemetry` | Available, not installed. Highest install count among generic OTel skills (install: `npx skills add bobmatnyc/claude-mpm-skills@opentelemetry`). |
| TUnit / .NET tests | `github/awesome-copilot@csharp-tunit` | Available, not installed. Highest-signal TUnit skill result (install: `npx skills add github/awesome-copilot@csharp-tunit`). |
| Built-in installed skills | none directly relevant | `debug-like-expert`, `frontend-design`, and `swiftui` are installed, but none are directly relevant to this .NET/OpenTelemetry package slice. |

## Sources

- OTel contract for this slice: `spec/src/integrations/opentelemetry.md`
- Current collector contract: `src/Wollax.Cupel/Diagnostics/ITraceCollector.cs`
- Current event payload shape: `src/Wollax.Cupel/Diagnostics/TraceEvent.cs`
- Current pipeline/report seam: `src/Wollax.Cupel/CupelPipeline.cs`
- Structured final-report data already available in core: `src/Wollax.Cupel/Diagnostics/ReportBuilder.cs`, `src/Wollax.Cupel/Diagnostics/SelectionReport.cs`, `src/Wollax.Cupel/Diagnostics/IncludedItem.cs`, `src/Wollax.Cupel/Diagnostics/ExcludedItem.cs`
- Proven new-package pattern: `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj`, `tests/Wollax.Cupel.Testing.Tests/Wollax.Cupel.Testing.Tests.csproj`
- Proven release/consumption pattern: `.github/workflows/release.yml`, `tests/Wollax.Cupel.ConsumptionTests/nuget.config`, `tests/Wollax.Cupel.ConsumptionTests/Wollax.Cupel.ConsumptionTests.csproj`
- Microsoft Learn — custom instrumentation guidance and library-vs-app guidance: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs
- Microsoft Learn — `ActivitySource.StartActivity` supports explicit `startTime`: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activitysource.startactivity
- Microsoft Learn — `Activity.SetEndTime` supports backfilling stop time: https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.setendtime
- NuGet package reference — `OpenTelemetry` 1.15.0: https://www.nuget.org/packages/OpenTelemetry
- NuGet package reference — `OpenTelemetry.Extensions.Hosting` 1.15.0: https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting
- NuGet package reference — `OpenTelemetry.Exporter.InMemory` 1.15.0 (explicitly intended for testing): https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory
