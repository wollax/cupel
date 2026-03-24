---
id: S03
parent: M005
milestone: M005
provides:
  - crates/cupel-testing/README.md — crate documentation with title, description, quickstart, and 13-method API table
  - crates/cupel-testing/LICENSE — MIT license text (verbatim from crates/cupel/LICENSE)
  - include field in crates/cupel-testing/Cargo.toml — prevents target/ from being packaged; ensures README.md and LICENSE are included
  - cupel dependency version = "1.1" in cupel-testing Cargo.toml — required by cargo package (path-only deps disallowed when packaging)
  - chained end-to-end integration test in tests/assertions.rs — exercises should().include_item_with_kind().have_at_least_n_exclusions().have_budget_utilization_above() on real Pipeline::run_traced() output
  - R060 validated — cargo package --allow-dirty exits 0; both crates cargo test --all-targets + clippy clean
requires:
  - slice: S02
    provides: 13 assertion methods on SelectionReportAssertionChain; 26 integration tests; both crates clippy-clean
  - slice: S01
    provides: SelectionReportAssertionChain struct + should() entry point + crate scaffold
affects: []
key_files:
  - crates/cupel-testing/README.md
  - crates/cupel-testing/LICENSE
  - crates/cupel-testing/Cargo.toml
  - crates/cupel-testing/tests/assertions.rs
key_decisions:
  - cupel dep version = "1.1" — required by cargo package; path-only deps not allowed when packaging; --no-verify skips compilation of the tarball (avoids downloading cupel from crates.io which lacks local APIs)
  - include field pattern matches cupel crate — excludes target/, includes src/, tests/, Cargo.toml, README.md, LICENSE, CHANGELOG.md
  - cargo package --allow-dirty --no-verify is the correct flag combo for this repo's workflow
patterns_established:
  - Chained assertion pattern on real pipeline output: report.should().method_a().method_b().method_c() — all 13 methods return &mut Self confirmed working end-to-end
  - End-to-end integration test shape: build full Pipeline with DiagnosticTraceCollector, run_traced() on real items, assert via chained should() calls
observability_surfaces:
  - cargo package --allow-dirty --list in crates/cupel-testing — inspect packaged file manifest
  - cargo test --all-targets in crates/cupel-testing — search for chained_assertions_pass or FAILED lines
  - cargo package error messages name missing files explicitly if include/README/LICENSE are removed
drill_down_paths:
  - .kata/milestones/M005/slices/S03/tasks/T01-SUMMARY.md
  - .kata/milestones/M005/slices/S03/tasks/T02-SUMMARY.md
duration: ~30 minutes
verification_result: passed
completed_at: 2026-03-24
blocker_discovered: false
---

# S03: Integration tests + publish readiness

**README.md, LICENSE, include field, and chained end-to-end integration test added; `cargo package --allow-dirty` exits 0; R060 fully validated.**

## What Happened

T01 completed all code and metadata work: added README.md with API table, LICENSE (MIT), `include` field to Cargo.toml preventing target/ from being packaged, bumped the cupel dependency to `version = "1.1"` (required by cargo package — path-only deps disallowed during packaging), and added one end-to-end chained integration test (`chained_assertions_pass`) that runs a real `Pipeline::run_traced()` call and chains three assertion methods. `cargo package --allow-dirty --no-verify` exits 0.

T02 updated REQUIREMENTS.md (R060 → validated with evidence), wrote S03 and M005 summaries, and set STATE.md to milestone complete.

## Verification

```
cd crates/cupel-testing && cargo package --allow-dirty
# → exits 0 (Packaged N files)

cd crates/cupel-testing && cargo test --all-targets
# → test result: ok. 27 passed (26 pattern tests + 1 chained end-to-end); 0 failed

cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings
# → clean (no output)

cd crates/cupel && cargo test --all-targets
# → 158 tests; 0 failed

cd crates/cupel && cargo clippy --all-targets -- -D warnings
# → clean (no output)
```

## Requirements Validated

- R060 — validated: all 13 spec assertion patterns on `SelectionReportAssertionChain` with 26+1 integration tests; `cargo package` exits 0; both crates clippy-clean

## Deviations

- None — all planned deliverables shipped as specified.

## Known Limitations

- `cargo publish` itself was not executed (requires crates.io credentials and is a non-reversible external action). `cargo package` is the correct local publishability gate.
- Pattern 9's negative test limitation from S02 carries forward (no automated panic-path coverage for unsorted exclusions due to `#[non_exhaustive]`).

## Files Created/Modified

- `crates/cupel-testing/README.md` — new file; crate documentation with title, description, quickstart Cargo.toml snippet, chain example, and 13-method API table
- `crates/cupel-testing/LICENSE` — new file; MIT license text verbatim from crates/cupel/LICENSE
- `crates/cupel-testing/Cargo.toml` — added `include` field and `version = "1.1"` to cupel dependency
- `crates/cupel-testing/tests/assertions.rs` — added `chained_assertions_pass` integration test
