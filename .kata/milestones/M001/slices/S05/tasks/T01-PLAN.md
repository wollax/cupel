---
estimated_steps: 4
estimated_files: 2
---

# T01: Add --all-targets to clippy steps in both CI workflow files

**Slice:** S05 — CI Quality Hardening
**Milestone:** M001

## Description

Both `.github/workflows/ci-rust.yml` and `.github/workflows/release-rust.yml` contain two `cargo clippy` invocations each — one for default features and one for the `serde` feature. Neither currently passes `--all-targets`, which means integration tests (`crates/cupel/tests/`) and examples (`crates/cupel/examples/`) are not linted by CI. This task inserts `--all-targets` into all four clippy steps and verifies that the expanded lint scope produces zero warnings locally.

## Steps

1. Open `.github/workflows/ci-rust.yml`. Locate the "Clippy (default features)" step: `cargo clippy --manifest-path crates/cupel/Cargo.toml -- -D warnings`. Insert `--all-targets` between `clippy` and `--manifest-path`, producing: `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings`.
2. Locate the "Clippy (serde)" step in the same file: `cargo clippy --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings`. Insert `--all-targets` between `serde` and `--manifest-path`, producing: `cargo clippy --features serde --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings`. Save the file.
3. Apply the identical two edits to `.github/workflows/release-rust.yml` (same step names, same clippy commands in the `test` job). Save the file.
4. Verify locally: run `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` and `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings`. Both must exit 0. Confirm with `git diff` that only the four targeted lines changed.

## Must-Haves

- [ ] `ci-rust.yml` "Clippy (default features)" step contains `--all-targets`
- [ ] `ci-rust.yml` "Clippy (serde)" step contains `--all-targets`
- [ ] `release-rust.yml` "Clippy (default features)" step contains `--all-targets`
- [ ] `release-rust.yml` "Clippy (serde)" step contains `--all-targets`
- [ ] `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0
- [ ] `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` exits 0
- [ ] No other lines in either YAML file are changed

## Verification

- `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` — must exit 0 with no warnings
- `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` — must exit 0 with no warnings
- `git diff -- .github/workflows/ci-rust.yml` — must show exactly 2 changed lines (the two clippy commands), no additions or deletions elsewhere
- `git diff -- .github/workflows/release-rust.yml` — must show exactly 2 changed lines, no additions or deletions elsewhere

## Observability Impact

- Signals added/changed: CI will now emit clippy diagnostics for `tests/`, `examples/`, and any future `benches/` targets; these appear in CI logs under the existing "Clippy" steps
- How a future agent inspects this: `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` locally; or examine CI job logs for the two Clippy steps
- Failure state exposed: if `--all-targets` surfaces a new warning in S07 (which adds new Rust source), the lint name, file path, and line number will be printed to stderr; `-D warnings` promotes it to an error that fails the step

## Inputs

- `.github/workflows/ci-rust.yml` — current state: two clippy steps without `--all-targets`
- `.github/workflows/release-rust.yml` — current state: two clippy steps without `--all-targets` in the `test` job
- Local baseline: `cargo clippy --all-targets` confirmed clean (zero warnings) on current HEAD per S05-RESEARCH.md

## Expected Output

- `.github/workflows/ci-rust.yml` — two clippy steps updated with `--all-targets`; all other lines unchanged
- `.github/workflows/release-rust.yml` — two clippy steps updated with `--all-targets`; all other lines unchanged
