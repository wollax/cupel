---
id: S04
parent: M002
milestone: M002
provides:
  - spec/src/scorers/metadata-trust.md — complete MetadataTrustScorer spec chapter with namespace reservation and conventions
  - spec/src/SUMMARY.md — chapter linked after ScaledScorer
  - spec/src/scorers.md — MetadataTrustScorer row in Scorer Summary table and Absolute Scorers list
requires: []
affects:
  - S06 (may reference cupel: namespace conventions when specifying DecayScorer or OTel attribute names)
key_files:
  - spec/src/scorers/metadata-trust.md
  - spec/src/SUMMARY.md
  - spec/src/scorers.md
key_decisions:
  - none (all design decisions resolved in S04-RESEARCH.md prior to execution)
patterns_established:
  - MetadataTrustScorer spec follows reflexive.md structure with two added sections (Metadata Namespace Reservation, Conventions) and Conformance Vector Outlines inserted before Complexity
observability_surfaces:
  - none (spec/doc changes only; reachability verified via grep + mdbook build)
drill_down_paths:
  - .kata/milestones/M002/slices/S04/tasks/T01-SUMMARY.md
  - .kata/milestones/M002/slices/S04/tasks/T02-SUMMARY.md
duration: 2 tasks, 1 session
verification_result: passed
completed_at: 2026-03-21
---

# S04: Metadata Convention System Spec

**New `spec/src/scorers/metadata-trust.md` spec chapter reserves the `"cupel:"` metadata namespace, defines `cupel:trust` and `cupel:source-type` conventions, and specifies the `MetadataTrustScorer` algorithm with configurable `defaultScore`, explicit parse-failure and non-finite handling, and 5 conformance vector outlines — linked from `SUMMARY.md` and indexed in `scorers.md`.**

## What Happened

**T01** wrote the complete `spec/src/scorers/metadata-trust.md` chapter from scratch, using `reflexive.md` as the structural template. Two sections not present in reflexive.md were added: "Metadata Namespace Reservation" (normative MUST NOT preventing callers from using the `cupel:` prefix for application keys) and "Conventions" (covering both `cupel:trust` and `cupel:source-type`). Conformance Vector Outlines (5 narrative scenarios) were inserted before Complexity.

Key spec decisions encoded in T01:
- `cupel:trust` is stored as a decimal string in Rust (`HashMap<String, String>`); .NET implementations MUST handle both `string` and `double` from `IReadOnlyDictionary<string, object?>`.
- Algorithm uses `config.defaultScore` throughout — no hardcoded literals; clamping order is: key-missing → default; parse-failure → default; non-finite → default; then clamp to [0.0, 1.0].
- `cupel:source-type` is an open string with 4 RECOMMENDED values ("user", "tool", "external", "system") — not a closed enum.
- Explicit anti-gate language: trust is a scoring input only; items are never excluded based on this score.

T01 also wired the chapter into both `spec/src/SUMMARY.md` (entry after ScaledScorer) and `spec/src/scorers.md` (Scorer Summary table row and Absolute Scorers list bullet).

**T02** confirmed all navigation wiring was complete (T01 had already done it) and ran full regression suites: Rust 113 passed, .NET 583 passed — no regressions.

## Verification

All slice-level checks passed:

```
PASS: chapter file exists        — test -f spec/src/scorers/metadata-trust.md
PASS: no TBD fields              — grep -ci "\bTBD\b" → 0
PASS: SUMMARY.md linked          — grep -q "metadata-trust" spec/src/SUMMARY.md
PASS: scorers.md updated         — grep -q "MetadataTrustScorer" spec/src/scorers.md
PASS: namespace mentioned        — grep -q 'cupel:' spec/src/scorers/metadata-trust.md
PASS: configurable default       — grep -q "defaultScore" spec/src/scorers/metadata-trust.md
PASS: Rust tests                 — cargo test: 35 passed, 0 failed (+ doctests: 78 passed)
PASS: .NET tests                 — dotnet test: 583 passed, 0 failed
```

## Requirements Advanced

- R042 — Metadata convention system spec: `spec/src/scorers/metadata-trust.md` written with `"cupel:"` namespace reservation, `cupel:trust`/`cupel:source-type` conventions, `MetadataTrustScorer` algorithm, and conformance vector outlines.

## Requirements Validated

- R042 — All validation criteria satisfied: file exists, no TBD fields, configurable defaultScore in algorithm, parse-failure behavior explicitly stated, .NET type note present, 3+ conformance vector outlines, SUMMARY.md linked, scorers.md updated, cargo test and dotnet test both pass.

## New Requirements Surfaced

- none

## Requirements Invalidated or Re-scoped

- none

## Deviations

T01 completed both navigation wiring steps (SUMMARY.md and scorers.md) that were originally scoped to T02. T02 therefore focused entirely on verification. No plan changes required; both tasks completed successfully.

## Known Limitations

- Conformance vectors are in narrative form (not TOML) because the current TOML schema lacks `metadata` support. A future slice or milestone could add TOML-backed conformance vectors when the schema is extended.
- `rtk dotnet test` is incompatible with TUnit's test runner (injects unsupported `--nologo` and `--logger` flags). Use bare `dotnet test` for .NET regression checks in this project.

## Follow-ups

- none (all deliverables complete; remaining M002 work is S05 and S06)

## Files Created/Modified

- `spec/src/scorers/metadata-trust.md` — new MetadataTrustScorer spec chapter (complete, no TBD fields, 5 conformance vector outlines)
- `spec/src/SUMMARY.md` — added `[MetadataTrustScorer](scorers/metadata-trust.md)` entry after ScaledScorer
- `spec/src/scorers.md` — added MetadataTrustScorer row to Scorer Summary table and bullet to Absolute Scorers list

## Forward Intelligence

### What the next slice should know
- The `cupel:` namespace reservation is now normative. S06 (OTel spec, DecayScorer) may reference or extend this namespace for `cupel.*` OTel attribute names — this is consistent with, not in conflict with, the metadata namespace.
- The `cupel:source-type` open string convention (not closed enum) is intentional; S06 should not introduce a closed enum without a design record.

### What's fragile
- Conformance vectors are narrative-only — no TOML test vectors exist for MetadataTrustScorer. Any future implementation will need to hand-translate them or extend the conformance schema.

### Authoritative diagnostics
- `grep -q "metadata-trust" spec/src/SUMMARY.md` — fastest check that the chapter is reachable in mdBook
- `grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md` → 0 confirms spec completeness

### What assumptions changed
- No significant assumption changes; this was a low-risk, standalone slice that executed as planned.
