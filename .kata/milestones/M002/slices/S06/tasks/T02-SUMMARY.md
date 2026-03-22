---
id: T02
parent: S06
milestone: M002
provides:
  - spec/src/integrations/opentelemetry.md — fully-specified OTel verbosity chapter
  - spec/src/SUMMARY.md — Integrations section added with OTel entry
key_files:
  - spec/src/integrations/opentelemetry.md
  - spec/src/SUMMARY.md
key_decisions:
  - none (D039/D043 honored as specified; no new decisions)
patterns_established:
  - none
observability_surfaces:
  - none (spec authoring; no runtime signals)
duration: short
verification_result: passed
completed_at: 2026-03-21
blocker_discovered: false
---

# T02: Write OTel Verbosity Spec Chapter

**Created `spec/src/integrations/opentelemetry.md` — a fully-specified OTel companion package chapter with 5-Activity hierarchy (Sort omitted), three `CupelVerbosity` tiers with complete attribute tables, cardinality table, pre-stability disclaimer, zero TBD fields; linked from `spec/src/SUMMARY.md` under new `# Integrations` section.**

## What Happened

Confirmed Sort omission from `spec/src/diagnostics/events.md` (line 63: "Sort is omitted"). Created `spec/src/integrations/` directory and wrote the OTel verbosity chapter with all required sections:

- Overview with zero-dependency core guarantee and ActivitySource name `"Wollax.Cupel"`
- Pre-stability disclaimer for all `cupel.*` attribute names
- Activity hierarchy: root `cupel.pipeline` + 5 stage Activities (`classify`, `score`, `deduplicate`, `slice`, `place`)
- Three verbosity tiers (`StageOnly`, `StageAndExclusions`, `Full`) with per-tier attribute tables
- Cardinality table with environment recommendations and `Full`-in-production warning
- Flat attribute reference table covering all 12 `cupel.*` attributes/events
- Conformance notes on `ExclusionReason` canonical name mapping and open-ended values

Added `# Integrations` section to `spec/src/SUMMARY.md` after the `# Testing` section (before `# Conformance`).

## Verification

- `test -f spec/src/integrations/opentelemetry.md` → PASS
- `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0
- `grep -q "StageOnly" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "StageAndExclusions" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "Full" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "cupel.budget.max_tokens" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "cupel.exclusion.reason" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "cupel.item.included" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "Wollax.Cupel" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "pre-stable" spec/src/integrations/opentelemetry.md` → PASS
- `grep -q "Integrations" spec/src/SUMMARY.md` → PASS
- `grep -q "opentelemetry" spec/src/SUMMARY.md` → PASS
- `cargo test --manifest-path crates/cupel/Cargo.toml` → 113 passed, 1 ignored
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` → 583 passed, 0 failed

## Diagnostics

Spec-only task; no runtime signals. Inspection: `grep -ci "\bTBD\b" spec/src/integrations/opentelemetry.md` → 0; `grep -q "Integrations" spec/src/SUMMARY.md`; attribute completeness verified by checking all `cupel.*` attribute names against must-haves list.

## Deviations

None. All sections written exactly as specified in the task plan.

## Known Issues

None.

## Files Created/Modified

- `spec/src/integrations/opentelemetry.md` — new file; fully-specified OTel verbosity chapter
- `spec/src/SUMMARY.md` — `# Integrations` section added with OpenTelemetry entry
