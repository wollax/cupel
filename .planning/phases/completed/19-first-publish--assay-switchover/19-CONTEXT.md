# Phase 19: First Publish & Assay Switchover - Context

**Gathered:** 2026-03-15
**Status:** Ready for planning

<domain>
## Phase Boundary

Publish the `cupel` crate (NOT `cupel-rs` — roadmap text is outdated, decision 16-01-D1 confirmed `cupel`) to crates.io at version 1.0.0, configure OIDC trusted publishing for future releases, update the release workflow to use OIDC, and switch `wollax/assay` from path dependency to registry dependency. The `assay-cupel` directory has already been removed from assay — this phase verifies the registry switchover works.

Does NOT include serde feature flag (Phase 20) or docs.rs documentation (Phase 21).

</domain>

<decisions>
## Implementation Decisions

### First publish authentication — token first, then OIDC
- First publish uses a personal crates.io API token stored as `CARGO_REGISTRY_TOKEN` GitHub secret
- Token needs to be created on crates.io and added to GitHub repo secrets before first publish
- After successful publish, configure OIDC trusted publishing on crates.io settings page
- Update `release-rust.yml` to use OIDC instead of `CARGO_REGISTRY_TOKEN`
- Once OIDC is verified working, **remove** the `CARGO_REGISTRY_TOKEN` secret from GitHub — no fallback, single auth path
- Claude's Discretion: OIDC workflow configuration details (permissions, environment variables, id-token scope)

### Release trigger — keep workflow_dispatch
- Manual `workflow_dispatch` trigger with `dry-run` option stays (consistent with .NET release workflow)
- No tag-triggered publishing — manual control over when publishes happen
- Existing main-branch guard remains

### Version and tagging
- First publish is `1.0.0` — the library is feature-complete with 641 tests and conformance suite from v1.0 .NET
- Tag format: `rust-v{version}` (e.g., `rust-v1.0.0`) — clear namespace separation from .NET `v{version}` tags
- GitHub Release: update `make_latest: true` (Rust is the primary distribution going forward)

### Crate name correction
- The published crate name is `cupel` (per Cargo.toml and decision 16-01-D1)
- Roadmap references to "cupel-rs" are outdated and should be corrected during planning
- Claude's Discretion: whether to fix roadmap references in this phase or leave as-is

### Assay switchover — separate PR with full verification
- Publish cupel to crates.io first, verify it's live
- Then open a **separate PR** in `wollax/assay` to switch from path to registry dependency
- Change `cupel = { path = "../cupel/crates/cupel" }` to `cupel = "1.0.0"` in assay's workspace Cargo.toml
- Run full `cargo test` in assay to verify everything passes against the published crate
- The `assay/crates/assay-cupel/` directory is already deleted — no cleanup needed there

### Local development workflow
- Document `[patch.crates-io]` pattern in assay's `CONTRIBUTING.md`
- Developers add `cupel = { path = "../cupel/crates/cupel" }` under `[patch.crates-io]` in assay's Cargo.toml to work against local cupel
- Claude's Discretion: exact CONTRIBUTING.md section structure and whether to include a commented example in Cargo.toml too

</decisions>

<specifics>
## Specific Ideas

- The release workflow already has the publish job structure — main changes are: add OIDC support, update `make_latest`, and verify the token-based publish works first
- crates.io metadata verification (readme rendered, categories visible, docs.rs build triggered) should be a manual verification step after first publish
- The assay PR should be a clean, single-purpose change — just the dependency switch and any import renames if the crate name in `use` statements changed

</specifics>

<deferred>
## Deferred Ideas

- Roadmap text references "cupel-rs" in multiple places — could be corrected but is cosmetic and doesn't affect execution

</deferred>

---

*Phase: 19-first-publish--assay-switchover*
*Context gathered: 2026-03-15*
