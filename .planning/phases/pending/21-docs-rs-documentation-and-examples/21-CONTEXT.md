# Phase 21: docs.rs Documentation & Examples - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Make `cupel` discoverable and approachable on docs.rs with crate-level quickstart documentation, module-level doc comments, runnable examples, and updated READMEs. Users should be able to understand what cupel does, how to use it, and which components to pick — all from the docs.rs landing page and GitHub repo.

</domain>

<decisions>
## Implementation Decisions

### Quickstart narrative
- **Audience:** Both Rust-experienced LLM devs and LLM devs new to Rust — layer the docs for both
- **Framing:** Conceptual intro paragraph explaining the pipeline model (score → slice → place) and why it matters, then code
- **Example structure:** Two layered examples — minimal "hello world" pipeline first, then a realistic multi-scorer configuration below it
- **Serde mention:** Note that serde support exists with a link to feature flag docs, but no inline code in the quickstart

### Module doc depth
- **Baseline:** Every module gets purpose + type list + mini code example (`//!` doc comments)
- **Extra attention:** scorer, pipeline, model, and slicer modules deserve deeper treatment
- **Scorer guide:** Include a comparison table mapping use cases to scorers (e.g., "Chat history → RecencyScorer")
- **Doctests:** Every public struct and trait gets at least one compilable doctest

### Example scope
- **Count:** Three standalone examples in `examples/`:
  1. `basic_pipeline.rs` — core pipeline flow
  2. `serde_roundtrip.rs` — serialization/deserialization with the serde feature
  3. `quota_slicing.rs` — advanced per-kind budget allocation with QuotaSlice
- **Data style:** Mix — realistic LLM-domain names/kinds (user message, system prompt, retrieved doc) but short content
- **Output:** Print results only (println!), no asserts — examples are tutorials, not tests
- **Comments:** Commented walkthrough style — each section gets a `//` comment explaining what's happening and why

### Doc tone & voice
- **Style reference:** Tokio — layered depth: quick overview for experts, detailed guides for learners
- **Terminology:** Brief glossary in crate-level docs defining key terms (context window, token budget, RAG), then use freely
- **External links:** Claude's discretion — link when it genuinely adds value, avoid stale blog posts

### README
- **Crate README** (`crates/cupel/README.md`): Mirror the lib.rs quickstart — single source of truth, same content on crates.io and docs.rs
- **Repo root README**: Multi-language project overview — cupel as a project spanning .NET packages + Rust crate + conformance spec, with links to each ecosystem's docs

### Claude's Discretion
- Pipeline stage framing: full 6-stage names vs. grouped 3-phase simplification — pick per context
- External link inclusion — case-by-case judgment
- `[package.metadata.docs.rs]` configuration details
- Error and placer module doc depth (baseline treatment is sufficient)

</decisions>

<specifics>
## Specific Ideas

- Tokio-style layered documentation: quick scan for experienced devs, expandable depth for newcomers
- Scorer comparison table is a key differentiator — users struggle with "which scorer for my use case"
- README should position cupel as a multi-language project, not just a Rust crate

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 21-docs-rs-documentation-and-examples*
*Context gathered: 2026-03-15*
