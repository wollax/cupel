# Phase 25: Conformance Infrastructure & Drift Guard - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Establish the CI conformance drift guard that diffs `spec/conformance/` against `crates/cupel/conformance/` and fails on divergence, fix misleading comments in 5 existing conformance vector TOML files, and create the diagnostics conformance vector schema with an example vector.

Requirements: CONF-01, CONF-02, CI-03, SPEC-02

</domain>

<decisions>
## Implementation Decisions

### Drift guard mechanism (CONF-02, CI-03)
- New step in existing `ci-rust.yml`, not a separate workflow
- Recursive `diff -rq` comparing `spec/conformance/required/` against `crates/cupel/conformance/required/` AND `spec/conformance/optional/` against `crates/cupel/conformance/optional/`
- Both `required/` and `optional/` directories are guarded from day one
- CI trigger paths expanded to include `spec/**` alongside the existing `crates/**` paths, so any spec change triggers the Rust CI (including the drift guard)

### Diagnostics conformance vector schema (SPEC-02)
- Full schema definition matching the diagnostics spec chapter (SelectionReport fields: included/excluded items with reasons, total_candidates, total_tokens_considered)
- Documented by extending `spec/src/conformance/format.md` with a new "Diagnostics Vectors" section — single source of truth for all vector formats
- Pipeline-level vectors only — no stage-level diagnostic vectors. Diagnostic vectors test `run_traced` output
- Example vector has correct schema shape with TBD placeholder values; real implementation validation deferred to Phase 29
- No separate SCHEMA.md file

### Comment fixes (CONF-01)
- Minimal corrections — fix factual errors, remove conversational debris, clarify ambiguous notation. Don't rewrite correct comments
- `knapsack-basic.toml`: Delete the abandoned first scenario (lines 6-28 with `expensive/cheap-a/cheap-b` items and the incorrect greedy comparison) entirely. Keep only the redesigned scenario that matches the actual test data
- `composite-weighted.toml`: Remove the `# Wait — let me recompute priority` scratchpad text
- `pinned-items.toml`: Add explicit density-sort step to greedy fill trace; clarify `new(score=1.0)` score source
- `u-shaped-basic.toml` and `u-shaped-equal-scores.toml`: Replace ambiguous `right[N]`/`left[N]` notation with `result[N]` indexing
- All fixes applied to both `spec/conformance/` and `crates/cupel/conformance/` copies simultaneously to maintain byte-exact parity
- No comment style guide added to format.md — out of scope

### Claude's Discretion
- U-shaped vector notation: user said "you decide" — using `result[N]` indexing (matches output array semantics, avoids confusion with spec pseudocode pointer variables)
- Exact wording of corrected comments
- Ordering of CI steps within ci-rust.yml
- Whether drift guard step runs before or after tests
- TOML field ordering within the diagnostics vector schema definition

</decisions>

<specifics>
## Specific Ideas

No specific requirements — open to standard approaches

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 25-conformance-infrastructure-drift-guard*
*Context gathered: 2026-03-15*
