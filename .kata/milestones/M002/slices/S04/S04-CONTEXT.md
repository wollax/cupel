---
id: S04
milestone: M002
status: ready
---

# S04: Metadata Convention System Spec — Context

## Goal

Write `spec/src/scorers/metadata-trust.md`, reserve the `"cupel:<key>"` namespace, fully specify `cupel:trust` and `cupel:source-type` conventions, and link the chapter from `spec/src/SUMMARY.md`.

## Why this Slice

Without a canonical metadata key namespace, every caller who wants to express trust invents their own schema — the ecosystem fragments before `MetadataTrustScorer` is even implemented. Reserving the namespace and specifying the first two conventions now means callers can start labelling items with `cupel:trust` and `cupel:source-type` today (before M003), and the implementation will honour the spec they already used. S04 has no dependencies and is a pure additive spec chapter.

## Scope

### In Scope

- `spec/src/scorers/metadata-trust.md` — a complete spec chapter containing:
  - `"cupel:<key>"` namespace reservation with normative statement (callers MUST NOT use this prefix for non-Cupel conventions)
  - `cupel:trust` convention: float64, range [0.0, 1.0], caller-computed, represents provenance/reliability trust; Cupel does not compute or validate trust scores
  - `cupel:source-type` convention: fully specified string enum with all 4 values and their semantics:
    - `"user"` — content supplied directly by a human user
    - `"tool"` — content produced by an automated tool or function call
    - `"external"` — content retrieved from an external data source (e.g. search, database, web)
    - `"system"` — content injected by the system/framework (e.g. system prompts, scaffolding)
  - `MetadataTrustScorer` algorithm: reads `cupel:trust`; returns the value directly when present; returns configurable default (mandated default: **0.5**, caller may override at construction); no trust gates — trust is a scoring input only, never a filter
  - Conformance vector outlines: 3-5 scenarios covering trust present, trust absent (default applies), `cupel:source-type` annotation, and optionally trust combined with other scorers
- `spec/src/SUMMARY.md` updated to include the new chapter under Scorers
- No `.planning/issues/open/` issues to close for S04 (standalone new chapter, no prior issue filed)

### Out of Scope

- `MetadataTrustScorer` implementation (no .NET or Rust code — D039 is locked)
- Any additional `cupel:<key>` conventions beyond `cupel:trust` and `cupel:source-type`
- Trust gates or trust-based filtering — trust is a scoring dimension only, not a filter; this is a hard design constraint from the March 15 brainstorm
- Future conventions like `cupel:priority`, `cupel:recency`, etc. — namespaced in the spec as "reserved for future use" only, no definition in S04
- Conformance vector TOML files in `spec/conformance/` — S04 writes conformance vector *outlines* in the spec chapter (scenario descriptions and expected outcomes), not the executable TOML vectors; those are implementation-phase concerns

## Constraints

- **No trust gates**: `MetadataTrustScorer` MUST NOT specify any behavior where an item is excluded because its trust score is below a threshold. All trust effects go through the scoring dimension only. This is non-negotiable per the March 15 debate.
- **`cupel:source-type` is fully specified**: all 4 enum values (`user`, `tool`, `external`, `system`) must have clear semantics. The chapter should specify what happens when an unknown value is present (implementation ignores it / treats as absent per "opaque metadata" principle from `context-item.md`).
- **`cupel:trust` default is 0.5**: the spec must state that if `cupel:trust` is missing or unparseable, `MetadataTrustScorer` returns 0.5 (neutral) unless the caller configures a different default at construction. This is the mandated built-in default.
- **Metadata is opaque to the core pipeline**: the existing `context-item.md` spec says the pipeline MUST NOT read or modify metadata. `MetadataTrustScorer` is a scorer (Score stage), not a pipeline stage — it reads metadata within its scorer scope without violating the opaque-pipeline invariant. The chapter should make this clear.
- **Chapter follows established spec style**: pseudocode in labeled `text` fenced blocks; conformance notes in a designated section; normative keywords (MUST/SHOULD/MAY) used per the existing scorer spec conventions.

## Integration Points

### Consumes

- `spec/src/data-model/context-item.md` — the `metadata: map<string, any>` field definition and the opaque-to-pipeline invariant; the metadata-trust chapter must be consistent with this
- `spec/src/scorers/` — existing scorer spec files as style reference (e.g. `tag.md`, `frequency.md`, `kind.md`) for chapter structure and pseudocode formatting
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/radical-report.md` (Surviving Proposal 1) — canonical framing of the metadata convention design and the "no trust gates" ruling; read before writing

### Produces

- `spec/src/scorers/metadata-trust.md` — complete spec chapter (no stubs)
- `spec/src/SUMMARY.md` — updated with the new chapter entry

## Open Questions

- **Unknown `cupel:source-type` values**: the `context-item.md` spec says metadata is opaque to the pipeline. By analogy, when `MetadataTrustScorer` encounters an unrecognized `cupel:source-type` value (e.g. a future enum value or a typo), it should ignore it rather than error. The chapter should state this explicitly. Working assumption: `cupel:source-type` is informational — MetadataTrustScorer does not read it; other future scorers might. Verify this during execution: does S04 need to specify that `MetadataTrustScorer` ignores `cupel:source-type`, or are they independent conventions that may be consumed by different scorers?
- **Conformance vector TOML deferral**: the roadmap says "conformance vector outlines (3-5 scenarios)." Working assumption: outlines live in the spec chapter as prose + expected outcome tables; the executable TOML vectors in `spec/conformance/` are M003 implementation-phase work. Confirm this interpretation doesn't leave the spec incomplete.
