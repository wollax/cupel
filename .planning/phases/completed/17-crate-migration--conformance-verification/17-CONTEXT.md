# Phase 17: Crate Migration & Conformance Verification - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Move all Rust source files from `wollax/assay` (`crates/assay-cupel/src/`) into `cupel/crates/cupel/src/`, copy conformance test vectors and test runner, update paths and imports, and verify the complete crate compiles, passes all conformance tests, and packages correctly for publishing.

</domain>

<decisions>
## Implementation Decisions

### Conformance vector placement
- Copy the 28 required vectors from repo-root `conformance/required/` into `crates/cupel/conformance/required/`
- Optional vectors (9) stay at repo-root only — not included in the crate
- Add `"conformance/**/*.toml"` to `Cargo.toml`'s `include` array so vectors appear in the `cargo package` tarball
- Repo-root `conformance/required/` remains the canonical source of truth; crate-local is a derived copy
- Test runner resolves vectors via `CARGO_MANIFEST_DIR/conformance/required/`

### CI diff guard
- CI step: `diff -r conformance/required/ crates/cupel/conformance/required/` — fails if vectors diverge
- Pre-commit hook: same diff check, hard fail (blocks commit if vectors are out of sync)
- Scope: required vectors only
- Developer workflow: edit canonical repo-root vectors, manually copy to crate-local, commit both together

### Test file migration
- Preserve existing test structure: `tests/conformance.rs` (shared helpers) + `tests/conformance/{pipeline,placing,scoring,slicing}.rs` submodules
- Straight find-and-replace: `assay_cupel` → `cupel` in all import statements
- Vector path in test runner changes from `CARGO_MANIFEST_DIR/tests/conformance/required/` → `CARGO_MANIFEST_DIR/conformance/required/`
- No non-conformance test files exist — migration scope is the 5 test `.rs` files only

### Claude's Discretion
- Source file copy method (cp, rsync, etc.)
- Ordering of migration steps within plans
- Pre-commit hook implementation details (bash script location, git hooks mechanism)
- Whether to verify `cargo package --list` output programmatically or visually

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

*Phase: 17-crate-migration--conformance-verification*
*Context gathered: 2026-03-14*
