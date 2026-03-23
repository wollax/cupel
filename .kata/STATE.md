# Kata State

**Active Milestone:** M003 — v1.3 Implementation Sprint
**Active Slice:** S02 — MetadataTrustScorer — Rust + .NET Implementation
**Active Task:** (S02 not yet started — next up)
**Phase:** Planning
**Slice Branch:** kata/M003/S01 (S01 complete; S02 branch to be created)
**Active Workspace:** /Users/wollax/Git/personal/cupel
**Next Action:** Start S02 — MetadataTrustScorer Rust + .NET implementation (depends on S01 scorer pattern, which is now established)
**Last Updated:** 2026-03-23 (S01 complete — DecayScorer Rust + .NET; all 5 conformance vectors pass; 45+38 Rust tests, 663 .NET tests; drift guard satisfied; R020 validated)

## M003 Overview

6 slices, all implementation against locked M002 spec chapters:

| Slice | Feature | Risk | Status |
|-------|---------|------|--------|
| S01 | DecayScorer (Rust + .NET) | high | ✅ complete |
| S02 | MetadataTrustScorer (Rust + .NET) | medium | — |
| S03 | CountQuotaSlice (Rust + .NET) | high | — |
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

## M002 Pending UAT Gate

S06-UAT.md defines the final human review gate for M002. Automated checks pass. Human review of
three S06 spec chapters (decay.md, opentelemetry.md, budget-simulation.md) is the final sign-off
step. M003 implementation proceeds in parallel — the specs are locked.

## Blockers

- (none)
