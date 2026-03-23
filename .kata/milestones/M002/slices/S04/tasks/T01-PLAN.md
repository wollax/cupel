---
estimated_steps: 5
estimated_files: 1
---

# T01: Write MetadataTrustScorer spec chapter

**Slice:** S04 — Metadata Convention System Spec
**Milestone:** M002

## Description

Write `spec/src/scorers/metadata-trust.md` — the complete MetadataTrustScorer spec chapter. This is the primary R042 deliverable: reserving the `"cupel:"` metadata namespace, defining the `cupel:trust` and `cupel:source-type` conventions, and specifying the MetadataTrustScorer algorithm with all edge cases, conformance notes, and conformance vector outlines.

The closest template is `spec/src/scorers/reflexive.md` — both are absolute scorers that read a single item field, clamp to [0.0, 1.0], and return. Key differences: MetadataTrustScorer reads from `metadata["cupel:trust"]` (a string map key), the default is configurable (not hardcoded), and parse failure must be explicitly specified.

No code changes. Pseudocode in `text` fenced blocks only.

## Steps

1. **Draft the chapter scaffold** following `reflexive.md` structure: title, overview paragraph, Fields Used table, algorithm, score interpretation, edge cases table, complexity, conformance notes. Add two new sections not in reflexive.md: "Metadata Namespace Reservation" (after overview) and "Conventions" (before algorithm), and "Conformance Vector Outlines" (before complexity).

2. **Write the Metadata Namespace Reservation section** — normative MUST NOT statement: all `ContextItem.metadata` keys with the `"cupel:"` prefix are reserved for library-defined conventions; callers MUST NOT use this prefix for application-specific keys; reservation is spec-level with no runtime enforcement.

3. **Write the Conventions section** covering both conventions:
   - `cupel:trust`: float64 in [0.0, 1.0] representing caller-computed trust; stored as a decimal string in Rust (`HashMap<String, String>`) for cross-language conformance; .NET callers MAY store as `double` directly (native `IReadOnlyDictionary<string, object?>` supports it) but string is the canonical wire format for serialization interop; caller-computed (not pipeline-computed); the scorer reads it but never validates it as a semantic gate.
   - `cupel:source-type`: open string convention for labeling the origin of an item; four RECOMMENDED values: `"user"` (end-user message), `"tool"` (tool/function call result), `"external"` (retrieved/injected context), `"system"` (system prompt fragment); values are not validated at construction time; callers MAY use other values.

4. **Write the Algorithm section** with pseudocode:
   ```text
   METADATA-TRUST-SCORE(item, allItems, config):
       if item.metadata does not contain "cupel:trust":
           return config.defaultScore

       raw <- item.metadata["cupel:trust"]   // string in Rust; string or double in .NET

       value <- parse_float64(raw)           // or cast if already float64

       if parse failed:
           return config.defaultScore

       if value is not finite:               // NaN, +infinity, -infinity
           return config.defaultScore

       return clamp(value, 0.0, 1.0)
   ```
   Where `clamp(x, lo, hi)` returns `lo` if `x < lo`, `hi` if `x > hi`, and `x` otherwise. `config.defaultScore` is a float64 in [0.0, 1.0] set at construction time.

5. **Write the Edge Cases table, Conformance Notes, and Conformance Vector Outlines**:
   - Edge cases: key missing, unparseable string (e.g. `"high"`, `""`), NaN value, ±infinity, valid 0.0, valid 0.75, valid 1.0, value -0.1 (clamped), value 1.5 (clamped)
   - Conformance notes: (a) clamping order is key-missing → default, parse-failure → default, non-finite → default, then clamp; (b) non-finite values MUST return `config.defaultScore`, not be clamped to a boundary; (c) in Rust, the metadata value MUST be parsed as a decimal string; (d) in .NET, the value MAY be stored as `double` — implementations MUST handle both `string` and `double` (or `object` boxing) in the `IReadOnlyDictionary<string, object?>` lookup
   - Conformance vector outlines (narrative, 3–5 scenarios): (1) item with `cupel:trust = "0.85"` → score 0.85; (2) item with `cupel:trust` key absent → score = defaultScore; (3) item with `cupel:trust = "high"` (unparseable) → score = defaultScore; (4) item with `cupel:trust = "1.5"` (out of range, clamped) → score 1.0; (5) item with `cupel:trust = "NaN"` or non-finite → score = defaultScore

## Must-Haves

- [ ] `spec/src/scorers/metadata-trust.md` exists
- [ ] `"cupel:"` namespace reservation is stated as a normative MUST NOT (no runtime enforcement caveat included)
- [ ] `cupel:trust` defined with float64 range, string-storage note for Rust, .NET dual-type note
- [ ] `cupel:source-type` defined as open string with 4 RECOMMENDED values (not a closed enum)
- [ ] Algorithm pseudocode uses `config.defaultScore` — no hardcoded 0.0 or other literal
- [ ] Clamping order explicitly stated: key-missing → default; parse-failure → default; non-finite → default; clamp
- [ ] Parse-failure behavior explicitly specified (unparseable string → defaultScore, not throw)
- [ ] No trust gate language (scorer produces a score; no filtering or exclusion described)
- [ ] Conformance vector outlines section present with ≥3 narrative scenarios
- [ ] `grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md` → 0
- [ ] Pseudocode in `text` fenced blocks (no language-specific fences)

## Verification

```bash
# File exists
test -f spec/src/scorers/metadata-trust.md && echo "PASS: file exists"

# No TBD fields
count=$(grep -ci "\bTBD\b" spec/src/scorers/metadata-trust.md)
[ "$count" -eq 0 ] && echo "PASS: no TBD" || echo "FAIL: $count TBD fields"

# Configurable default (not hardcoded)
grep -q "defaultScore" spec/src/scorers/metadata-trust.md && echo "PASS: configurable default"

# Namespace reservation present
grep -q "MUST NOT" spec/src/scorers/metadata-trust.md && echo "PASS: normative statement"

# source-type convention present
grep -q "cupel:source-type" spec/src/scorers/metadata-trust.md && echo "PASS: source-type defined"

# Conformance vector outlines present
grep -q "Conformance Vector" spec/src/scorers/metadata-trust.md && echo "PASS: outlines present"

# No trust gate language
grep -qi "exclud\|filter\|gate\|block" spec/src/scorers/metadata-trust.md && echo "WARNING: check for trust gate language" || echo "PASS: no gate language found"
```

## Observability Impact

- Signals added/changed: None (spec/doc only)
- How a future agent inspects this: `grep` commands above; `mdbook build spec/` renders the chapter; check chapter is reachable in built HTML
- Failure state exposed: None (spec authoring task; failure = file missing or TBD fields remaining)

## Inputs

- `spec/src/scorers/reflexive.md` — algorithmic template (absolute scorer, single-field passthrough, clamp, null handling)
- `spec/src/scorers/tag.md` — metadata key-lookup pattern and case-sensitivity note
- `spec/src/data-model/context-item.md` — metadata field definition (string-to-any map, opaque to pipeline, constraint 4)
- `crates/cupel/src/model/context_item.rs` — confirms `metadata: HashMap<String, String>` in Rust
- `src/Wollax.Cupel/ContextItem.cs` — confirms `metadata: IReadOnlyDictionary<string, object?>` in .NET
- S04-RESEARCH.md — all design decisions resolved (no open questions); recommendations for algorithm, type-system note, and namespace reservation

## Expected Output

- `spec/src/scorers/metadata-trust.md` — complete MetadataTrustScorer spec chapter with all sections, no TBD fields, configurable defaultScore, explicit parse-failure and non-finite handling, 3–5 conformance vector outlines, and .NET/Rust type notes
