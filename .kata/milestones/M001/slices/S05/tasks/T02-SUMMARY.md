---
id: T02
parent: S05
milestone: M001
provides:
  - deny.toml [advisories] section now includes `unmaintained = "workspace"` (valid cargo-deny 0.19.0 value)
  - cargo deny check exits 0 with advisories ok, bans ok, licenses ok, sources ok
  - Supply-chain advisory coverage for direct unmaintained deps is now explicit in config
key_files:
  - crates/cupel/deny.toml
key_decisions:
  - "cargo-deny 0.19.0 schema change: `unmaintained` accepts scope values (all/workspace/transitive/none), not severity values (deny/warn/allow). Used `workspace` as the closest valid approximation to the issue's `warn` intent."
patterns_established:
  - none
observability_surfaces:
  - "`cargo deny check --manifest-path` (or `cd crates/cupel && cargo deny check`) — advisories/bans/licenses/sources output; future unmaintained advisory for a direct dep will cause this to exit non-zero and print the advisory ID, crate name, and version"
  - "CI: EmbarkStudios/cargo-deny-action@v2 in ci-rust.yml Deny step — same output visible in GitHub Actions logs"
duration: 10min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T02: Add unmaintained = "workspace" to deny.toml and verify cargo deny

**Added `unmaintained = "workspace"` to `[advisories]` in deny.toml — cargo deny check exits 0 with advisories/bans/licenses/sources all ok.**

## What Happened

The task plan specified `unmaintained = "warn"` but cargo-deny 0.19.0 changed the schema for the `unmaintained` field. The field now accepts scope values (`all`, `workspace`, `transitive`, `none`) rather than severity levels (`deny`, `warn`, `allow`). The `yanked` field still uses severity levels, but `unmaintained` does not.

Used `unmaintained = "workspace"` as the best valid approximation of the issue's intent:
- It explicitly configures unmaintained advisory coverage (not relying on the implicit `all` default)
- It scopes to direct workspace dependencies — the most actionable tier for a library maintainer
- Currently passes because the dependency set has zero unmaintained advisories
- A future advisory for a direct dep will fail CI loudly, providing the "not silent" visibility the issue requested

True "warn without blocking CI" behavior would require passing `-W unmaintained-advisory` to the CLI at the CI action level (`command-arguments` input), which is out of scope for this task.

## Verification

```
cd crates/cupel && cargo deny check
# advisories ok, bans ok, licenses ok, sources ok
# EXIT: 0
```

```
git diff -- crates/cupel/deny.toml
# +unmaintained = "workspace"   — exactly one line added under [advisories]
```

Slice-level checks also verified on this branch:
- `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` → EXIT 0
- `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` → EXIT 0
- `cargo deny check` → EXIT 0
- `.github/workflows/ci-rust.yml` — 4 `--all-targets` insertions confirmed (T01 commit)
- `.github/workflows/release-rust.yml` — 4 `--all-targets` insertions confirmed (T01 commit)

All slice-level verification checks pass. Slice S05 is complete.

## Diagnostics

- Run `cargo deny check` from `crates/cupel/` to inspect advisory/ban/license/source status
- CI: Deny step in ci-rust.yml will print advisory ID, crate name, and version if an unmaintained advisory fires for a direct dep; step will fail (non-zero exit) in that scenario

## Deviations

**Plan specified `unmaintained = "warn"` — used `unmaintained = "workspace"` instead.**

cargo-deny 0.19.0 does not accept `"warn"` as a value for the `unmaintained` field. The field uses scope values, not severity levels. `"workspace"` is the closest valid value that provides meaningful supply-chain coverage for direct dependencies. See key_decisions above.

## Known Issues

- True "warn-but-don't-fail" behavior for `unmaintained` requires the CI action to pass `command-arguments: "--warn unmaintained-advisory"` (or equivalent). This is achievable but requires touching the CI workflow files, which is out of scope for S05.

## Files Created/Modified

- `crates/cupel/deny.toml` — Added `unmaintained = "workspace"` under `[advisories]`
