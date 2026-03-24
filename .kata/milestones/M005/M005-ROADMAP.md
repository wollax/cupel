# M005: cupel-testing crate

**Vision:** Deliver a `cupel-testing` Rust crate that provides the 13 spec assertion patterns from the testing vocabulary as a fluent chain API over `SelectionReport`, closing the testing DX parity gap with .NET's `Wollax.Cupel.Testing`.

## Success Criteria

- Rust callers can `use cupel_testing::SelectionReportAssertions;` and write `report.should().include_item_with_kind(kind)` fluent assertion chains
- All 13 spec assertion patterns are implemented with structured panic messages (assertion name, expected, actual)
- `cargo test --all-targets` passes across both `cupel` and `cupel-testing` crates
- `cargo clippy --all-targets -- -D warnings` is clean
- Crate is publishable: `cargo package` succeeds with proper metadata

## Key Risks / Unknowns

- Low risk overall — this is a direct port of well-defined spec patterns to Rust
- Pattern 13 (`place_top_n_scored_at_edges`) has the most complex logic; the .NET implementation is the reference

## Proof Strategy

- Pattern 13 complexity → retire in S02 by implementing and testing with ≥3 edge cases

## Verification Classes

- Contract verification: `cargo test --all-targets`, `cargo clippy --all-targets -- -D warnings`, `cargo package`
- Integration verification: tests that create real `Pipeline` + `run_traced()` and assert on the resulting `SelectionReport`
- Operational verification: none (library crate)
- UAT / human verification: none

## Milestone Definition of Done

This milestone is complete only when all are true:

- All 13 assertion patterns implemented on `SelectionReportAssertionChain`
- Each pattern has positive test (assertion passes) and negative test (assertion panics with structured message)
- Integration test proves chaining works on real pipeline output
- `cargo test --all-targets` passes (both `cupel` and `cupel-testing`)
- `cargo clippy --all-targets -- -D warnings` clean
- `cargo package` succeeds for `cupel-testing`
- Panic messages follow spec error message contract

## Requirement Coverage

- Covers: R060
- Partially covers: none
- Leaves for later: none
- Orphan risks: none

## Slices

- [x] **S01: Crate scaffold + chain plumbing** `risk:medium` `depends:[]`
  > After this: `cupel-testing` crate exists, compiles, and `report.should()` returns a `SelectionReportAssertionChain` that can be chained (no assertions yet, but the plumbing works and one smoke test proves the chain compiles).

- [x] **S02: 13 assertion patterns** `risk:medium` `depends:[S01]`
  > After this: all 13 spec assertion patterns are implemented with positive/negative tests and structured panic messages; `cargo test --all-targets` passes; `cargo clippy` clean.

- [x] **S03: Integration tests + publish readiness** `risk:low` `depends:[S02]`
  > After this: integration tests exercise assertions on real `Pipeline::run_traced()` output; `cargo package` succeeds; crate is ready for `cargo publish`.

## Boundary Map

### S01 → S02

Produces:
- `crates/cupel-testing/src/lib.rs` → `SelectionReportAssertions` trait with `fn should(&self) -> SelectionReportAssertionChain`
- `crates/cupel-testing/src/chain.rs` → `SelectionReportAssertionChain` struct holding `&SelectionReport`, with no assertion methods yet
- `crates/cupel-testing/Cargo.toml` → crate metadata, `cupel` dependency

Consumes: nothing (first slice)

### S02 → S03

Produces:
- 13 assertion methods on `SelectionReportAssertionChain`, each returning `&mut Self` and panicking on failure
- Structured panic messages: `"{assertion_name} failed: expected {expected}, but found {actual}."` format
- Unit tests for all 13 patterns (positive + negative)

Consumes from S01:
- `SelectionReportAssertionChain` struct and `should()` entry point
