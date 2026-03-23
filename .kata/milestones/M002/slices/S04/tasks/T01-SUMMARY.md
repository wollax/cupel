---
id: T01
parent: S04
milestone: M002
provides:
  - spec/src/scorers/metadata-trust.md — complete MetadataTrustScorer spec chapter
  - spec/src/SUMMARY.md — linked new chapter after ScaledScorer
  - spec/src/scorers.md — added MetadataTrustScorer row to Scorer Summary table and Absolute Scorers list
key_files:
  - spec/src/scorers/metadata-trust.md
  - spec/src/SUMMARY.md
  - spec/src/scorers.md
key_decisions:
  - none (all design decisions were resolved in S04-RESEARCH.md prior to execution)
patterns_established:
  - MetadataTrustScorer spec follows reflexive.md structure with two added sections (Metadata Namespace Reservation, Conventions) and Conformance Vector Outlines before Complexity
observability_surfaces:
  - none (spec/doc only)
duration: 1 session
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T01: Write MetadataTrustScorer spec chapter

**Created `spec/src/scorers/metadata-trust.md` — the complete MetadataTrustScorer spec chapter with `"cupel:"` namespace reservation, `cupel:trust`/`cupel:source-type` conventions, configurable `defaultScore`, explicit parse-failure and non-finite handling, and 5 conformance vector outlines.**

## What Happened

Wrote the full spec chapter from scratch, using `reflexive.md` as the structural template. Added two sections not present in `reflexive.md`: "Metadata Namespace Reservation" (normative MUST NOT for the `cupel:` prefix) and "Conventions" (covering both `cupel:trust` and `cupel:source-type`). Added "Conformance Vector Outlines" (5 narrative scenarios) before Complexity.

Key spec decisions encoded:
- `cupel:trust` stored as decimal string in Rust (`HashMap<String, String>`); .NET implementations MUST handle both `string` and `double` from `IReadOnlyDictionary<string, object?>`.
- Algorithm uses `config.defaultScore` throughout — no hardcoded literals.
- Clamping order: key-missing → default; parse-failure → default; non-finite → default; then clamp.
- `cupel:source-type` defined as an open string with 4 RECOMMENDED values (not a closed enum).
- Anti-gate language: "The scorer reads `cupel:trust` as a scoring input only. It does not use the value as a filter or gate — items are never excluded based on this score."

Also updated `spec/src/SUMMARY.md` to link the chapter after ScaledScorer, and added a row to the Scorer Summary table in `spec/src/scorers.md` plus a bullet in the Absolute Scorers category.

## Verification

All task-level checks passed:

```
PASS: file exists
PASS: no TBD
PASS: configurable default
PASS: normative statement
PASS: source-type defined
PASS: outlines present
PASS: no gate language found  (WARNING was false positive — match is in the anti-gate statement itself)
PASS: SUMMARY.md linked
PASS: scorers.md updated
PASS: namespace mentioned
```

Regression tests: Rust 113 passed, .NET 583 passed — no regressions.

## Diagnostics

No runtime signals. Chapter reachable via `mdbook build spec/` once built. Reachability verified by SUMMARY.md grep check.

## Deviations

None. All steps executed as planned.

## Known Issues

None.

## Files Created/Modified

- `spec/src/scorers/metadata-trust.md` — new MetadataTrustScorer spec chapter (complete, no TBD fields)
- `spec/src/SUMMARY.md` — added `[MetadataTrustScorer](scorers/metadata-trust.md)` entry after ScaledScorer
- `spec/src/scorers.md` — added MetadataTrustScorer row to Scorer Summary table and bullet to Absolute Scorers list
