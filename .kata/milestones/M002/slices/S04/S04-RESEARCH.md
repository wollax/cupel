# S04: Metadata Convention System Spec — Research

**Date:** 2026-03-21
**Requirement:** R042

## Summary

S04 is a self-contained spec authoring task: write `spec/src/scorers/metadata-trust.md` and update `spec/src/SUMMARY.md`. No code changes. The deliverable is a new scorer spec chapter following the established mdBook style and one SUMMARY.md edit.

The MetadataTrustScorer is structurally the closest scorer to `ReflexiveScorer` — both are absolute scorers that read a single item field and return it directly with clamping and a configurable default for missing values. The main complexity in S04 is not the algorithm (trivial) but the two cross-cutting design decisions that must be resolved in the spec text: (1) how trust values are represented across the Rust/C# type split in `metadata`, and (2) how the `"cupel:<key>"` namespace reservation is stated.

The spec chapter should also address `cupel:source-type` as a documented convention (string enum with four RECOMMENDED values), but this convention does not require its own scorer — it is a labeling convention for caller use.

## Recommendation

Write the spec chapter in the style of `ReflexiveScorer` (absolute scorer, single-field read, clamp to [0.0, 1.0], configurable default). Make two explicit design decisions in the chapter text:

1. **Trust value representation in metadata**: Since Rust's `metadata` is `HashMap<String, String>`, trust must be stored as a decimal string (e.g., `"0.85"`). The spec MUST define parse-failure behavior: treat unparseable values as missing (use `defaultScore`). This aligns with the "opaque to the pipeline" contract — the pipeline never validates metadata values.

2. **Namespace reservation**: State normatively that all `ContextItem.metadata` keys with the `"cupel:"` prefix are reserved for library-defined conventions. Callers MUST NOT use this prefix for application-specific keys. This is a spec-level reservation with no runtime enforcement.

Both decisions are low-controversy (no open questions remain from DECISIONS.md), so this slice should proceed directly to execution without a debate phase.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Scorer spec chapter structure | `spec/src/scorers/reflexive.md` | Closest algorithmic analog — absolute scorer, field passthrough, clamp, null handling |
| SUMMARY.md entry format | Existing SUMMARY.md scorer section | Pattern: `  - [MetadataTrustScorer](scorers/metadata-trust.md)` after ScaledScorer |
| Pseudocode style | All existing scorer chapters | Labeled `text` fenced blocks, consistent identifier naming |
| Edge cases table | All existing scorer chapters | Markdown pipe table with Condition/Result columns |

## Existing Code and Patterns

- `spec/src/scorers/reflexive.md` — The direct template. ReflexiveScorer is an absolute scorer that reads `futureRelevanceHint`, clamps to [0.0, 1.0], treats null as 0.0 (hardcoded). MetadataTrustScorer differs in: (a) reads from `metadata["cupel:trust"]`, (b) default is configurable (not hardcoded), (c) must parse string to float64 in the Rust type model.
- `spec/src/scorers/tag.md` — Shows how to handle the metadata key-lookup pattern (string map, case-sensitive).
- `spec/src/data-model/context-item.md` — Metadata is described as "map of string to any"; the Rust implementation uses `HashMap<String, String>` while .NET uses `IReadOnlyDictionary<string, object?>`. The spec must acknowledge string-valued storage for cross-language conformance.
- `spec/src/SUMMARY.md` — Add entry after ScaledScorer line. Full scorers block is under the `- [Scorers](scorers.md)` heading.
- `spec/src/scorers.md` — The scorer index page. Will need a new row in the `Scorer Summary` table for MetadataTrustScorer.
- `crates/cupel/src/model/context_item.rs` — Rust `metadata` is `HashMap<String, String>`. Trust floats must be stored as decimal string representations (e.g., `"0.75"`).
- `src/Wollax.Cupel/ContextItem.cs` — .NET `metadata` is `IReadOnlyDictionary<string, object?>`. Trust values can be stored as `double` directly, but for cross-language conformance the spec should standardize on string representation at the data model level.

## Constraints

- **No implementation code.** D039: M002 is design-only. The spec chapter defines the algorithm; no Rust or .NET scorer class may be written from this slice.
- **Pseudocode in `text` fenced blocks.** All existing scorer chapters use this format. Do not use language-specific fenced blocks (```csharp, ```rust).
- **SUMMARY.md is the reachability gate.** mdBook silently drops chapters not listed in SUMMARY.md. The chapter must be linked from SUMMARY.md before the slice is done.
- **scorers.md index must be updated.** The `Scorer Summary` table in `spec/src/scorers.md` lists all scorers — MetadataTrustScorer must be added as a new row.
- **No trust gates.** Per R042 notes: "Trust is a scoring input, not a filter. No trust gates (silent exclusion) in this spec." The chapter must not describe any behavior that excludes items based on trust — the scorer produces a score, and slicing/scoring does the rest.
- **`"cupel:"` namespace is spec-reserved, not runtime-enforced.** The pipeline does not validate metadata key prefixes. The reservation is a normative statement in the spec, not a runtime check.
- **source-type is a convention, not a scorer.** `cupel:source-type` is documented in the chapter (alongside `cupel:trust`) as a labeling convention, but there is no `SourceTypeScorer`. The caller uses `cupel:source-type` for application logic; the spec documents its canonical values.
- **Conformance vectors: metadata field not in current format schema.** The TOML conformance vector schema in `spec/src/conformance/format.md` does not list `metadata` as a supported `[[items]]` field. The spec chapter should include _outline_ conformance scenarios (narrative form, 3-5 scenarios) rather than full TOML vectors, since the format schema extension is out of S04 scope. Mark them as "Conformance Vector Outlines" consistent with the roadmap language.

## Common Pitfalls

- **Forgetting to update `scorers.md` index** — The `Scorer Summary` table in `spec/src/scorers.md` needs a new row, not just the SUMMARY.md entry. It is easy to miss since it's a separate file from SUMMARY.md.
- **Hardcoding the default to 0.0** — ReflexiveScorer hardcodes null → 0.0 but MetadataTrustScorer must make `defaultScore` configurable (per R042 intent and brainstorm DecayScorer precedent). The algorithm must reference `config.defaultScore`, not a literal.
- **Ambiguous parse-failure behavior** — If `metadata["cupel:trust"]` is present but not a valid float string (e.g., `"high"`, `""`), the spec must say exactly what happens. Recommendation: treat unparseable as missing → use `defaultScore`. Do not throw.
- **Type system mismatch unaddressed** — If the chapter silently assumes float64 without noting the string-serialization requirement, Rust implementors will be surprised. A short Conformance Note on string representation is needed.
- **source-type closed vs open** — `cupel:source-type` should be an open string with RECOMMENDED values, not a closed enum validated at construction time. Closing it now prevents future additions without a spec revision.
- **Clamping order** — As with ReflexiveScorer, the check order must be: key missing → default; parse failure → default; value not finite → default (or 0.0); then clamp to [0.0, 1.0]. Non-finite values (NaN, ±∞) must return the default, not be clamped.

## Open Risks

- **`.NET metadata value type`**: .NET callers can store `double` directly in `IReadOnlyDictionary<string, object?>`. If the spec says "values are strings," it creates friction for .NET callers who don't need to stringify. One option: the spec says the _canonical wire format_ is string (for serialization interop), but the .NET implementation MAY accept `double` as well. This needs a clear ruling in the spec chapter to avoid implementation drift.
- **Conformance vector format gap**: The TOML `[[items]]` schema doesn't include `metadata`. Full TOML conformance vectors for MetadataTrustScorer require a format.md extension (adding `[items.metadata]` key-value map). This is a minor spec schema task. If the format extension is not done in S04, the outline conformance scenarios are sufficient; the format extension can be done in the same PR or a follow-up.
- **`scorers.md` output range for MetadataTrustScorer**: The scorer is `[0.0, 1.0]` (clamped), same as ReflexiveScorer. No ambiguity here, but worth confirming with the clamp-before-return in the pseudocode.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| mdBook (Markdown spec authoring) | — | none found (no specialized skill needed; pure Markdown) |

## Sources

- Metadata type is `HashMap<String, String>` in Rust (source: `crates/cupel/src/model/context_item.rs:37`)
- Metadata type is `IReadOnlyDictionary<string, object?>` in .NET (source: `src/Wollax.Cupel/ContextItem.cs`)
- ReflexiveScorer is the closest algorithmic analog (source: `spec/src/scorers/reflexive.md`)
- No trust gates policy (source: R042 notes in `REQUIREMENTS.md`)
- `"cupel:"` namespace reservation and `cupel:trust`/`cupel:source-type` conventions are the full R042 scope (source: M002-ROADMAP.md S04 boundary map)
- Metadata is opaque to pipeline — MUST NOT be read or modified by pipeline stages (source: `spec/src/data-model/context-item.md` constraint 4) — MetadataTrustScorer reads metadata at scoring time (scorer, not pipeline stage) so this constraint is not violated
- Conformance vector schema does not currently support metadata fields in `[[items]]` (source: `spec/src/conformance/format.md`)
