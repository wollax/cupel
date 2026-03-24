# S03: Integration tests + publish readiness

**Goal:** Fix `cargo package` (README.md + `include` field + LICENSE), add one end-to-end chained-assertion integration test, update R060 to validated, and write summaries.
**Demo:** `cargo package` exits 0 for `cupel-testing`; one integration test in `tests/assertions.rs` chains ≥3 assertion methods on a single real `Pipeline::run_traced()` output; `cargo test --all-targets` + `cargo clippy --all-targets -- -D warnings` pass in both crates.

## Must-Haves

- `cargo package` exits 0 for `cupel-testing` crate (no errors, no missing-file warnings)
- `crates/cupel-testing/README.md` exists and documents the fluent chain API
- `crates/cupel-testing/Cargo.toml` has an explicit `include` field matching the `cupel` crate pattern
- `crates/cupel-testing/LICENSE` exists as a real file (copied from `crates/cupel/LICENSE`)
- End-to-end chained integration test in `crates/cupel-testing/tests/assertions.rs` calls ≥3 assertion methods on the same chain (`report.should().a().b().c()`) using real `Pipeline::run_traced()` output
- `cargo test --all-targets` passes in both crates (0 failed)
- `cargo clippy --all-targets -- -D warnings` clean in both crates
- R060 updated to `validated` in `REQUIREMENTS.md`

## Proof Level

- This slice proves: final-assembly
- Real runtime required: yes (real `cargo package` + `cargo test --all-targets` in the `cupel-testing` crate)
- Human/UAT required: no

## Verification

- `cd crates/cupel-testing && cargo package --allow-dirty` exits 0
- `cd crates/cupel-testing && cargo test --all-targets` — all tests pass (0 failed); chained test visible in output
- `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` exits 0 (clean)
- `cd crates/cupel && cargo test --all-targets` passes (regression check, 0 failed)
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` exits 0

## Observability / Diagnostics

- Runtime signals: `cargo test -- --nocapture` shows panic messages if any assertion fails; `cargo package --allow-dirty --list` lists packaged files for inspection
- Inspection surfaces: `cargo package --allow-dirty --list` verifies `include` field correctness (no `target/`; LICENSE and README.md present)
- Failure visibility: `cargo package` error message explicitly names the missing file; clippy emits the warning code and file location
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `SelectionReportAssertionChain` (all 13 methods from S02); `Pipeline::run_traced()` + `DiagnosticTraceCollector::into_report()` from `cupel` crate; `make_pipeline()` / `make_priority_pipeline()` / `budget()` / `run()` helpers from `tests/assertions.rs`
- New wiring introduced in this slice: `cargo package`-ready crate metadata (README, LICENSE, `include` field); one chained test that composes multiple assertion methods in a single fluent call
- What remains before the milestone is truly usable end-to-end: nothing — milestone is complete after S03

## Tasks

- [x] **T01: Write README, add include field and LICENSE, add chained test, verify publish readiness** `est:30m`
  - Why: Fixes the only blocking gap for `cargo package` (missing README.md, missing include field, missing LICENSE); adds the single end-to-end chained integration test that proves `&mut Self` composes correctly; validates R060 completely
  - Files: `crates/cupel-testing/README.md` (new), `crates/cupel-testing/Cargo.toml`, `crates/cupel-testing/LICENSE` (new copy), `crates/cupel-testing/tests/assertions.rs`
  - Do:
    1. Copy `crates/cupel/LICENSE` to `crates/cupel-testing/LICENSE` as a real file
    2. Write `crates/cupel-testing/README.md` — follow `crates/cupel/README.md` structure: title, one-paragraph description, Quickstart section with `Cargo.toml` snippet (dev-dependency), minimal fluent chain example, API table listing all 13 assertion methods with a one-line description each
    3. Add `include` field to `crates/cupel-testing/Cargo.toml` matching `cupel` pattern: `["src/**/*.rs", "tests/**/*.rs", "Cargo.toml", "LICENSE", "README.md"]`
    4. Append one `chained_assertions_pass` test to `crates/cupel-testing/tests/assertions.rs`: use `make_pipeline()` + a mix of included and excluded items; call `report.should().include_item_with_kind(kind).have_at_least_n_exclusions(1).excluded_items_are_sorted_by_score_descending()` on a single report — three methods chained, proves `&mut Self` return composes end-to-end
    5. Run `cargo package --allow-dirty` in `crates/cupel-testing` — verify exit 0; inspect `--list` output to confirm LICENSE and README.md are included and `target/` is absent
    6. Run `cargo test --all-targets` in both `crates/cupel-testing` and `crates/cupel` — verify 0 failures; confirm `chained_assertions_pass` appears in the output
    7. Run `cargo clippy --all-targets -- -D warnings` in both crates — verify clean
  - Verify: `cargo package --allow-dirty` exits 0; `cargo test --all-targets` shows `chained_assertions_pass ... ok`; clippy clean in both crates
  - Done when: `cargo package` exits 0; the chained test passes; both crates test and clippy clean

- [x] **T02: Update REQUIREMENTS.md, write summaries, update STATE.md** `est:15m`
  - Why: Marks R060 validated, closes the planning record, and leaves the project in a clean state for the next agent
  - Files: `.kata/REQUIREMENTS.md`, `.kata/milestones/M005/slices/S03/S03-SUMMARY.md`, `.kata/milestones/M005/M005-SUMMARY.md`, `.kata/STATE.md`
  - Do:
    1. Update `REQUIREMENTS.md`: change R060 status from `active` to `validated`; update Validation field to reflect M005/S03 completion (cargo package success + chained integration test); update the traceability table row
    2. Write `S03-SUMMARY.md` following the task-summary frontmatter format: id=S03, provides list, key_files, key_decisions, verification_result=pass
    3. Update `M005-SUMMARY.md` with S03's contributions (or write it if it doesn't exist yet)
    4. Update `.kata/STATE.md`: set Active Milestone to M005 complete, no active slice/task, Phase=Complete, Next Action=milestone done
  - Verify: `grep "validated" .kata/REQUIREMENTS.md | grep R060` matches; `S03-SUMMARY.md` exists with frontmatter; `STATE.md` reflects completion
  - Done when: R060 marked validated; S03 summary written; M005 summary updated; STATE.md reflects milestone complete

## Files Likely Touched

- `crates/cupel-testing/README.md` (new)
- `crates/cupel-testing/LICENSE` (new — copy of `crates/cupel/LICENSE`)
- `crates/cupel-testing/Cargo.toml`
- `crates/cupel-testing/tests/assertions.rs`
- `.kata/REQUIREMENTS.md`
- `.kata/milestones/M005/slices/S03/S03-SUMMARY.md`
- `.kata/milestones/M005/M005-SUMMARY.md`
- `.kata/STATE.md`
