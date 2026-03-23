---
estimated_steps: 5
estimated_files: 7
---

# T03: Implement the OpenTelemetry companion package and SDK-backed assertions

**Slice:** S05 — OTel bridge companion package
**Milestone:** M003

## Description

Build the actual `Wollax.Cupel.Diagnostics.OpenTelemetry` package on top of the new structured core seam. The package must emit the exact spec-defined Activities and Events, expose the public registration surface, and keep the OTel-specific dependency surface out of `Wollax.Cupel` core.

## Steps

1. Create `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` from the proven S04 package template, including `PublicAPI.Shipped.txt`, `PublicAPI.Unshipped.txt`, and package metadata.
2. Implement `CupelOpenTelemetryVerbosity` and `CupelOpenTelemetryTraceCollector` using `ActivitySource("Wollax.Cupel")`, the structured completion handoff, and exact emission rules for `StageOnly`, `StageAndExclusions`, and `Full`.
3. Implement `AddCupelInstrumentation(this TracerProviderBuilder)` so host code can register the canonical source without manually repeating the source name.
4. Add a package README with the spec-required cardinality warning, source name, and redaction guidance; ensure the implementation does not emit item content or raw metadata values.
5. Make the package test project pass against the real OpenTelemetry SDK in-memory exporter and clean up any PublicAPI analyzer failures.

## Must-Haves

- [ ] The package compiles independently and exposes the public API promised in the roadmap: `CupelOpenTelemetryTraceCollector`, `CupelOpenTelemetryVerbosity`, and `AddCupelInstrumentation()`.
- [ ] The emitted trace contract matches the spec exactly: canonical source name, root/stage hierarchy, exact `cupel.*` attributes, `cupel.exclusion` events at the middle tier, and `cupel.item.included` events only in `Full`.
- [ ] The package keeps observability safe-by-default: no item content or raw metadata values are emitted, and the README calls out the high-cardinality warning for `Full`.

## Verification

- `dotnet test tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests.csproj`
- `dotnet build src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj`

## Observability Impact

- Signals added/changed: Real `ActivitySource` spans/events for Cupel pipeline runs at three verbosity levels.
- How a future agent inspects this: Use the in-memory exporter tests to inspect emitted Activities/Events and inspect the package README/PublicAPI files for the supported public surface.
- Failure state exposed: Missing source registration, wrong attribute carrier, tier leakage, or content leakage fail deterministic package tests.

## Inputs

- `src/Wollax.Cupel.Testing/Wollax.Cupel.Testing.csproj` — template for a new packable companion package.
- `Directory.Packages.props` — central version management for OpenTelemetry dependencies.
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — failing SDK-backed proof targets from T01.
- `src/Wollax.Cupel/Diagnostics/StageTraceSnapshot.cs` and `ITraceCollector` completion hook — structured core seam from T02.

## Expected Output

- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/Wollax.Cupel.Diagnostics.OpenTelemetry.csproj` — new packable package project.
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryTraceCollector.cs` — concrete collector implementation.
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/CupelOpenTelemetryVerbosity.cs` — public verbosity enum.
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/TracerProviderBuilderExtensions.cs` — public registration extension.
- `src/Wollax.Cupel.Diagnostics.OpenTelemetry/README.md` — package usage + cardinality/redaction guidance.
- `tests/Wollax.Cupel.Diagnostics.OpenTelemetry.Tests/CupelOpenTelemetryTraceCollectorTests.cs` — passing SDK-backed contract tests.
