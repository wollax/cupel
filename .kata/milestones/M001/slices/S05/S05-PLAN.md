# S05: CI Quality Hardening

**Goal:** Add `--all-targets` to both clippy steps in `ci-rust.yml` and `release-rust.yml`, and add `unmaintained = "warn"` to `deny.toml` — so integration tests, examples, and unmaintained crate advisories are covered by CI from this slice forward.
**Demo:** `cargo clippy --all-targets -- -D warnings` (default and serde) exits 0 locally; `cargo deny check` exits 0 locally; CI YAML diffs confirm the three targeted changes and nothing else.

## Must-Haves

- `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0
- `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0
- `cargo deny check --manifest-path crates/cupel/Cargo.toml` exits 0 with `unmaintained = "warn"` present
- `ci-rust.yml` Clippy (default features) step includes `--all-targets`
- `ci-rust.yml` Clippy (serde) step includes `--all-targets`
- `release-rust.yml` Clippy (default features) step includes `--all-targets`
- `release-rust.yml` Clippy (serde) step includes `--all-targets`
- `deny.toml` `[advisories]` section contains `unmaintained = "warn"`
- No other changes to any of the three files

## Proof Level

- This slice proves: contract
- Real runtime required: no (local CLI verification is sufficient; CI will confirm on merge)
- Human/UAT required: no

## Verification

- `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` — exits 0
- `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` — exits 0
- `cargo deny check --manifest-path crates/cupel/Cargo.toml` — exits 0
- `git diff HEAD -- .github/workflows/ci-rust.yml` — exactly two lines changed (one per clippy step)
- `git diff HEAD -- .github/workflows/release-rust.yml` — exactly two lines changed (one per clippy step)
- `git diff HEAD -- crates/cupel/deny.toml` — exactly one line added under `[advisories]`

## Observability / Diagnostics

- Runtime signals: none — this is CI configuration, not runtime code
- Inspection surfaces: `cargo clippy --all-targets` output; `cargo deny check` output
- Failure visibility: if `--all-targets` surfaces a new warning, `cargo clippy` will print the lint name, file, and line; `cargo deny` prints the advisory ID and crate name
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: none (S05 has no dependencies)
- New wiring introduced in this slice: CI now lints `crates/cupel/tests/`, `crates/cupel/examples/` (and any future `benches/`); cargo-deny now warns on unmaintained crates in the transitive closure
- What remains before the milestone is truly usable end-to-end: S07 (Rust Quality Hardening) depends on this clean baseline; S06 (.NET Quality Hardening) is independent

## Tasks

- [x] **T01: Add --all-targets to clippy steps in both CI workflow files** `est:20m`
  - Why: R003 requires `cargo clippy --all-targets` in CI; `ci-rust.yml` and `release-rust.yml` both have two clippy steps (default and serde) that currently omit `--all-targets`, leaving integration tests and examples unlinted
  - Files: `.github/workflows/ci-rust.yml`, `.github/workflows/release-rust.yml`
  - Do: In `ci-rust.yml`, update the "Clippy (default features)" step from `cargo clippy --manifest-path ...` to `cargo clippy --all-targets --manifest-path ...`; update "Clippy (serde)" step identically (add `--all-targets` before `--manifest-path`). Apply the exact same two changes to `release-rust.yml`. Touch nothing else in either file.
  - Verify: `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0; `git diff` shows exactly 4 changed lines across the two YAML files
  - Done when: Both local clippy commands exit 0; both YAML files show exactly `--all-targets` added to two clippy steps each and no other changes

- [x] **T02: Add unmaintained = "warn" to deny.toml and verify cargo deny** `est:15m`
  - Why: R003 requires `deny.toml` to flag unmaintained crates as warnings; the `[advisories]` section currently only has `version = 2` and `yanked = "deny"` — the `unmaintained` key is absent
  - Files: `crates/cupel/deny.toml`
  - Do: Under `[advisories]` in `deny.toml`, add `unmaintained = "warn"` on its own line after `yanked = "deny"`. Touch nothing else in the file. Run `cargo deny check --manifest-path crates/cupel/Cargo.toml` locally to confirm the new key is accepted and no existing advisory violations are introduced.
  - Verify: `cargo deny check --manifest-path crates/cupel/Cargo.toml` exits 0; `git diff -- crates/cupel/deny.toml` shows exactly one line added under `[advisories]`
  - Done when: `cargo deny check` exits 0 with `unmaintained = "warn"` present; no other lines changed in `deny.toml`

## Files Likely Touched

- `.github/workflows/ci-rust.yml`
- `.github/workflows/release-rust.yml`
- `crates/cupel/deny.toml`
