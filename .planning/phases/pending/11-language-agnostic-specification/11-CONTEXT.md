# Phase 11: Language-Agnostic Specification - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Document Cupel's context selection algorithm (Classify → Score → Deduplicate → Slice → Place) as a formal, language-agnostic specification. Define conformance test vectors so implementations can validate correctness. Publish as a GitHub Pages site. The Rust implementation (Phase 12) is the first consumer — it builds against this spec, not the C# code.

</domain>

<decisions>
## Implementation Decisions

### Document structure & format
- Audience: both readers (understanding the algorithm) and implementors (building against the spec) equally
- Claude has discretion on single vs multi-document structure, Markdown vs typeset format, and whether to include diagrams
- Structure should serve the dual audience — conceptual overviews followed by precise specifications

### Algorithmic precision level
- Behavioral equivalence, not bit-exact output — same items selected with same ordering, but floating-point scores may differ within epsilon
- IEEE 754 64-bit doubles mandated for all numeric scoring computations
- Claude has discretion on notation style (prose, pseudocode, or both) per algorithm, and on tiebreaking rules (prescribed vs implementation-defined)

### Conformance suite design
- TOML format for test vector fixtures
- Both per-stage vectors (scoring, slicing, placing individually) and end-to-end pipeline vectors
- Tiered conformance: required test vectors for core behavior, optional vectors for edge cases — implementations can claim partial conformance
- Claude has discretion on whether the suite lives inline in the spec or as a separate directory

### Publication & versioning
- Published as a GitHub Pages site for rendered reading experience
- Claude has discretion on: repo location (in this repo vs separate), versioning strategy (coupled vs independent from library), and whether to retroactively validate the C# implementation against the conformance suite in this phase

### Claude's Discretion
- Document format choice (Markdown, Typst, or other)
- Single document vs multi-document split
- Diagram inclusion and style
- Algorithm notation per section (prose, pseudocode, mathematical, or combination)
- Tiebreaking: prescribed rules vs implementation-defined
- Conformance suite location (inline vs separate directory)
- Spec versioning strategy relative to library versions
- Whether C# implementation is validated against conformance suite in this phase or deferred
- Spec repo location (in cupel repo or standalone)

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 11-language-agnostic-specification*
*Context gathered: 2026-03-14*
