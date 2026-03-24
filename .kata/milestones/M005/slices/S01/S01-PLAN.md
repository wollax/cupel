# S01: Crate scaffold + chain plumbing

**Goal:** `cupel-testing` crate exists, compiles, and `report.should()` returns a `SelectionReportAssertionChain` that can be chained. No assertion methods yet — just the plumbing.
**Demo:** A smoke test proves `report.should()` compiles and returns the chain struct.

## Must-Haves

- `crates/cupel-testing/Cargo.toml` exists with correct metadata (edition 2024, MSRV 1.85, MIT license, `cupel` path dependency)
- `SelectionReportAssertions` trait defined with `fn should(&self) -> SelectionReportAssertionChain`
- `SelectionReportAssertionChain<'a>` struct holds `&'a SelectionReport` with lifetime parameter
- Smoke test compiles and runs: creates a `SelectionReport`, calls `.should()`, gets the chain back
- `cargo test --all-targets` passes (both `cupel` and `cupel-testing`)
- `cargo clippy --all-targets -- -D warnings` clean across both crates

## Proof Level

- This slice proves: contract (crate compiles, trait plumbing works, chain struct is constructable)
- Real runtime required: yes (cargo test runs a real test)
- Human/UAT required: no

## Verification

- `cd crates/cupel-testing && cargo test --all-targets` — smoke test passes
- `cd crates/cupel-testing && cargo clippy --all-targets -- -D warnings` — clean
- `cd crates/cupel && cargo test --all-targets` — existing tests still pass
- `cd crates/cupel && cargo clippy --all-targets -- -D warnings` — existing crate still clean

## Observability / Diagnostics

- Runtime signals: none (library crate, no runtime behavior yet)
- Inspection surfaces: `cargo test` output shows smoke test pass/fail
- Failure visibility: compiler errors and test panics surface the exact failure
- Redaction constraints: none

## Integration Closure

- Upstream surfaces consumed: `cupel::SelectionReport`, `cupel::IncludedItem`, `cupel::ExcludedItem`, `cupel::ExclusionReason`, `cupel::InclusionReason` (all from `crates/cupel/src/diagnostics/mod.rs`)
- New wiring introduced in this slice: `SelectionReportAssertions` trait as the entry point; `SelectionReportAssertionChain` as the chain struct S02 will fill with methods
- What remains before the milestone is truly usable end-to-end: S02 (13 assertion methods), S03 (integration tests + publish readiness)

## Tasks

- [x] **T01: Create cupel-testing crate with Cargo.toml and module structure** `est:20m`
  - Why: The crate must exist before any code can be written. Sets up Cargo.toml metadata, module structure, and lib.rs with re-exports.
  - Files: `crates/cupel-testing/Cargo.toml`, `crates/cupel-testing/src/lib.rs`, `crates/cupel-testing/src/chain.rs`
  - Do: Create `Cargo.toml` matching `cupel` conventions (edition 2024, MSRV 1.85, MIT, `cupel = { path = "../cupel" }` dependency). Create `src/lib.rs` with `SelectionReportAssertions` trait defining `fn should(&self) -> SelectionReportAssertionChain`. Create `src/chain.rs` with `SelectionReportAssertionChain<'a>` struct holding `&'a SelectionReport`. Implement the trait for `SelectionReport`. Re-export chain from lib.rs.
  - Verify: `cargo check` in `crates/cupel-testing/` succeeds
  - Done when: crate compiles with `cargo check`, trait and chain struct are public, `should()` returns the chain

- [x] **T02: Add smoke test and verify full build** `est:15m`
  - Why: Proves the chain plumbing works end-to-end: a test creates a `SelectionReport`, calls `.should()`, and gets a `SelectionReportAssertionChain` back. Also verifies clippy cleanliness across both crates.
  - Files: `crates/cupel-testing/tests/smoke.rs`
  - Do: Create an integration test that constructs a minimal `SelectionReport` (empty events, included, excluded, zeroed totals), imports `SelectionReportAssertions` trait, calls `report.should()`, and asserts the chain is returned (let-binding is sufficient — the type system proves it). Run `cargo test --all-targets` and `cargo clippy --all-targets -- -D warnings` for both crates.
  - Verify: `cargo test --all-targets` passes in both `cupel` and `cupel-testing`; `cargo clippy --all-targets -- -D warnings` clean in both
  - Done when: smoke test passes, clippy clean, no regressions in cupel crate

## Files Likely Touched

- `crates/cupel-testing/Cargo.toml`
- `crates/cupel-testing/src/lib.rs`
- `crates/cupel-testing/src/chain.rs`
- `crates/cupel-testing/tests/smoke.rs`
