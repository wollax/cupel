# Kata State

**Active Milestone:** M003 — v1.3 Implementation Sprint
**Active Slice:** S05 — OTel bridge companion package
**Active Task:** T01 — Write failing-first OTel seam and package contract tests
**Phase:** S05 planned; ready to execute
**Slice Branch:** main
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Execute S05/T01 — add failing-first seam/package/consumption tests for the OTel bridge, wire the new test project into `Cupel.slnx`, and run the focused `dotnet test` commands to lock the exact R022 contract.
**Last Updated:** 2026-03-23 (S05 planned)

## M003 Overview

6 slices, all implementation against locked M002 spec chapters:

| Slice | Feature | Risk | Status |
|-------|---------|------|--------|
| S01 | DecayScorer (Rust + .NET) | high | ✅ complete |
| S02 | MetadataTrustScorer (Rust + .NET) | medium | ✅ complete |
| S03 | CountQuotaSlice (Rust + .NET) | high | ✅ complete |
| S04 | Core analytics + Cupel.Testing package | medium | ✅ complete |
| S05 | OTel bridge companion package | high | 🟡 planned |
| S06 | Budget simulation + tiebreaker + spec alignment | low | ⏸ queued after S05 |

## Recent Planning Decisions

- D100: S05 uses an additive `ITraceCollector` completion hook plus `StageTraceSnapshot`; OTel data must come from structured handoff, not `TraceEvent.Message` parsing
- D101: S05 proof is integration-level — failing-first seam/package tests, real OpenTelemetry SDK in-memory exporter capture, local-feed consumption proof, and full `dotnet test` regression
- D102: The companion package uses its own `CupelOpenTelemetryVerbosity` enum and emits only `cupel.*` operational fields — never item content or raw metadata values
- D095: Local NuGet consumption tests restore from `./packages`, not `./nupkg`
- D089: Rust analytics live as free functions in `crates/cupel/src/analytics.rs`

## S05 Planned Deliverables

- Additive core diagnostics seam so any enabled collector can receive the final `SelectionReport`, pipeline budget, and structured per-stage in/out counts + timing
- New `src/Wollax.Cupel.Diagnostics.OpenTelemetry/` package with `CupelOpenTelemetryTraceCollector`, `CupelOpenTelemetryVerbosity`, and `AddCupelInstrumentation(this TracerProviderBuilder)`
- Real SDK-backed tests using the OpenTelemetry in-memory exporter proving `StageOnly`, `StageAndExclusions`, and `Full` output with exact `cupel.*` attributes/events
- Package README with the spec-required cardinality warning and redaction guidance
- Local-feed consumption smoke test proving the installed companion package can observe a real pipeline run

## Inputs Available from Prior Slices

- S04 provides the proven pattern for a new packable companion package, PublicAPI files, TUnit package-test project layout, and local-feed consumption verification
- `SelectionReport`, `IncludedItem`, and `ExcludedItem` already contain most of the structured data the OTel bridge needs once the core seam hands it to enabled collectors
- `.github/workflows/release.yml` already packs all packable projects and copies `./nupkg/*.nupkg` into the consumption-test local feed; S05 must verify the new package is picked up cleanly

## Blockers

- None
