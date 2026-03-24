# cupel-testing

Fluent assertion vocabulary for `cupel` selection reports.

`cupel-testing` provides a chainable assertion API for writing expressive integration
tests against [`SelectionReport`](https://docs.rs/cupel) values produced by
`cupel::Pipeline`. Instead of manually inspecting `report.included` and `report.excluded`,
you call `.should()` on any `SelectionReport` and compose readable assertion chains that
panic with precise, diagnostic messages on failure. Every assertion method returns
`&mut Self`, so multiple checks compose on a single expression without rebinding.

## Quickstart

Add to your crate's dev-dependencies:

```toml
[dev-dependencies]
cupel-testing = "0.1"
```

Then in your integration tests:

```rust
use cupel::{ContextBudget, ContextItemBuilder, ContextKind, DiagnosticTraceCollector,
             ExclusionReason, GreedySlice, Pipeline, RecencyScorer, TraceDetailLevel,
             UShapedPlacer};
use cupel_testing::SelectionReportAssertions;
use std::collections::HashMap;

let pipeline = Pipeline::builder()
    .scorer(Box::new(RecencyScorer))
    .slicer(Box::new(GreedySlice))
    .placer(Box::new(UShapedPlacer))
    .build()
    .expect("pipeline build failed");

let items = vec![
    ContextItemBuilder::new("msg", 10)
        .kind(ContextKind::new("Message").unwrap())
        .build()
        .unwrap(),
    ContextItemBuilder::new("oversized", 9999)
        .kind(ContextKind::new("Document").unwrap())
        .build()
        .unwrap(),
];

let budget = ContextBudget::new(100, 100, 0, HashMap::new(), 0.0).unwrap();
let mut collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
pipeline.run_traced(&items, &budget, &mut collector).unwrap();
let report = collector.into_report();

// Chain multiple assertions on a single `.should()` call:
report
    .should()
    .include_item_with_kind(ContextKind::new("Message").unwrap())
    .have_at_least_n_exclusions(1)
    .excluded_items_are_sorted_by_score_descending();
```

## API

All methods are on [`SelectionReportAssertionChain`] and return `&mut Self` for chaining.
Obtain a chain via `report.should()` (from the `SelectionReportAssertions` extension trait).

| Method | Description |
|--------|-------------|
| `include_item_with_kind(kind)` | At least one included item has the given `ContextKind`. |
| `include_item_matching(predicate)` | At least one included item satisfies the predicate closure. |
| `include_exact_n_items_with_kind(kind, n)` | Exactly `n` included items have the given `ContextKind`. |
| `exclude_item_with_reason(reason)` | At least one excluded item carries the given `ExclusionReason` variant. |
| `exclude_item_matching_with_reason(predicate, reason)` | At least one excluded item matches the predicate and has the given `ExclusionReason`. |
| `have_excluded_item_with_budget_details(predicate, item_tokens, available_tokens)` | An excluded item matching the predicate was excluded with `BudgetExceeded` carrying the exact token values. |
| `have_no_exclusions_for_kind(kind)` | No excluded item has the given `ContextKind`. |
| `have_at_least_n_exclusions(n)` | The excluded list contains at least `n` items. |
| `excluded_items_are_sorted_by_score_descending()` | Excluded items are in non-increasing score order. |
| `have_budget_utilization_above(threshold, budget)` | `sum(included tokens) / budget.max_tokens() >= threshold`. |
| `have_kind_coverage_count(n)` | At least `n` distinct `ContextKind` values appear in the included list. |
| `place_item_at_edge(predicate)` | The item matching the predicate is at position 0 or position `count−1`. |
| `place_top_n_scored_at_edges(n)` | The top-`n` scored included items occupy the `n` outermost edge positions (alternating from both ends). |

## License

MIT
