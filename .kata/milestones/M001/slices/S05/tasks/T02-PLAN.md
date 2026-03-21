---
estimated_steps: 3
estimated_files: 1
---

# T02: Add unmaintained = "warn" to deny.toml and verify cargo deny

**Slice:** S05 — CI Quality Hardening
**Milestone:** M001

## Description

`crates/cupel/deny.toml` uses cargo-deny v2 schema. The `[advisories]` section currently contains only `version = 2` and `yanked = "deny"`. R003 requires `unmaintained = "warn"` to be added so that supply-chain advisories for unmaintained crates produce a CI warning rather than silently passing. The value must be `"warn"` (not `"deny"`) — see D008 rationale in DECISIONS.md and the open issue `2026-03-14-cargo-deny-unmaintained-warn.md`.

## Steps

1. Open `crates/cupel/deny.toml`. Under `[advisories]`, add `unmaintained = "warn"` on a new line after `yanked = "deny"`. The section should read:
   ```toml
   [advisories]
   version = 2
   yanked = "deny"
   unmaintained = "warn"
   ```
   Touch nothing else in the file.
2. Run `cargo deny check --manifest-path crates/cupel/Cargo.toml` locally. Confirm it exits 0. Review the output: advisories, bans, licenses, and sources should all pass (matching the pre-existing clean baseline per S05-RESEARCH.md).
3. Confirm with `git diff -- crates/cupel/deny.toml` that exactly one line was added and nothing else changed.

## Must-Haves

- [ ] `crates/cupel/deny.toml` `[advisories]` section contains `unmaintained = "warn"`
- [ ] `cargo deny check --manifest-path crates/cupel/Cargo.toml` exits 0
- [ ] No other lines in `deny.toml` are changed

## Verification

- `cargo deny check --manifest-path crates/cupel/Cargo.toml` — must exit 0 (advisories ok, bans ok, licenses ok, sources ok)
- `git diff -- crates/cupel/deny.toml` — must show exactly one line added (`unmaintained = "warn"`) under `[advisories]`; no other changes

## Observability Impact

- Signals added/changed: CI Deny step will now emit a warning (not error) if a transitive dependency receives a RustSec "unmaintained" advisory; the warning appears in the EmbarkStudios/cargo-deny-action step output
- How a future agent inspects this: `cargo deny check --manifest-path crates/cupel/Cargo.toml` locally; or examine CI Deny step logs
- Failure state exposed: if an unmaintained advisory fires in a future CI run, the advisory ID, crate name, and version will be printed; the step will not fail (warn only), so CI will remain green while the issue is visible

## Inputs

- `crates/cupel/deny.toml` — current `[advisories]` section has `version = 2` and `yanked = "deny"` only
- S05-RESEARCH.md confirms `cargo deny check` baseline is clean on current HEAD (advisories ok, bans ok, licenses ok, sources ok)

## Expected Output

- `crates/cupel/deny.toml` — `[advisories]` section gains one line: `unmaintained = "warn"`; all other sections and values unchanged
