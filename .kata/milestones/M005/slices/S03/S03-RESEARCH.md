# S03: Integration tests + publish readiness — Research

**Date:** 2026-03-24
**Domain:** Rust crate packaging + integration testing
**Confidence:** HIGH

## Summary

S03 has two deliverables: (1) `cargo package` succeeds for `cupel-testing`, and (2) end-to-end integration tests prove the assertion chain works on real `Pipeline::run_traced()` output. Both are low-complexity — the scope is narrow and the required patterns already exist.

**Blocking gap #1 — `cargo package` fails today.** The crate declares `readme = "README.md"` in `Cargo.toml` but no `README.md` exists at `crates/cupel-testing/`. `cargo package --allow-dirty` exits with code 101 immediately. Fixing this is the first action of S03: write a `README.md` that documents the fluent chain API, then verify `cargo package` exits 0.

**Blocking gap #2 — no `include` field in `Cargo.toml`.** The `cupel` crate declares an explicit `include` list (src, tests, Cargo.toml, LICENSE, README.md). `cupel-testing` has no `include` field, which means `cargo package` will include everything (`target/`, `.git/`, etc.) once the README gap is fixed. An `include` field should be added to mirror the `cupel` crate pattern, matching what the published artifact should contain.

**Integration tests.** The existing integration test file (`crates/cupel-testing/tests/assertions.rs`) already uses real `Pipeline::run_traced()` output via `DiagnosticTraceCollector` — 26 tests with two helper factories (`make_pipeline()`, `make_priority_pipeline()`). S03 needs one additional end-to-end test that explicitly demonstrates the full fluent chain (`report.should().include_item_with_kind(k).have_at_least_n_exclusions(1)`) on a single real pipeline run. This is distinct from the per-pattern tests: it proves the `&mut Self` chain return works when methods are composed together end-to-end.

No new research or library lookups needed. All patterns, types, and APIs are already in-crate.

## Recommendation

**Two tasks:**
1. **T01:** Write `crates/cupel-testing/README.md`; add `include` field to `Cargo.toml`; add `LICENSE` file (or symlink); verify `cargo package` exits 0; add a chained-assertion integration test; run full `cargo test --all-targets` + clippy.
2. **T02:** Update `REQUIREMENTS.md`, write summaries, update `STATE.md`.

The work is mechanical. No unknowns remain.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Packaging metadata structure | Follow `crates/cupel/Cargo.toml` exactly | Already vetted; same license, repository, edition, rust-version |
| Integration test helpers | Existing `make_pipeline()` / `make_priority_pipeline()` + `run()` in `assertions.rs` | Copy-paste pattern into a new test function; no new helpers needed |
| README structure | Follow `crates/cupel/README.md` structure (Quickstart + API table) | Consistent crate docs; concise |

## Existing Code and Patterns

- `crates/cupel-testing/Cargo.toml` — current metadata; missing `include` field; `readme = "README.md"` already declared; version `0.1.0`, edition `2024`, rust-version `1.85`, license `MIT`, repository already set
- `crates/cupel-testing/src/lib.rs` — `SelectionReportAssertions` trait + `SelectionReportAssertionChain` re-export; entry point is `report.should()`
- `crates/cupel-testing/src/chain.rs` — 13 assertion methods on `SelectionReportAssertionChain`; all return `&mut Self`
- `crates/cupel-testing/tests/assertions.rs` — 26 integration tests (2 per pattern); `make_pipeline()`, `make_priority_pipeline()`, `budget()`, `run()` helper fns; uses `DiagnosticTraceCollector` + `into_report()`
- `crates/cupel/Cargo.toml` — reference for `include` list shape: `["src/**/*.rs", "tests/**/*.rs", "Cargo.toml", "LICENSE", "README.md"]`
- `crates/cupel/README.md` — reference for README content and structure; 173 lines; glossary + quickstart + API table

## Constraints

- No `LICENSE` file exists at `crates/cupel-testing/LICENSE` — need to copy or symlink from `crates/cupel/LICENSE`; `cargo package` will include it only if the `include` field references it and the file is present
- `cupel-testing` has no `examples/` or `conformance/` directories — the `include` list can be simpler than `cupel`'s
- `SelectionReport` is `#[non_exhaustive]` — direct construction remains blocked; S03 integration tests must use the existing `DiagnosticTraceCollector` → `into_report()` path, exactly as S02 does
- `cargo package` bundles relative paths only; the LICENSE at the repo root (`LICENSE`) is not included automatically unless copied into `crates/cupel-testing/`
- Version should remain `0.1.0` for the initial publish (no user-facing API changes in S03)

## Common Pitfalls

- **Missing LICENSE file causes `cargo package` to warn (or fail).** `cargo package` with `include` set will silently skip the LICENSE if it's not present at the declared path. Copy `crates/cupel/LICENSE` to `crates/cupel-testing/LICENSE` as a real file (not a symlink — symlinks are dereferenced but are fragile in some publish paths).
- **`include` field too broad includes `target/`.** Without an `include` field, `cargo package` walks the entire directory and includes the `target/` directory if it exists. Always add an explicit `include` list.
- **Chained test is different from per-pattern tests.** The existing tests call one assertion per test. The end-to-end chain test must call at least two or three assertions on the same chain instance (`report.should().a().b().c()`), proving `&mut Self` composes correctly.
- **`cargo publish --dry-run` vs `cargo package`.** S03's acceptance criterion is `cargo package` (produces a `.crate` tarball). `cargo publish --dry-run` would also upload to the registry in dry-run mode; S03 only requires `cargo package` to exit 0, per milestone spec.

## Open Risks

- None. All APIs are locked, all patterns are implemented. S03 is pure delivery: metadata + README + one integration test.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust crate packaging | — | none found / not needed — patterns are straightforward |

## Sources

- `cargo package` error output (local run 2026-03-24) — confirmed README.md is the blocking issue
- `crates/cupel/Cargo.toml` — reference for publish-ready `include` list and metadata fields
- `crates/cupel-testing/tests/assertions.rs` — confirmed all 26 existing tests already use `Pipeline::run_traced()` via `DiagnosticTraceCollector`
