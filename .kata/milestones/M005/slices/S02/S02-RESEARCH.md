# S02: 13 assertion patterns — Research

**Date:** 2026-03-24

## Summary

S02 implements all 13 spec assertion patterns on `SelectionReportAssertionChain` in `crates/cupel-testing/`. The spec vocabulary is fully specified in `spec/src/testing/vocabulary.md` with exact semantics, error message formats, edge cases, and tie-breaking rules for every pattern. The .NET reference implementation at `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` is complete and battle-tested — it is the direct translation target. The Rust crate scaffold from S01 is ready: `SelectionReportAssertionChain<'a>` holds `&'a SelectionReport`, and the `#[allow(dead_code)]` on the `report` field must be removed when the first assertion method reads it.

The primary implementation risk is Pattern 13 (`place_top_n_scored_at_edges`): it requires score-sorting, edge-position enumeration, and tie-aware set membership. The .NET implementation uses a `HashSet<IncludedItem>` and `minTopScore` comparison — the Rust port needs `PartialEq` on `IncludedItem` (already derived) and explicit f64 comparison via `>=`. All other patterns are straightforward field access + `panic!` on failure.

Test construction for Rust `#[non_exhaustive]` `SelectionReport` requires running real mini-pipelines through `DiagnosticTraceCollector::new(TraceDetailLevel::Item)`. This is the established S01 pattern. Tests should be integration tests in `crates/cupel-testing/tests/` (not unit tests), using `cupel`'s `Pipeline`, `ContextItemBuilder`, `ContextBudget`, and `DiagnosticTraceCollector`.

## Recommendation

Port directly from the .NET reference. Group patterns by complexity:
- **T01 (patterns 1–7):** Existence/count/reason checks — trivial iteration with `panic!`.
- **T02 (patterns 8–13):** Aggregate/budget/coverage/ordering — slightly more complex but all pure computation.

Each pattern needs: one positive test (assertion passes on valid report) + one negative test (assertion panics with the spec error message). Verify the panic message text exactly with `#[should_panic(expected = "...")]`.

## Don't Hand-Roll

| Problem | Existing Solution | Why Use It |
|---------|------------------|------------|
| Budget utilization arithmetic | `cupel::analytics::budget_utilization()` in `analytics.rs` | Free function already computes `sum(included tokens) / budget.max_tokens()` — reuse rather than duplicate |
| Kind diversity count | `cupel::analytics::kind_diversity()` in `analytics.rs` | Identical logic to `HaveKindCoverageCount` denominator |
| ExclusionReason discriminant matching | `matches!(e.reason, ExclusionReason::BudgetExceeded { .. })` macro | No string parsing; variant-aware; handles data-carrying variants cleanly |
| Test report construction | `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()` + mini pipeline | The only valid path since `SelectionReport` is `#[non_exhaustive]` in a separate crate |

## Existing Code and Patterns

- `crates/cupel-testing/src/chain.rs` — `SelectionReportAssertionChain<'a>` struct; `pub(crate)` constructor; `#[allow(dead_code)]` on `report` field **must be removed** when first assertion method reads `self.report`
- `crates/cupel-testing/src/lib.rs` — `SelectionReportAssertions` trait + impl; re-export pattern to follow
- `crates/cupel/src/diagnostics/mod.rs` — Complete type definitions: `SelectionReport`, `IncludedItem`, `ExcludedItem`, `ExclusionReason` (all variants), `InclusionReason`; all derive `Debug + Clone + PartialEq`
- `crates/cupel/src/analytics.rs` — `budget_utilization(report, budget)` and `kind_diversity(report)` as free functions; import with `use cupel::analytics;` or re-export
- `crates/cupel/src/model/context_item.rs` — `ContextItem` accessors: `.kind()` returns `&ContextKind`, `.tokens()` returns `i64`, `.content()` returns `&str`
- `crates/cupel/src/model/context_budget.rs` — `ContextBudget::max_tokens()` returns `i64` — the PD-2 denominator
- `crates/cupel/src/model/context_kind.rs` — `ContextKind` is a newtype wrapping `String`; implements `PartialEq` — equality via `==` works for `i.item.kind() == &kind`
- `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs` — Complete .NET reference for all 13 patterns with exact error message strings; direct translation target
- `crates/cupel-testing/tests/smoke.rs` — Test construction pattern to reuse: `DiagnosticTraceCollector::new(TraceDetailLevel::Item).into_report()`

## Constraints

- **`SelectionReport` is `#[non_exhaustive]`** — struct literal construction is impossible from `cupel-testing` crate; all test `SelectionReport` instances must be produced via `DiagnosticTraceCollector` after a real pipeline run
- **Chain methods return `&mut Self`** (D128) — each method signature: `pub fn method_name(&mut self, ...) -> &mut Self`
- **Panic on failure** (D128) — use `panic!("{message}")` with the spec error message; do NOT return `Result`
- **`ContextKind` comparison** — `i.item.kind() == &kind` (accessor returns `&ContextKind`; compare by reference equality against a borrowed arg)
- **f64 comparison** — Pattern 9 (`excluded_items_are_sorted_by_score_descending`) uses `<` for adjacent pair check; no NaN expected from a real pipeline but silent on NaN (NaN comparisons are false so a NaN score would be treated as ≤, not a violation)
- **No external dependencies** — only `cupel` as path dep; no `itertools`, no `float_ord`, no anything else
- **Edition 2024, MSRV 1.85** — matches the `cupel` crate; no unstable features
- **`analytics` functions are in `crates/cupel`** — import via `cupel::analytics::budget_utilization` and `cupel::analytics::kind_diversity`; check that `pub` visibility allows it from `cupel-testing` (these are `pub fn` in `analytics.rs` with `pub mod analytics` in `lib.rs`)

## Common Pitfalls

- **`&kind` vs `kind` in kind comparisons** — `item.kind()` returns `&ContextKind`; comparisons must be `item.kind() == &kind` or `*item.kind() == kind` depending on how the arg is taken. If the method takes `kind: ContextKind` (by value), then `item.kind() == &kind` works.
- **Pattern 6 token fields** — `ExclusionReason::BudgetExceeded { item_tokens, available_tokens }` — destructure directly in a `match` or `if let`; the error message needs both expected and actual values
- **Pattern 13 tie handling** — `minTopScore` is the minimum score among the top-N set; items with `score >= minTopScore` are "in the tie zone". The Rust port must use the same `minTopScore` comparison against `IncludedItem` PartialEq (which compares all fields). A separate HashSet of the top-N items by index is safer than by value equality for disambiguation.
- **Pattern 13 edge position enumeration** — `lo` starts at 0, `hi` starts at `count-1`; add `lo` then `hi` each iteration, skip if `lo == hi` for the second add; stop when `edge_positions.len() == n`
- **`#[allow(dead_code)]` removal** — must be removed from `chain.rs` when the first assertion method accesses `self.report`; missing this causes a dead_code warning that blocks clippy `-D warnings`
- **`should_panic` message matching** — `#[should_panic(expected = "...")]` does substring matching, not exact; use a unique prefix from the error message to avoid false positives
- **Test isolation** — each integration test must build its own pipeline; shared static state is not a concern for pure in-memory pipelines

## Open Risks

- **Pattern 13 edge cases with `n > included.count`** — spec says "assertion fails"; the implementation must detect this before the edge-position loop to avoid out-of-bounds. The .NET check is `if (n > _report.Included.Count)` before any sorting.
- **`analytics::budget_utilization` visibility** — `analytics.rs` is `pub mod analytics` in `cupel/src/lib.rs` and the functions are `pub fn`; should be visible from `cupel-testing` as `cupel::analytics::budget_utilization`. Verify at compile time.
- **`PartialEq` on `IncludedItem` for HashSet use in Pattern 13** — `IncludedItem` derives `PartialEq` but Rust's `HashSet` requires `Eq + Hash`. Since `IncludedItem` contains `f64` (score), it cannot derive `Eq` or `Hash`. The Rust port of Pattern 13 **cannot use `HashSet<&IncludedItem>`** as the .NET version does. Alternative: track top-N by index or use a `Vec<usize>` of top-N indices sorted by score descending.
- **Pattern 13 index-based approach** — instead of `HashSet<IncludedItem>`, collect `(score, original_index)` pairs, sort by score descending, take first N, build a set of expected edge positions, then verify each top-N index is at an edge position. This avoids the `Hash` requirement entirely.

## Skills Discovered

| Technology | Skill | Status |
|------------|-------|--------|
| Rust | (none found relevant) | n/a — standard Rust library patterns only |

## Sources

- Spec vocabulary (13 patterns, error messages, edge cases): `spec/src/testing/vocabulary.md`
- .NET reference implementation: `src/Wollax.Cupel.Testing/SelectionReportAssertionChain.cs`
- Rust diagnostic types (full): `crates/cupel/src/diagnostics/mod.rs`
- Analytics helpers: `crates/cupel/src/analytics.rs`
- S01 forward intelligence: `#[allow(dead_code)]` removal, test construction pattern, `TraceDetailLevel::Item` (not `Full`)
- Decisions: D126 (separate crate), D127 (fluent chain), D128 (panic), D129 (S01 strategy), D096 (`PlaceTopNScoredAtEdges` tie handling via minTopScore + HashSet — **HashSet approach blocked by `f64` in Rust; use index-based instead**)
