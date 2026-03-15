# Phase 23: API Hardening Foundations - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship `#[non_exhaustive]` on `CupelError` and `OverflowStrategy`, derive `Debug`/`Clone`/`Copy` on concrete slicer/placer structs, add `ContextKind` factory methods and `TryFrom<&str>`, and expose `ContextBudget::unreserved_capacity()` — all before any new error variants are introduced.

Requirements: RAPI-01, RAPI-02, RAPI-03, RAPI-04, RAPI-05

</domain>

<decisions>
## Implementation Decisions

### ContextKind string mapping
- Case sensitivity, alias acceptance, and canonical name set for `TryFrom<&str>` are at Claude's discretion
- Error type for unrecognized strings (dedicated `ParseContextKindError` vs reusing `CupelError`) is at Claude's discretion
- Whether factory methods (`message()`, `system_prompt()`, etc.) are `const fn` is at Claude's discretion

### Trait derivation breadth
- Whether slicer/placer structs get `PartialEq`/`Eq`/`Hash`/`Default` beyond the required `Debug`/`Clone`/`Copy` is at Claude's discretion — decide based on field composition and whether the traits are supportable
- Whether `ContextKind` gets additional derives for consistency with slicer/placer structs is at Claude's discretion — audit current derives and fill gaps

### unreserved_capacity() semantics
- Whether to add convenience methods like `has_capacity() -> bool` is at Claude's discretion
- Arithmetic approach (saturating vs plain subtraction) is at Claude's discretion
- Whether `#[must_use]` lands in Phase 23 or defers to Phase 32's full audit is at Claude's discretion
- Whether to also expose `total_reserved()` alongside `unreserved_capacity()` is at Claude's discretion

### .NET parity alignment
- Cross-language naming consistency (Rust idiom vs exact .NET mirror) is at Claude's discretion — balance cross-language familiarity with Rust conventions
- RAPI-05 sequencing (Rust-first then .NET, or design-both-now) is at Claude's discretion
- Cross-language conformance testing timing (Phase 23 vs Phase 25/31) is at Claude's discretion — respect the phase sequencing constraints
- Whether Rust `ContextKind` `TryFrom<&str>` accepts the same string set as .NET parsing is at Claude's discretion — check what .NET does and align or diverge as appropriate

### Claude's Discretion
All four discussion areas were delegated to Claude's judgment. The guiding principles:
- Prefer idiomatic Rust patterns
- Derive traits when field composition supports them and they cost nothing
- Keep API surface minimal but useful
- Respect cross-language consistency where it doesn't conflict with language idiom
- Follow the phase sequencing constraints in the roadmap

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches. The roadmap success criteria are highly prescriptive and serve as the specification.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 23-api-hardening-foundations*
*Context gathered: 2026-03-15*
