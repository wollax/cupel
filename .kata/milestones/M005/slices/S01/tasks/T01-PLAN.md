---
estimated_steps: 4
estimated_files: 3
---

# T01: Create cupel-testing crate with Cargo.toml and module structure

**Slice:** S01 — Crate scaffold + chain plumbing
**Milestone:** M005

## Description

Create the `cupel-testing` crate at `crates/cupel-testing/` with proper Cargo.toml metadata, the `SelectionReportAssertions` trait, and the `SelectionReportAssertionChain` struct. This establishes the entry point and chain plumbing that S02 will fill with assertion methods.

## Steps

1. Create `crates/cupel-testing/Cargo.toml` with:
   - `name = "cupel-testing"`, `version = "0.1.0"`, `edition = "2024"`, `rust-version = "1.85"`, `license = "MIT"`
   - Repository, description, readme, categories, keywords appropriate for a testing vocabulary crate
   - `cupel = { path = "../cupel" }` as the sole dependency
   - No optional features, no dev-dependencies beyond what's needed
2. Create `crates/cupel-testing/src/chain.rs` with:
   - `SelectionReportAssertionChain<'a>` struct holding `report: &'a SelectionReport`
   - `pub(crate) fn new(report: &'a SelectionReport) -> Self` constructor
   - The struct is public but the constructor is crate-internal (callers go through `should()`)
3. Create `crates/cupel-testing/src/lib.rs` with:
   - `pub mod chain;` declaration
   - `pub use chain::SelectionReportAssertionChain;` re-export
   - `SelectionReportAssertions` trait with `fn should(&self) -> SelectionReportAssertionChain<'_>;`
   - `impl SelectionReportAssertions for SelectionReport` that calls `SelectionReportAssertionChain::new(self)`
   - Import `cupel::SelectionReport`
4. Run `cargo check` in `crates/cupel-testing/` to confirm compilation

## Must-Haves

- [ ] `crates/cupel-testing/Cargo.toml` has edition 2024, MSRV 1.85, MIT license, `cupel` path dependency
- [ ] `SelectionReportAssertionChain<'a>` struct holds `&'a SelectionReport`
- [ ] `SelectionReportAssertions` trait is public with `should()` method
- [ ] `impl SelectionReportAssertions for SelectionReport` exists
- [ ] `cargo check` passes in `crates/cupel-testing/`

## Verification

- `cd crates/cupel-testing && cargo check` succeeds with no errors
- `SelectionReportAssertionChain` and `SelectionReportAssertions` are visible in `cargo doc --no-deps` output

## Observability Impact

- Signals added/changed: None
- How a future agent inspects this: `cargo check` and compiler errors
- Failure state exposed: Compiler errors with line numbers and types

## Inputs

- `crates/cupel/Cargo.toml` — reference for metadata conventions (edition, MSRV, license)
- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport`, `IncludedItem`, `ExcludedItem` struct definitions
- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — .NET `Should()` pattern reference
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — .NET chain struct reference

## Expected Output

- `crates/cupel-testing/Cargo.toml` — crate metadata with cupel dependency
- `crates/cupel-testing/src/lib.rs` — `SelectionReportAssertions` trait + impl + re-exports
- `crates/cupel-testing/src/chain.rs` — `SelectionReportAssertionChain<'a>` struct with crate-internal constructor
