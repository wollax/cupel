# S01: Crate scaffold + chain plumbing — Research

**Date:** 2026-03-24
**Domain:** Rust crate scaffolding, fluent API design
**Confidence:** HIGH

## Summary

S01 creates the `cupel-testing` crate with the `SelectionReportAssertions` trait and `SelectionReportAssertionChain` struct — no assertion methods yet, just the entry point and chain plumbing. The .NET `Wollax.Cupel.Testing` package (339 lines total) is the reference implementation. The Rust version is simpler: no snapshot support (D107), no `CallerFilePath` equivalent needed.

The core design is straightforward: a trait with a `should()` method returning a chain struct that holds a `&SelectionReport`. The chain methods (added in S02) will return `&mut Self` for fluent chaining and panic on failure (D128). The main technical consideration is that Rust's `ExclusionReason` is a data-carrying enum (unlike .NET's flat enum), which will affect S02's variant-matching patterns — but S01 only needs the chain plumbing, not the assertions.

No external dependencies are needed. The crate depends only on `cupel`. No workspace — the `cupel` crate is standalone at `crates/cupel/`, and `cupel-testing` will be standalone at `crates/cupel-testing/` with a path dependency.

## Recommendation

Create `crates/cupel-testing/` as a standalone crate (no workspace) with:
- `Cargo.toml` referencing `cupel` via path dependency
- `src/lib.rs` defining the `SelectionReportAssertions` trait and re-exporting the chain
- `src/chain.rs` defining `SelectionReportAssertionChain` holding `&SelectionReport`
- One smoke test proving `report.should()` compiles and returns the chain

Follow the .NET structure closely: `SelectionReportExtensions.cs` → trait, `SelectionReportAssertionChain.cs` → chain struct, `SelectionReportAssertionException.cs` → panic (Rust doesn't need a separate exception type).

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Report analytics (budget utilization, kind diversity) | `cupel::analytics::{budget_utilization, kind_diversity}` | Already public; S02 will call these from assertion methods |
| Test data construction | `cupel::ContextItemBuilder` | Builder pattern for test fixtures; avoid raw struct construction |
| Diagnostic types | `cupel::{SelectionReport, IncludedItem, ExcludedItem, ExclusionReason, InclusionReason}` | All publicly exported from `cupel` crate root |

## Existing Code and Patterns

- `src/Wollax.Cupel.Testing/SelectionReportExtensions.cs` — .NET `Should()` entry point. 7 lines. Direct translation to a Rust trait.
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — .NET chain with all 13 patterns (339 lines). S01 creates only the struct shell; S02 fills in methods.
- `src/Wollax.Cupel.Testing/SelectionReportAssertionException.cs` — .NET exception type. Rust equivalent: `panic!()` with formatted message (D128). No custom type needed.
- `crates/cupel/Cargo.toml` — Reference for crate metadata conventions (edition 2024, MSRV 1.85, license, categories).
- `crates/cupel/src/lib.rs` — Shows the public re-export pattern (`pub use module::{Type1, Type2}`).
- `crates/cupel/src/diagnostics/mod.rs` — `SelectionReport`, `IncludedItem`, `ExcludedItem` definitions. All derive `PartialEq` (not `Eq` due to f64 fields per D109). `ExclusionReason` is data-carrying (9+ variants with fields) — S02 will need `std::mem::discriminant()` or match arms for variant-level assertions (patterns 4, 5), not `==` on the whole enum.
- `crates/cupel/src/analytics.rs` — `budget_utilization(&SelectionReport, &ContextBudget) -> f64` and `kind_diversity(&SelectionReport) -> usize`. S02 assertions (patterns 10, 11) should delegate to these.

## Constraints

- **No workspace**: `cupel` crate has no `Cargo.toml` workspace root. `cupel-testing` will be a standalone crate with `cupel = { path = "../cupel" }` in its `Cargo.toml`. For publishing, this becomes `cupel = { version = "1.1" }`.
- **Edition 2024, MSRV 1.85**: Must match `cupel` crate conventions.
- **No external dependencies beyond `cupel`**: Per M005-CONTEXT.md technical constraints. No `serde`, no optional features.
- **Panic on failure, not `Result`**: D128. Chain methods return `&mut Self`, not `Result<&mut Self, E>`. Standard Rust test convention — `#[should_panic]` works for negative tests.
- **Lifetime on chain**: `SelectionReportAssertionChain` holds `&SelectionReport`, so it needs a lifetime parameter: `SelectionReportAssertionChain<'a>`. The `should()` trait method returns `SelectionReportAssertionChain<'_>`.
- **`#[non_exhaustive]` on `ExclusionReason`**: The enum is `#[non_exhaustive]`, which means match arms in `cupel-testing` (external crate) must include a wildcard `_` arm. This is fine — the assertion patterns only need to check specific variants, not exhaustively match.

## Common Pitfalls

- **Forgetting the lifetime parameter on the chain** — `SelectionReportAssertionChain` borrows `&SelectionReport`. Without a lifetime, you'd need to clone the report or use `Arc`. A borrowed reference is correct: chain is always short-lived within a test assertion expression.
- **Using `==` on `ExclusionReason` when you mean variant-matching** — .NET's flat enum allows `==` for "is this BudgetExceeded?". Rust's data-carrying enum requires `matches!(reason, ExclusionReason::BudgetExceeded { .. })` or `std::mem::discriminant()`. This affects S02, not S01, but the chain struct design should anticipate it.
- **Publishing path dependency** — `Cargo.toml` must use `{ path = "../cupel" }` for local development but will need version pinning for crates.io publishing. The `cargo package` step in S03 will handle this.
- **Pattern 6 asymmetry** — .NET method is `HaveExcludedItemWithBudgetExceeded(predicate)` (degenerate — flat enum has no token fields). Rust can be fully faithful: `ExclusionReason::BudgetExceeded { item_tokens, available_tokens }` carries the data. Method signature will differ from .NET (D090). S02 concern, noted here.

## Open Risks

- **None for S01** — the scaffold + plumbing is mechanically straightforward. All decisions are locked (D126 separate crate, D127 fluent chain, D128 panic). The only risk is getting the crate metadata wrong for `cargo package`, but that's S03's concern.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | N/A | No relevant professional skill needed — standard Rust crate scaffolding |

## Sources

- .NET reference: `src/Wollax.Cupel.Testing/` — 3 files, ~360 lines total (chain + extensions + exception)
- Spec vocabulary: `spec/src/testing/vocabulary.md` — 533 lines, 13 patterns fully specified
- Decisions register: D126 (separate crate), D127 (fluent chain API), D128 (panic on failure), D090 (pattern 6 .NET degenerate form), D109 (PartialEq not Eq for f64 types)
