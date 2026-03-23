---
id: T01
parent: S06
milestone: M002
provides:
  - spec/src/scorers/decay.md — fully-specified DecayScorer spec chapter
  - spec/src/SUMMARY.md — DecayScorer entry added under Scorers
key_files:
  - spec/src/scorers/decay.md
  - spec/src/SUMMARY.md
key_decisions:
  - none (D042, D047 honored as specified; no new decisions)
patterns_established:
  - none
observability_surfaces:
  - none (spec authoring; no code changes)
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Write DecayScorer Spec Chapter

**Created `spec/src/scorers/decay.md` — a fully-specified DecayScorer chapter with DECAY-SCORE pseudocode, three curve factories (Exponential/Step/Window) with preconditions, mandatory TimeProvider injection (D042/D047), edge cases table, and 5 conformance vector outlines; zero TBD fields; linked from SUMMARY.md.**

## What Happened

Read `spec/src/scorers/metadata-trust.md` (canonical chapter template) and `spec/src/scorers/recency.md` (contrast framing) then authored `spec/src/scorers/decay.md` following the same section ordering: Overview → Fields Used → TimeProvider → Algorithm → Curve Factories → Configuration → Edge Cases → Conformance Vector Outlines → Complexity → Conformance Notes.

Key content delivered:
- **Overview**: Contrast framing — RecencyScorer is rank-based (relative), DecayScorer is absolute-decay (absolute).
- **TimeProvider**: D042 no-default mandate documented; .NET `System.TimeProvider` and Rust trait declaration verbatim per D047.
- **DECAY-SCORE pseudocode**: `allItems` ignored; negative-age clamping to `duration_zero` explicit in both pseudocode comment and Conformance Notes.
- **Exponential**: `pow(2.0, −age/halfLife)` with `halfLife > Duration::ZERO` precondition and throw-at-construction.
- **Step**: ordered windows list, strict `>` comparison, zero-maxAge and empty-list preconditions.
- **Window**: half-open `[0, maxAge)` interval, `age == maxAge` returns 0.0 explicitly.
- **nullTimestampScore**: default 0.5, neutral-semantics rationale documented.
- **5 conformance vector outlines**: all with `referenceTime = 2025-01-01T12:00:00Z`.
- Added `  - [DecayScorer](scorers/decay.md)` immediately after `MetadataTrustScorer` entry in SUMMARY.md.

## Verification

- `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0 ✓
- `grep -q "DECAY-SCORE" spec/src/scorers/decay.md` → PASS ✓
- `grep -q "Exponential\|Step\|Window" spec/src/scorers/decay.md` → PASS ✓
- `grep -q "TimeProvider" spec/src/scorers/decay.md` → PASS ✓
- `grep -q "nullTimestampScore" spec/src/scorers/decay.md` → PASS ✓
- `grep -q "Conformance" spec/src/scorers/decay.md` → PASS ✓
- `grep -q "decay" spec/src/SUMMARY.md` → PASS ✓
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed, 0 failed ✓
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed ✓

## Diagnostics

Spec-only task; no runtime signals. Inspection: `grep -ci "\bTBD\b" spec/src/scorers/decay.md` → 0; `grep -q "DECAY-SCORE" spec/src/scorers/decay.md`.

## Deviations

None. Followed task plan exactly.

## Known Issues

None.

## Files Created/Modified

- `spec/src/scorers/decay.md` — new file; fully-specified DecayScorer chapter
- `spec/src/SUMMARY.md` — DecayScorer entry added under Scorers
