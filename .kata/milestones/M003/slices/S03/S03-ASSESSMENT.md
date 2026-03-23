# S03 Post-Slice Roadmap Assessment

**Assessed after:** S03 — CountQuotaSlice Rust + .NET implementation
**Verdict:** Roadmap is fine — no changes needed

## Success Criteria Coverage

- `cargo test` passes with CountQuotaSlice, analytics extension tests → S04 (analytics); S03 done ✓
- `dotnet test` passes with Cupel.Testing, OTel bridge, budget simulation tests → S04, S05, S06 ✓
- `Wollax.Cupel.Testing` NuGet package installs with `SelectionReport.Should()` chains → S04 ✓
- `Wollax.Cupel.Diagnostics.OpenTelemetry` produces real OTel output at 3 verbosity tiers → S05 ✓
- DecayScorer and MetadataTrustScorer conformance vectors pass → already done (S01, S02) ✓
- Tiebreaker rule spec-committed and implemented in GreedySlice both languages → S06 ✓

All success criteria have at least one remaining owning slice.

## Risk Retirement

S03 was `risk:high`. Its declared pre-condition risk — the `#[non_exhaustive]` audit on `ExclusionReason` before adding new variants — was verified clean in T01 (`grep -r non_exhaustive` confirmed the attribute was present; new variants added without incident). Risk retired.

## Known Limitations (scoped, not blocking)

Two documented limitations from S03 are explicitly out of scope for S04–S06:

1. `SelectionReport.CountRequirementShortfalls` always returns `[]` via `Pipeline.DryRun` — `ReportBuilder` lacks a shortfall injection path. Use `CountQuotaSlice.LastShortfalls` for post-run inspection in .NET tests. Future pipeline wiring is a follow-up item.
2. `CountCapExceeded` / `CountRequireCandidatesExhausted` exclusion reasons are not surfaced in `SelectionReport.Excluded` in either language — requires pipeline-level wiring through `ReportBuilder`. Deferred to a future slice.

Neither limitation affects S04's Cupel.Testing vocabulary (patterns operate on SelectionReport fields that are populated via standard DryRun), nor S05/S06.

## Boundary Map Accuracy

- **S04** consumes `SelectionReport` (stable ✓) and `CountRequirementShortfalls` from S03 (property exists, hedged "if included" in boundary map ✓). No change needed.
- **S05** consumes ITraceCollector / SelectionReport from S04. S03 did not affect these types.
- **S06** consumes Pipeline/DryRun APIs (stable since M001/M002) and analytics from S04. S03 introduced no changes to these APIs.

## New Patterns Established (available to S04–S06)

- `is_knapsack()` default method on `Slicer` trait is the approved guard pattern for any future decorator slicers with similar inner-slicer constraints.
- `RawSelectionReport` backward-compatible field extension via `#[serde(default)]` (deny_unknown_fields intentionally removed) — future SelectionReport field additions should follow this pattern.

## Requirement Coverage

- R021 (Cupel.Testing) → S04 active ✓
- R022 (OTel bridge) → S05 active ✓
- R040 (CountQuotaSlice implementation) → validated by S03 ✓
- All other validated requirements remain unaffected.

No requirement ownership changes. Active requirement coverage remains sound.
