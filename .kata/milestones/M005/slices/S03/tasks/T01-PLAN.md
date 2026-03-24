---
estimated_steps: 7
estimated_files: 4
---

# T01: Write README, add include field and LICENSE, add chained test, verify publish readiness

**Slice:** S03 — Integration tests + publish readiness
**Milestone:** M005

## Description

Fix the three blocking gaps that prevent `cargo package` from succeeding: write `README.md`, add the `include` field to `Cargo.toml`, and copy `LICENSE`. Then add one end-to-end chained integration test that proves `&mut Self` chain composition works across three assertion methods on a single real `Pipeline::run_traced()` output. Verify full publish readiness.

## Steps

1. Copy `crates/cupel/LICENSE` to `crates/cupel-testing/LICENSE` as a real file (not a symlink)
2. Write `crates/cupel-testing/README.md` — follow `crates/cupel/README.md` structure:
   - Title `# cupel-testing` + one-paragraph description of the fluent assertion crate
   - Quickstart section with `Cargo.toml` `[dev-dependencies]` snippet
   - Minimal fluent chain example (2-3 assertion calls on a `SelectionReport`)
   - API table listing all 13 assertion methods with a one-line description each
3. Add `include` field to `crates/cupel-testing/Cargo.toml` immediately after the existing `keywords` field: `include = ["src/**/*.rs", "tests/**/*.rs", "Cargo.toml", "LICENSE", "README.md"]`
4. Append test `chained_assertions_pass` to `crates/cupel-testing/tests/assertions.rs`:
   - Use `make_pipeline()` + 3 items: 2 that fit the budget (one per unique kind), 1 that is excluded (oversized or duplicate)
   - Call `report.should().include_item_with_kind(kind).have_at_least_n_exclusions(1).excluded_items_are_sorted_by_score_descending()` — three methods on the same chain instance
   - This proves `&mut Self` return composes end-to-end on real pipeline output
5. Run `cd crates/cupel-testing && cargo package --allow-dirty --list` — inspect output: `LICENSE` and `README.md` must appear; `target/` must not appear
6. Run `cd crates/cupel-testing && cargo test --all-targets` and `cd crates/cupel && cargo test --all-targets` — verify 0 failures; confirm `chained_assertions_pass` appears in the output
7. Run `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` and `cd crates/cupel && cargo clippy --all-targets -- -D warnings` — verify both exit 0

## Must-Haves

- [ ] `crates/cupel-testing/LICENSE` exists as a real file (not symlink)
- [ ] `crates/cupel-testing/README.md` exists with title, description, quickstart, and 13-method API table
- [ ] `crates/cupel-testing/Cargo.toml` has `include = ["src/**/*.rs", "tests/**/*.rs", "Cargo.toml", "LICENSE", "README.md"]`
- [ ] `cargo package --allow-dirty` exits 0 in `crates/cupel-testing`
- [ ] `cargo package --allow-dirty --list` output contains `LICENSE` and `README.md`; does not contain `target/`
- [ ] `tests/assertions.rs` contains `chained_assertions_pass` test calling ≥3 assertion methods on the same `should()` chain
- [ ] `cargo test --all-targets` passes in `crates/cupel-testing` (0 failed; `chained_assertions_pass` is in the output)
- [ ] `cargo test --all-targets` passes in `crates/cupel` (0 failed — regression check)
- [ ] `cargo clippy --all-targets -- -D warnings` exits 0 in both crates

## Verification

- `cd crates/cupel-testing && cargo package --allow-dirty` → exits 0, no errors
- `cd crates/cupel-testing && cargo package --allow-dirty --list` → LICENSE and README.md appear; no target/ entries
- `cd crates/cupel-testing && cargo test --all-targets 2>&1 | grep "chained_assertions_pass"` → `test chained_assertions_pass ... ok`
- `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` → no output (clean)
- `cd crates/cupel && cargo test --all-targets` → all previously passing tests still pass (0 failed)
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` → no output (clean)

## Observability Impact

- Signals added/changed: `cargo package --allow-dirty --list` is the definitive inspection surface for packaged file contents; `cargo test -- --nocapture` shows full panic messages if the chained test fails
- How a future agent inspects this: run `cargo package --allow-dirty --list` in `crates/cupel-testing` to see exactly what would be published; run `cargo test --all-targets` to see test names and pass/fail counts
- Failure state exposed: `cargo package` error message names the missing file; clippy names the file and line; test output names the failing test

## Inputs

- `crates/cupel/LICENSE` — source file to copy verbatim
- `crates/cupel/README.md` — reference structure (title, glossary, quickstart, API table)
- `crates/cupel/Cargo.toml` — reference for `include` field shape
- `crates/cupel-testing/src/chain.rs` — all 13 method names and signatures for the API table
- `crates/cupel-testing/tests/assertions.rs` — existing `make_pipeline()`, `budget()`, `run()` helpers to reuse for the chained test
- S02 summary: pattern names, helper function signatures, confirmed `&mut Self` return on all 13 methods

## Expected Output

- `crates/cupel-testing/LICENSE` — MIT license text (copy of `crates/cupel/LICENSE`)
- `crates/cupel-testing/README.md` — crate documentation with quickstart and API table
- `crates/cupel-testing/Cargo.toml` — updated with `include` field
- `crates/cupel-testing/tests/assertions.rs` — updated with `chained_assertions_pass` test at the end
- `cargo package --allow-dirty` exits 0 — `.crate` tarball successfully produced
