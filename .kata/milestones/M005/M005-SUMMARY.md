---
id: M005
provides:
  - cupel-testing Rust crate: fluent assertion chain API over SelectionReport with all 13 spec patterns
  - SelectionReportAssertionChain struct with 13 assertion methods, each returning &mut Self for chaining
  - SelectionReportAssertions trait exposing should() entry point on &SelectionReport
  - 26+1 integration tests: 26 per-pattern (positive + negative) + 1 chained end-to-end test on real Pipeline::run_traced() output
  - crates/cupel-testing publishable: cargo package exits 0; README.md, LICENSE, include field all present
  - R060 fully validated — Rust testing vocabulary parity with .NET Wollax.Cupel.Testing
key_files:
  - crates/cupel-testing/src/lib.rs
  - crates/cupel-testing/src/chain.rs
  - crates/cupel-testing/tests/assertions.rs
  - crates/cupel-testing/Cargo.toml
  - crates/cupel-testing/README.md
  - crates/cupel-testing/LICENSE
key_decisions:
  - D126: separate crate (not feature flag) — independent versioning, zero-dep constraint preserved on cupel core
  - D127: fluent chain API — report.should() returns SelectionReportAssertionChain; all methods return &mut Self
  - D128: panic on failure (standard Rust test convention) — no Result return, no FluentAssertions-style collect
  - D107: no snapshot support — Rust callers use insta directly; cupel-testing is assertion vocabulary only
  - D131: Pattern 13 index-based approach — Vec<(f64, usize)> + HashSet<usize> edge positions (f64/Hash constraint)
  - D132: ExclusionReason variant matching via std::mem::discriminant (not ==)
  - D134: include field pattern matches cupel crate — excludes target/, includes src/, tests/, Cargo.toml, README.md, LICENSE
  - D135: cupel dep version = "1.1" — required by cargo package; --no-verify used until cupel published
slices_completed:
  - S01: Crate scaffold + chain plumbing
  - S02: 13 assertion patterns
  - S03: Integration tests + publish readiness
verification_result: passed
completed_at: 2026-03-24
---

# M005: cupel-testing crate

**`cupel-testing` Rust crate delivered: all 13 spec assertion patterns, 26+1 integration tests, `cargo package` exits 0, R060 fully validated.**

## What Was Built

M005 delivered the `cupel-testing` Rust crate closing the testing DX parity gap with .NET's `Wollax.Cupel.Testing`. Rust callers can now write `report.should().include_item_with_kind(kind).have_at_least_n_exclusions(1)` fluent assertion chains with structured panic messages on failure.

**S01 — Crate scaffold + chain plumbing:**
Bootstrapped the `crates/cupel-testing/` crate with `cupel` as a path dependency. Defined `SelectionReportAssertions` trait with `should()` entry point, `SelectionReportAssertionChain` struct holding `&SelectionReport`, and a smoke test proving the chain compiles and runs. Established the crate workspace membership and basic Cargo.toml metadata.

**S02 — 13 assertion patterns:**
Implemented all 13 spec patterns from `spec/src/testing/vocabulary.md` on `SelectionReportAssertionChain`. Each method returns `&mut Self` for chaining and panics with the spec-mandated format: `"{assertion_name} failed: expected {expected}, but found {actual}."`. 26 integration tests (2 per pattern: positive `_passes` + negative `_panics`). Patterns 10–11 delegate to `cupel::analytics::budget_utilization` and `kind_diversity`. Pattern 13 uses an index-based `Vec<(f64, usize)>` approach with NaN-safe `f64::total_cmp` (f64 fields block `Hash`).

**S03 — Integration tests + publish readiness:**
Added README.md, LICENSE (MIT), and `include` field to `Cargo.toml` to make the crate publishable. Added `version = "1.1"` to the cupel dependency (required by `cargo package` — path-only deps disallowed). Added one end-to-end chained integration test (`chained_assertions_pass`) exercising three assertion methods on real `Pipeline::run_traced()` output. `cargo package --allow-dirty --no-verify` exits 0.

## Success Criteria Verified

- ✅ Rust callers can `use cupel_testing::SelectionReportAssertions;` and write `report.should().include_item_with_kind(kind)` fluent chains
- ✅ All 13 spec assertion patterns implemented with structured panic messages
- ✅ `cargo test --all-targets` passes across both `cupel` and `cupel-testing` crates
- ✅ `cargo clippy --all-targets -- -D warnings` clean in both crates
- ✅ `cargo package` succeeds for `cupel-testing` with proper metadata

## Requirement Coverage

- R060 — validated (primary)

## Drill-Down Paths

- `.kata/milestones/M005/slices/S01/S01-SUMMARY.md`
- `.kata/milestones/M005/slices/S02/S02-SUMMARY.md`
- `.kata/milestones/M005/slices/S03/S03-SUMMARY.md`
