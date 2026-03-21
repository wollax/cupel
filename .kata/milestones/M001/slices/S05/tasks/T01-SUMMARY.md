---
id: T01
parent: S05
milestone: M001
provides:
  - Both CI workflow files now pass --all-targets to all cargo clippy invocations
  - Integration tests (tests/) and examples (examples/) are linted in CI
  - cargo clippy --all-targets exits 0 for both default and serde feature sets
key_files:
  - .github/workflows/ci-rust.yml
  - .github/workflows/release-rust.yml
key_decisions:
  - "--all-targets placed before --manifest-path (not before -- -D warnings) to match cargo flag ordering convention"
patterns_established:
  - none
observability_surfaces:
  - "cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings"
duration: 5min
verification_result: passed
completed_at: 2026-03-21T00:00:00Z
blocker_discovered: false
---

# T01: Add --all-targets to clippy steps in both CI workflow files

**Inserted `--all-targets` into all four `cargo clippy` invocations across `ci-rust.yml` and `release-rust.yml`, expanding CI lint scope to include integration tests and examples.**

## What Happened

Both workflow files had two `cargo clippy` steps each (default features and serde feature) that omitted `--all-targets`, leaving `crates/cupel/tests/` and `crates/cupel/examples/` unlinted by CI. The flag was inserted into all four steps. No other lines were modified.

## Verification

- `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` — exited 0, no warnings
- `cargo clippy --all-targets --features serde --manifest-path crates/cupel/Cargo.toml -- -D warnings` — exited 0, no warnings
- `git diff -- .github/workflows/ci-rust.yml` — exactly 2 lines changed
- `git diff -- .github/workflows/release-rust.yml` — exactly 2 lines changed

## Diagnostics

Run `cargo clippy --all-targets --manifest-path crates/cupel/Cargo.toml -- -D warnings` locally to inspect the full lint scope. CI logs for both "Clippy (default features)" and "Clippy (serde)" steps will now include diagnostics from `tests/` and `examples/`.

## Deviations

None.

## Known Issues

None.

## Files Created/Modified

- `.github/workflows/ci-rust.yml` — two clippy steps updated with `--all-targets`
- `.github/workflows/release-rust.yml` — two clippy steps updated with `--all-targets`
