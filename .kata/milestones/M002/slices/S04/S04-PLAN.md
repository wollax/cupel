# S04: Metadata Convention System Spec

**Goal:** Write `spec/src/scorers/metadata-trust.md` (MetadataTrustScorer spec chapter with `"cupel:"` namespace reservation and `cupel:trust`/`cupel:source-type` conventions), update `spec/src/SUMMARY.md` to link it, and update the `scorers.md` index table — with no TBD fields and no test regressions.
**Demo:** `spec/src/scorers/metadata-trust.md` exists; `grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md` returns 0; `grep -q "metadata-trust" spec/src/SUMMARY.md` passes; `grep -q "MetadataTrustScorer" spec/src/scorers.md` passes; `cargo test` and `dotnet test` both pass.

## Must-Haves

- `spec/src/scorers/metadata-trust.md` exists with all required sections (namespace reservation, `cupel:trust`, `cupel:source-type`, algorithm, edge cases, conformance vector outlines)
- `"cupel:"` namespace reserved normatively (callers MUST NOT use this prefix for application keys)
- `cupel:trust` convention defined: float64 [0.0, 1.0], stored as decimal string in Rust (`HashMap<String, String>`), .NET MAY store as `double` or string (canonical wire format is string)
- `cupel:source-type` convention defined: open string with four RECOMMENDED values ("user", "tool", "external", "system")
- `MetadataTrustScorer` algorithm: configurable `defaultScore` (not hardcoded), key-missing → defaultScore, unparseable string → defaultScore, non-finite → defaultScore, then clamp to [0.0, 1.0]
- Clamping order stated: key missing → default; parse failure → default; non-finite → default; clamp
- 3–5 conformance vector outlines (narrative form, since TOML schema lacks `metadata` support)
- `spec/src/SUMMARY.md` updated: new entry `[MetadataTrustScorer](scorers/metadata-trust.md)` after ScaledScorer
- `spec/src/scorers.md` Scorer Summary table updated with MetadataTrustScorer row
- No TBD fields anywhere in the new chapter (`grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md` → 0)
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes (no regressions — spec-only change)
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes (no regressions)

## Proof Level

- This slice proves: contract (spec chapter completeness + reachability via SUMMARY.md)
- Real runtime required: no (spec/doc changes only; test suites confirm no accidental regressions)
- Human/UAT required: yes (human review of spec chapter for clarity and internal consistency before marking done — per milestone DoD)

## Verification

```bash
# Chapter exists
test -f spec/src/scorers/metadata-trust.md && echo "PASS: chapter file exists"

# No TBD fields in chapter
count=$(grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md); [ "$count" -eq 0 ] && echo "PASS: no TBD fields" || echo "FAIL: $count TBD fields"

# SUMMARY.md links the chapter
grep -q "metadata-trust" spec/src/SUMMARY.md && echo "PASS: SUMMARY.md linked" || echo "FAIL: not in SUMMARY.md"

# scorers.md index updated
grep -q "MetadataTrustScorer" spec/src/scorers.md && echo "PASS: scorers.md updated" || echo "FAIL: not in scorers.md"

# Namespace reservation present
grep -q 'cupel:' spec/src/scorers/metadata-trust.md && echo "PASS: namespace mentioned"

# defaultScore referenced in algorithm (not hardcoded 0.0)
grep -q "defaultScore" spec/src/scorers/metadata-trust.md && echo "PASS: configurable default"

# Regression: Rust tests
cargo test --manifest-path crates/cupel/Cargo.toml

# Regression: .NET tests
dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj
```

## Observability / Diagnostics

- Runtime signals: none (spec/doc changes only)
- Inspection surfaces: `grep` commands above; mdBook can be built locally with `mdbook build spec/` to verify chapter renders correctly
- Failure visibility: missing file or broken SUMMARY.md link causes mdBook to silently drop the chapter — verified by checking `grep -q "metadata-trust" spec/src/SUMMARY.md`
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `spec/src/scorers/reflexive.md` (template), `spec/src/scorers/tag.md` (metadata key-lookup pattern), `spec/src/data-model/context-item.md` (metadata field definition), `spec/src/SUMMARY.md`, `spec/src/scorers.md`
- New wiring introduced in this slice: SUMMARY.md entry (makes chapter reachable in mdBook); scorers.md Scorer Summary table row (makes scorer discoverable from the index)
- What remains before the milestone is truly usable end-to-end: S05, S06 (remaining spec chapters); milestone DoD requires all 6 slices done

## Tasks

- [x] **T01: Write MetadataTrustScorer spec chapter** `est:45m`
  - Why: The core deliverable for R042 — the spec chapter that reserves the `"cupel:"` namespace, defines `cupel:trust` and `cupel:source-type` conventions, and specifies the MetadataTrustScorer algorithm with all edge cases and conformance vector outlines.
  - Files: `spec/src/scorers/metadata-trust.md` (new)
  - Do: Write the full spec chapter following the `reflexive.md` structure. Sections: Overview (fields used table), Metadata Namespace Reservation (normative MUST NOT), cupel:trust Convention (float64 [0,1], string storage note for Rust, .NET type note), cupel:source-type Convention (open string, 4 RECOMMENDED values), Algorithm (pseudocode in `text` fenced block — configurable defaultScore, key-missing/parse-failure/non-finite all → defaultScore, then clamp), Edge Cases table, Conformance Notes (string-serialization requirement, .NET accepts double or string), Conformance Vector Outlines (3–5 narrative scenarios), Complexity. Ensure: no hardcoded 0.0 default, no trust gates, no closed enum enforcement, no runtime namespace enforcement.
  - Verify: `test -f spec/src/scorers/metadata-trust.md && grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md` returns 0; `grep -q "defaultScore" spec/src/scorers/metadata-trust.md`; `grep -q "cupel:source-type" spec/src/scorers/metadata-trust.md`
  - Done when: File exists with all required sections, no TBD fields, configurable defaultScore in algorithm, parse-failure behavior explicitly stated, .NET type note present, 3+ conformance vector outlines included

- [x] **T02: Wire chapter into spec navigation and verify regressions** `est:20m`
  - Why: mdBook silently drops chapters not listed in SUMMARY.md. The scorers.md index is the discovery surface for all scorers. Both must be updated before the slice is done. Regression checks confirm the spec-only changes haven't accidentally broken anything.
  - Files: `spec/src/SUMMARY.md`, `spec/src/scorers.md`
  - Do: (1) In `spec/src/SUMMARY.md`, add `  - [MetadataTrustScorer](scorers/metadata-trust.md)` after the ScaledScorer line (line 26 area). (2) In `spec/src/scorers.md` Scorer Summary table, add row: `| [MetadataTrustScorer](scorers/metadata-trust.md) | \`metadata["cupel:trust"]\` | [0.0, 1.0] | Passthrough of caller-provided trust value from metadata |` after the ScaledScorer row. (3) Also add MetadataTrustScorer to the Absolute Scorers category list in scorers.md. (4) Run regression checks.
  - Verify: `grep -q "metadata-trust" spec/src/SUMMARY.md && grep -q "MetadataTrustScorer" spec/src/scorers.md`; `cargo test --manifest-path crates/cupel/Cargo.toml`; `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj`
  - Done when: SUMMARY.md links the chapter, scorers.md index includes MetadataTrustScorer row, both test suites pass

## Files Likely Touched

- `spec/src/scorers/metadata-trust.md` (new)
- `spec/src/SUMMARY.md`
- `spec/src/scorers.md`
