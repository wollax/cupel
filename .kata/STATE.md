# Kata State

**Active Milestone:** M003 — v1.3 Implementation Sprint
**Active Slice:** S04 — Core analytics + Cupel.Testing package
**Active Task:** (S04 not yet started)
**Phase:** S03 complete; advancing to S04
**Slice Branch:** (S04 branch not yet created)
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Create S04 branch, read S04 plan, begin T01 (analytics extension methods in both languages)
**Last Updated:** 2026-03-23 (S03 complete — CountQuotaSlice in Rust + .NET; 117 Rust tests + 682 .NET tests pass; all 5 conformance vectors in all 3 locations; drift guard clean)

## M003 Overview

6 slices, all implementation against locked M002 spec chapters:

| Slice | Feature | Risk | Status |
|-------|---------|------|--------|
| S01 | DecayScorer (Rust + .NET) | high | ✅ complete |
| S02 | MetadataTrustScorer (Rust + .NET) | medium | ✅ complete |
| S03 | CountQuotaSlice (Rust + .NET) | high | ✅ complete |
| S04 | Core analytics + Cupel.Testing package | medium | — |
| S05 | OTel bridge companion package | high | — |
| S06 | Budget simulation + tiebreaker + spec alignment | low | — |

## Key Decisions Established in M003

- D072: S01 verification strategy — contract-level (cargo test + dotnet test + drift guard diff)
- D073: .NET DecayCurve as abstract class with sealed nested subtypes; ArgumentException at construction
- D074: Rust DecayCurve uses free-function constructor pattern returning Result<Self, CupelError>
- D075: Rust age uses millisecond precision (num_milliseconds() / 1000.0) to avoid integer truncation
- D076: .NET age clamping uses explicit zero-check (rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge), NOT .Duration()
- D077: Protected constructor on abstract DecayCurve must be listed in PublicAPI.Unshipped.txt (RS0016)
- D078: S02 verification strategy — contract-level only (cargo test + dotnet test + drift guard diff)
- D079: TOML metadata format — inline table on [[items]]: `metadata = { "cupel:trust" = "0.85" }` — build_items extended with .as_table() block
- D080: .NET MetadataTrustScorer uses ArgumentOutOfRangeException for out-of-range defaultScore
- D081: is_finite() MUST follow parse() in MetadataTrustScorer score() — "NaN".parse::<f64>() returns Ok(NaN)
- D082: Conformance vectors must exist in THREE locations: spec/conformance/, root conformance/, crates/cupel/conformance/
- D083: D059 dual-type dispatch: double branch before string branch in .NET Score()
- D084: S03 verification strategy — contract-level only; no pipeline-level integration tests for slicers
- D085: is_knapsack() default false on Slicer trait; KnapsackSlice overrides true — avoids Any/downcast
- D086: Cap-reason exclusions not observable at Slicer::slice level in Rust (no TraceCollector in interface)
- D087: .NET CountQuotaSlice.LastShortfalls as test inspection surface (not part of ISlicer)

## S03 Known Gaps (deferred to future)

- `SelectionReport.CountRequirementShortfalls` always `[]` via standard Pipeline — ReportBuilder needs wiring
- `ExclusionReason.CountCapExceeded` not in `SelectionReport.Excluded` — pipeline extension deferred
- Shortfall propagation via `ITraceCollector` requires a new interface method (deferred)

## Blockers

- (none)

## S04 Prerequisites

- `CountRequirementShortfalls` field exists on SelectionReport in both languages (done in S03) — S04 assertion patterns can reference it
- `SelectionReport` stable type established through M001/M002 — S04 analytics and testing vocab can build on it
