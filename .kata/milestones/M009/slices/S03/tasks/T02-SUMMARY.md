---
id: T02
parent: S03
milestone: M009
provides:
  - spec/src/scorers/metadata-key.md — complete MetadataKeyScorer spec chapter with all 10 required sections; zero TBD fields; serves as S04 implementation contract
  - cupel:priority convention documented — key, RECOMMENDED values, open-string semantics, typical usage with MetadataKeyScorer
  - Construction validation semantics documented — boost > 0.0; CupelError::ScorerConfig (Rust) / ArgumentException not ArgumentOutOfRangeException (.NET); D178 enforced
  - spec/src/SUMMARY.md — MetadataKeyScorer link added under Scorers section after DecayScorer
  - spec/src/scorers.md — MetadataKeyScorer row in Scorer Summary table; MetadataKeyScorer added to Absolute Scorers list
  - CHANGELOG.md — .NET CountConstrainedKnapsackSlice entry added to unreleased section
key_files:
  - spec/src/scorers/metadata-key.md
  - spec/src/SUMMARY.md
  - spec/src/scorers.md
  - CHANGELOG.md
key_decisions:
  - "No new decisions — spec faithfully documents existing D178 (ArgumentException not ArgumentOutOfRangeException), multiplicative scorer semantics, and cupel:priority convention"
patterns_established:
  - "MetadataKeyScorer pseudocode follows MetadataTrustScorer section structure but diverges intentionally: no defaultScore parameter, no clamping, returns multiplier (1.0 or boost) not absolute score"
observability_surfaces:
  - "grep -ci \"\\bTBD\\b\" spec/src/scorers/metadata-key.md → 0 is the binary completeness signal"
  - "grep \"^## \" spec/src/scorers/metadata-key.md lists all 10 sections for structural check"
duration: 20min
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T02: Write metadata-key scorer spec chapter and finish wiring

**MetadataKeyScorer spec chapter authored (10 sections, zero TBD), SUMMARY.md and scorers.md wired, CHANGELOG .NET CCKS entry added; cargo and dotnet regression suites green.**

## What Happened

Created `spec/src/scorers/metadata-key.md` using `metadata-trust.md` as the structural template. The chapter deliberately diverges from the template in three key ways: (1) no `defaultScore` constructor parameter — the neutral multiplier is a fixed constant of 1.0; (2) no clamping — the scorer returns multiplicative values not bounded to [0.0, 1.0]; (3) construction error is `ArgumentException` not `ArgumentOutOfRangeException` per D178.

All 10 required sections are present: Overview, Metadata Namespace Reservation, Conventions, Configuration, Algorithm, Score Interpretation, Edge Cases, Conformance Vector Outlines, Complexity, Conformance Notes. The Conventions section introduces `cupel:priority` as a reserved metadata key and cross-references `cupel:trust` and `cupel:source-type` in metadata-trust.md.

SUMMARY.md gained the MetadataKeyScorer link after DecayScorer. `scorers.md` gained both a table row (with `{1.0, boost}` output range to reflect the discrete multiplier semantics) and an entry in the Absolute Scorers category list. CHANGELOG.md gained the `.NET CountConstrainedKnapsackSlice` companion entry that was deferred from S02.

## Verification

- `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0 ✓
- `grep -q "metadata-key" spec/src/SUMMARY.md` → exits 0 ✓
- `grep -q "MetadataKeyScorer" spec/src/scorers.md` → exits 0 ✓
- `grep -q "defaultMultiplier\|1\.0" spec/src/scorers/metadata-key.md` → exits 0 ✓
- `grep -q "cupel:priority" spec/src/scorers/metadata-key.md` → exits 0 ✓
- `grep -qi "ArgumentException\|ScorerConfig" spec/src/scorers/metadata-key.md` → exits 0 ✓
- `grep -q "\.NET.*CountConstrainedKnapsackSlice" CHANGELOG.md` → exits 0 ✓
- `cargo test --all-targets` (from crates/cupel) → exit 0; all test suites pass ✓
- `dotnet test --solution Cupel.slnx` → exit 0; 797 passed, 0 failed ✓

## Diagnostics

Spec completeness check: `grep -ci "\bTBD\b" spec/src/scorers/metadata-key.md` → 0.
Section structure check: `grep "^## " spec/src/scorers/metadata-key.md` lists all 10 sections.

## Deviations

None. Cargo test workspace root is `crates/cupel/` not the repo root — ran from that directory. `dotnet test --solution Cupel.slnx` continues to work from repo root.

## Known Issues

None.

## Files Created/Modified

- `spec/src/scorers/metadata-key.md` — complete MetadataKeyScorer spec chapter (new file)
- `spec/src/SUMMARY.md` — MetadataKeyScorer link added under Scorers section
- `spec/src/scorers.md` — MetadataKeyScorer table row and Absolute Scorers list entry added
- `CHANGELOG.md` — .NET CountConstrainedKnapsackSlice entry added to unreleased section
