# S04: Metadata Convention System Spec — UAT

**Milestone:** M002
**Written:** 2026-03-21

## UAT Type

- UAT mode: artifact-driven
- Why this mode is sufficient: S04 produces only spec/documentation files — no runtime components, no APIs, no UI. Correctness is verified by inspecting the artifact content (section completeness, absence of TBD fields, normative language accuracy) and confirming mdBook navigation wiring. Human review of the spec chapter for clarity and internal consistency is required per the milestone DoD.

## Preconditions

- `spec/src/scorers/metadata-trust.md` exists
- `spec/src/SUMMARY.md` contains `metadata-trust`
- `spec/src/scorers.md` contains `MetadataTrustScorer`
- `cargo test --manifest-path crates/cupel/Cargo.toml` passes
- `dotnet test --project tests/Wollax.Cupel.Tests/Wollax.Cupel.Tests.csproj` passes

## Smoke Test

```bash
test -f spec/src/scorers/metadata-trust.md && \
  grep -q "metadata-trust" spec/src/SUMMARY.md && \
  grep -q "MetadataTrustScorer" spec/src/scorers.md && \
  echo "SMOKE PASS"
```

## Test Cases

### 1. Namespace reservation is normative

1. Open `spec/src/scorers/metadata-trust.md`.
2. Locate the "Metadata Namespace Reservation" section.
3. **Expected:** The section contains a normative MUST NOT statement prohibiting callers from using the `cupel:` prefix for application keys. RFC 2119 language ("MUST NOT") is used.

### 2. cupel:trust convention is fully specified

1. Open `spec/src/scorers/metadata-trust.md`.
2. Locate the `cupel:trust` subsection under "Conventions".
3. **Expected:** The convention specifies: float64 range [0.0, 1.0]; stored as decimal string in Rust (`HashMap<String, String>`); .NET implementations MUST handle both `string` and `double`; caller-computed (not computed by Cupel).

### 3. cupel:source-type convention is fully specified

1. Open `spec/src/scorers/metadata-trust.md`.
2. Locate the `cupel:source-type` subsection under "Conventions".
3. **Expected:** The convention defines an open string (not a closed enum) with exactly 4 RECOMMENDED values: "user", "tool", "external", "system".

### 4. Algorithm uses configurable defaultScore throughout

1. Open `spec/src/scorers/metadata-trust.md`.
2. Locate the "Algorithm" section (pseudocode block).
3. **Expected:** The pseudocode references `config.defaultScore` (or equivalent) for all fallback paths — key-missing, parse-failure, and non-finite. No hardcoded literal `0.0` or `0.5` appears as the default in the algorithm.

### 5. Clamping order is explicit

1. Open `spec/src/scorers/metadata-trust.md`, Algorithm section.
2. **Expected:** The algorithm specifies the following order: (1) key missing → return defaultScore; (2) parse failure → return defaultScore; (3) non-finite value → return defaultScore; (4) clamp result to [0.0, 1.0].

### 6. Anti-gate language is present

1. Open `spec/src/scorers/metadata-trust.md`.
2. **Expected:** The chapter explicitly states that trust is a scoring input only and that items are never excluded or filtered based on the trust score.

### 7. Conformance vector outlines are present and sufficient

1. Open `spec/src/scorers/metadata-trust.md`, "Conformance Vector Outlines" section.
2. **Expected:** At least 3 narrative scenarios are present. Scenarios cover: (a) a present and valid `cupel:trust` value returned directly after clamping; (b) a missing key returning `defaultScore`; (c) an unparseable or non-finite value returning `defaultScore`.

### 8. Chapter is reachable via SUMMARY.md

1. Run: `grep "metadata-trust" spec/src/SUMMARY.md`
2. **Expected:** Line contains `[MetadataTrustScorer](scorers/metadata-trust.md)` as a navigation entry, positioned after the ScaledScorer entry.

### 9. scorers.md index is updated

1. Open `spec/src/scorers.md`.
2. Verify: MetadataTrustScorer row in Scorer Summary table (with input field, output range, and description columns populated).
3. Verify: MetadataTrustScorer listed in the Absolute Scorers category section.
4. **Expected:** Both entries are present and accurate.

## Edge Cases

### No TBD fields anywhere in chapter

1. Run: `grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md`
2. **Expected:** Output is `0`.

### mdBook renders the chapter correctly (optional local build)

1. Run: `mdbook build spec/`
2. Open the generated book and navigate to the MetadataTrustScorer chapter via the sidebar.
3. **Expected:** Chapter renders with all sections visible; no broken links.

## Failure Signals

- `test -f spec/src/scorers/metadata-trust.md` fails — chapter file was not created or was deleted
- `grep -ci "\bTBD\b"` returns non-zero — incomplete spec with placeholder fields
- `grep -q "metadata-trust" spec/src/SUMMARY.md` fails — chapter is silently dropped from mdBook
- Algorithm section shows hardcoded `0.0` as default rather than `config.defaultScore` — configurability requirement violated
- No normative language (MUST NOT) in namespace reservation section — reservation is advisory only, not normative
- `cargo test` or `dotnet test` fails — spec change accidentally introduced regression

## Requirements Proved By This UAT

- R042 — Proves that the metadata convention system spec is complete: `"cupel:"` namespace reserved normatively; `cupel:trust` and `cupel:source-type` conventions defined with precision; `MetadataTrustScorer` algorithm specified with configurable default, explicit fallback order, and clamping; no TBD fields; chapter reachable via SUMMARY.md; scorers.md updated; no test regressions.

## Not Proven By This UAT

- Implementation correctness — no Rust or .NET implementation of `MetadataTrustScorer` exists; runtime behavior against the spec is not tested here.
- TOML conformance vector execution — vectors are in narrative form only; automated conformance test execution is not possible until the conformance schema supports `metadata` fields.
- Cross-language wire format interoperability — the string-vs-double `cupel:trust` handling is specified but not exercised across language boundaries by this UAT.
- mdBook CI rendering — only verified locally if the reviewer runs `mdbook build spec/`; no CI step enforces successful mdBook build at present.

## Notes for Tester

- This is a spec-only slice. The UAT is a human document review against the checklist above.
- Pay particular attention to: (1) the normative MUST NOT in namespace reservation — "SHOULD NOT" or "is discouraged" would be insufficient; (2) the anti-gate language — any wording that implies items could be excluded based on trust score violates the design intent; (3) the open-string nature of `cupel:source-type` — a closed enum would be a spec error.
- The `rtk dotnet test` wrapper is incompatible with TUnit; use bare `dotnet test` for regression verification.
