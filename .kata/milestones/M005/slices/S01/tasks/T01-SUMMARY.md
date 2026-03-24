---
id: T01
parent: S01
milestone: M005
provides:
  - cupel-testing crate with Cargo.toml (edition 2024, MSRV 1.85, MIT, cupel path dep)
  - SelectionReportAssertionChain<'a> struct holding &'a SelectionReport
  - SelectionReportAssertions trait with should() entry point
  - impl SelectionReportAssertions for SelectionReport
  - Public re-exports from lib.rs
key_files:
  - crates/cupel-testing/Cargo.toml
  - crates/cupel-testing/src/lib.rs
  - crates/cupel-testing/src/chain.rs
key_decisions:
  - "Added #[allow(dead_code)] on chain.report field — intentionally unused until S02 adds assertion methods; keeps clippy -D warnings clean"
patterns_established:
  - "Trait extension pattern: SelectionReportAssertions trait + impl for SelectionReport, chain struct with pub(crate) constructor"
observability_surfaces:
  - "cargo check / cargo clippy surface compilation and lint errors with line numbers"
duration: 5min
verification_result: passed
completed_at: 2026-03-24T12:00:00Z
blocker_discovered: false
---

# T01: Create cupel-testing crate with Cargo.toml and module structure

**Fluent assertion crate scaffolded with SelectionReportAssertions trait and SelectionReportAssertionChain plumbing**

## What Happened

Created the `cupel-testing` crate at `crates/cupel-testing/` with three files. `Cargo.toml` follows the same conventions as the `cupel` crate (edition 2024, MSRV 1.85, MIT license) with `cupel = { path = "../cupel" }` as the sole dependency. `chain.rs` defines `SelectionReportAssertionChain<'a>` with a `pub(crate)` constructor — callers use the `should()` trait method, not `new()` directly. `lib.rs` defines the `SelectionReportAssertions` trait with `fn should(&self) -> SelectionReportAssertionChain<'_>`, implements it for `SelectionReport`, and re-exports the chain struct.

## Verification

- `cargo check` in `crates/cupel-testing/` — passed (no errors)
- `cargo clippy --all-targets -- -D warnings` in `crates/cupel-testing/` — clean
- `cargo test --all-targets` in `crates/cupel/` — 158 tests passed, no regressions
- `cargo clippy --all-targets -- -D warnings` in `crates/cupel/` — clean

### Slice-level checks (partial — T01 is intermediate):
- [x] `cargo clippy --all-targets -- -D warnings` clean in cupel-testing
- [x] `cargo clippy --all-targets -- -D warnings` clean in cupel
- [x] `cargo test --all-targets` passes in cupel (158 tests)
- [ ] `cargo test --all-targets` in cupel-testing — no tests yet (T02 adds smoke test)

## Diagnostics

Compiler errors with file/line/type information. No runtime diagnostics — library crate with no runtime behavior yet.

## Deviations

Added `#[allow(dead_code)]` on `chain.report` field. Clippy with `-D warnings` promoted the dead_code warning to an error. The field is intentionally unused until S02 adds assertion methods that read it.

## Known Issues

None.

## Files Created/Modified

- `crates/cupel-testing/Cargo.toml` — crate metadata with cupel path dependency
- `crates/cupel-testing/src/lib.rs` — SelectionReportAssertions trait, impl for SelectionReport, re-exports
- `crates/cupel-testing/src/chain.rs` — SelectionReportAssertionChain struct with pub(crate) constructor
