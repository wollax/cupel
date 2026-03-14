# Phase 12: Rust Crate (Assay) - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Implement the Cupel specification as an idiomatic Rust crate (`assay`) in the separate `wollax/assay` repository. The crate must pass all 28 required conformance test vectors from the spec. This phase delivers the crate and conformance — integration into the assay project and crates.io publishing are out of scope.

</domain>

<decisions>
## Implementation Decisions

### API idiom fidelity
- Fully idiomatic Rust: snake_case functions, Rust naming conventions throughout
- Spec type names (ContextItem, ContextBudget, etc.) appear only in docs/comments for traceability
- Rust types use idiomatic names (e.g., `context_item`, `ContextItem` struct following Rust's PascalCase for types, snake_case for functions/methods)

### Conformance tier targeting
- All 28 required conformance tests must pass for v1.0
- 9 optional conformance tests are NOT targeted — can come in a point release
- Required-only scope validates the spec's language-independence without over-committing

### Assay project integration
- Crate lives in the separate `wollax/assay` GitHub repository, not in the cupel monorepo
- No crates.io publishing in this phase — path dependency only
- Phase is complete when the crate exists and passes conformance; assay project integration is separate work

### Claude's Discretion
- **Builder pattern**: Typestate vs runtime-validated builder — pick what's most ergonomic for Rust
- **Error modeling**: Single enum vs granular per-module error types — pick based on API surface
- **Trait extensibility**: Whether Scorer/Slicer/Placer traits are public from day one or pub(crate) initially
- **Crate structure**: Single crate with feature flags vs workspace — pick based on scope
- **Serde support**: Whether to include behind a feature flag or skip for v1.0
- **Async/streaming**: Whether to include StreamSlice equivalent (behind feature flag) or sync-only
- **MSRV policy**: Pick a sensible minimum supported Rust version
- **Conformance test consumption**: Inline TOML vectors vs git submodule
- **Conformance runner**: Standalone example binary vs `cargo test` only
- **CI/CD**: Level of GitHub Actions automation for the assay repo

</decisions>

<specifics>
## Specific Ideas

- This is the first non-C# implementation — its primary purpose is validating that the spec is truly language-agnostic
- The crate name is `assay` (matching the existing GitHub repo `wollax/assay`)
- Conformance test vectors are TOML files defined in Phase 11's spec

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 12-rust-crate-assay*
*Context gathered: 2026-03-14*
