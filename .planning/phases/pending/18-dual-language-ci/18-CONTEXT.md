# Phase 18: Dual-Language CI - Context

**Gathered:** 2026-03-14
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire Rust CI into GitHub Actions alongside existing .NET workflows. Separate workflow files with path filters ensure Rust changes trigger Rust CI and .NET changes trigger .NET CI. Release pipeline verified with dry-run before first publish. Does NOT include actual crates.io publishing or OIDC trusted publishing setup (Phase 19).

</domain>

<decisions>
## Implementation Decisions

### Path filter strategy
- Add path filters to the existing .NET CI (`ci.yml`) so Rust-only PRs skip .NET builds
- New `ci-rust.yml` gets positive path filters for `crates/**`, `rust-toolchain.toml`, and self-referencing workflow path
- Each workflow file includes itself in its path filter list (self-referencing) so editing a workflow validates it
- Claude's Discretion: which root config files trigger Rust CI, and how `conformance/**` overlaps between .NET and Rust CI (decide based on where tests actually consume vectors)

### cargo-deny policy
- Deny on known security advisories (RustSec database) — fail CI, not warn
- Ban duplicate dependency versions in the tree
- Claude's Discretion: license allowlist/denylist strategy (appropriate for a permissive open-source library crate) and source origin policy

### Release workflow shape
- `release-rust.yml` follows the same pattern as `release.yml`: `workflow_dispatch` with a `dry-run` boolean input
- Require a GitHub `release` environment with approval gate before the publish step
- Run full test suite (fmt + clippy + test + cargo-deny) before publishing — never publish untested code
- Create a GitHub Release with auto-generated notes on successful publish (consistent with .NET workflow)

### Branch protection & skip behavior
- Claude's Discretion: skip handling mechanism (native path filters vs paths-filter action vs gate job), branch protection configuration for required checks, and how skipped workflows report status

</decisions>

<specifics>
## Specific Ideas

- Existing .NET workflows (`ci.yml`, `release.yml`) are the reference for workflow conventions — match structure and patterns where applicable
- The .NET `release.yml` already uses OIDC (`NuGet/login@v1`), `softprops/action-gh-release@v2`, and environment separation — Rust release should feel like a sibling
- Phase 18 only needs dry-run verification of the release workflow; actual publishing happens in Phase 19

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 18-dual-language-ci*
*Context gathered: 2026-03-14*
