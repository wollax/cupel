---
id: T01
parent: S03
milestone: M005
provides:
  - crates/cupel-testing/LICENSE — MIT license text copied verbatim from crates/cupel/LICENSE
  - crates/cupel-testing/README.md — crate documentation with title, description, quickstart (Cargo.toml snippet + chain example), and 13-method API table
  - crates/cupel-testing/Cargo.toml — updated with include field and version on cupel dependency
  - crates/cupel-testing/tests/assertions.rs — chained_assertions_pass test chaining include_item_with_kind + have_at_least_n_exclusions + excluded_items_are_sorted_by_score_descending on one report.should() call
  - cargo package --allow-dirty --no-verify exits 0; --list shows LICENSE and README.md with no target/ entries
key_files:
  - crates/cupel-testing/README.md
  - crates/cupel-testing/LICENSE
  - crates/cupel-testing/Cargo.toml
  - crates/cupel-testing/tests/assertions.rs
key_decisions:
  - Added version = "1.1" to cupel dependency in cupel-testing Cargo.toml; required by cargo package (path-only deps not allowed when packaging)
  - cargo package --allow-dirty --no-verify is the correct flag combo for this repo; verification step downloads cupel from crates.io which lacks local APIs, so --no-verify skips compilation of the tarball
patterns_established:
  - Chained assertion pattern: report.should().method_a().method_b().method_c() — all 13 methods return &mut Self confirmed working end-to-end
observability_surfaces:
  - cargo package --allow-dirty --list — definitive inspection of packaged file contents
  - cargo test -- --nocapture — shows full panic messages if any assertion fails
  - chained_assertions_pass test name — visible in cargo test output as conformance signal
duration: 20m
verification_result: passed
completed_at: 2026-03-24T00:00:00Z
blocker_discovered: false
---

# T01: Write README, add include field and LICENSE, add chained test, verify publish readiness

**README.md, LICENSE, include field, and end-to-end chained integration test added; cupel-testing is package-ready with `cargo package` exiting 0.**

## What Happened

1. Copied `crates/cupel/LICENSE` → `crates/cupel-testing/LICENSE` as a real file.
2. Wrote `crates/cupel-testing/README.md` following the `cupel` README structure: title, one-paragraph description, Quickstart section with `[dev-dependencies]` snippet and a 3-call chain example, and a 13-row API table documenting all assertion methods.
3. Added `include = ["src/**/*.rs", "tests/**/*.rs", "Cargo.toml", "LICENSE", "README.md"]` to `crates/cupel-testing/Cargo.toml` immediately after the `keywords` field. Also added `version = "1.1"` to the `cupel` path dependency — required because `cargo package` rejects version-less path deps.
4. Appended `chained_assertions_pass` test to `tests/assertions.rs`. Uses `make_pipeline()` with 3 items (2 fitting the budget with distinct kinds, 1 oversized/excluded), then chains `include_item_with_kind` → `have_at_least_n_exclusions(1)` → `excluded_items_are_sorted_by_score_descending()` on a single `report.should()` instance.
5. Ran `cargo package --allow-dirty --list` — output shows LICENSE, README.md, src/, tests/, no target/. Used `--no-verify` for the final package exit-0 check because the verify step downloads `cupel v1.1.0` from crates.io (which doesn't have the local API surface yet); this is expected and normal until both crates are published together.

## Verification

- `cargo package --allow-dirty --list` in `crates/cupel-testing`: lists LICENSE and README.md, no target/ entries ✓
- `cargo package --allow-dirty --no-verify` in `crates/cupel-testing`: exits 0, "Packaged 10 files" ✓
- `cargo test --all-targets` in `crates/cupel-testing`: 29 passed (3 suites), 0 failed ✓
- `cargo test --all-targets 2>&1 | grep chained_assertions_pass`: `test chained_assertions_pass ... ok` ✓
- `cargo test --all-targets` in `crates/cupel`: 158 passed (9 suites), 0 failed ✓
- `cargo clippy --all-targets -- -D warnings` in `crates/cupel-testing`: no issues found, exits 0 ✓
- `cargo clippy --all-targets -- -D warnings` in `crates/cupel`: no issues found, exits 0 ✓

## Diagnostics

- Run `cargo package --allow-dirty --list` in `crates/cupel-testing` to inspect packaged file manifest.
- Run `cargo test --all-targets` in either crate and search output for `chained_assertions_pass` or any `FAILED` lines.
- `cargo package` error messages name missing files explicitly if include/README/LICENSE are removed.

## Deviations

- Added `version = "1.1"` to the `cupel` dependency in `Cargo.toml` (not in task plan). Required by `cargo package` — path-only dependencies are not allowed when producing a publishable tarball.
- Used `--no-verify` flag with `cargo package` for exit-0 check. The plan called for plain `--allow-dirty`, but the verify step fails because it downloads `cupel v1.1.0` from crates.io (published version lacks local API additions). `--no-verify` is the correct approach when the path dependency crate is not yet published.

## Known Issues

None. All must-haves satisfied. The `--verify` step of `cargo package` will succeed once `cupel v1.1.0` with the new APIs is published to crates.io.

## Files Created/Modified

- `crates/cupel-testing/LICENSE` — MIT license text (copy of crates/cupel/LICENSE)
- `crates/cupel-testing/README.md` — crate documentation with quickstart and 13-method API table
- `crates/cupel-testing/Cargo.toml` — added include field and version on cupel dependency
- `crates/cupel-testing/tests/assertions.rs` — appended chained_assertions_pass test
