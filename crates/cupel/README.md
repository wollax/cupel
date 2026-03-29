# cupel

Context window management pipeline for LLM applications.

Given a set of context items — messages, documents, tool outputs, memory — and a
token budget, Cupel determines the optimal subset and ordering for a model's
context window. The pipeline follows a fixed six-stage flow: classify inputs,
**score** every candidate for relevance, deduplicate, sort, **slice** the list
to fit the budget, and **place** the selected items in an attention-optimal
order. Each configurable stage (scorer, slicer, placer) is independently
swappable through trait implementations, and every inclusion/exclusion decision
carries a traceable reason.

Cupel is framework-agnostic. It accepts pre-counted token lengths and returns
plain `Vec<ContextItem>` — no LLM client, tokenizer, or async runtime required.

## Glossary

| Term | Description |
|------|-------------|
| **ContextItem** | An immutable record representing a single piece of context (message, document, tool output, etc.). Constructed via `ContextItemBuilder`. |
| **ContextBudget** | Token budget constraints: hard ceiling (`max_tokens`), soft goal (`target_tokens`), output reserve, per-kind reserved slots, and safety margin. Validated at construction. |
| **Scorer** | A trait that computes a relevance score for a context item. Eight built-in implementations cover common strategies. |
| **Slicer** | A trait that selects items from the scored list to fit within the budget. Built-in: `GreedySlice`, `KnapsackSlice`, `QuotaSlice`. |
| **Placer** | A trait that determines the final presentation order. Built-in: `ChronologicalPlacer`, `UShapedPlacer`. |
| **Pipeline** | The fixed six-stage executor: Classify, Score, Deduplicate, Sort, Slice, Place. Built via `Pipeline::builder()`. |
| **ContextKind** | Extensible string enum classifying item type (`Message`, `Document`, `ToolOutput`, `Memory`, `SystemPrompt`). Case-insensitive comparison. |
| **ContextSource** | Extensible string enum identifying item origin (`Chat`, `Tool`, `Rag`). Case-insensitive comparison. |

## Quickstart

A minimal pipeline that scores three items by recency, greedily fills a
500-token budget, and places results in chronological order:

```rust
use std::collections::HashMap;
use chrono::Utc;
use cupel::*;

# fn main() -> Result<(), CupelError> {
let now = Utc::now();

let items = vec![
    ContextItemBuilder::new("System: you are a helpful assistant", 20)
        .kind(ContextKind::new("SystemPrompt")?)
        .timestamp(now - chrono::Duration::seconds(30))
        .build()?,
    ContextItemBuilder::new("User: summarize the RFC", 15)
        .kind(ContextKind::new("Message")?)
        .timestamp(now - chrono::Duration::seconds(20))
        .build()?,
    ContextItemBuilder::new("Tool: RFC-1234 full text (4k tokens)", 400)
        .kind(ContextKind::new("ToolOutput")?)
        .timestamp(now - chrono::Duration::seconds(10))
        .build()?,
];

let budget = ContextBudget::new(
    1024,  // max_tokens (model context window)
    500,   // target_tokens (soft goal)
    200,   // output_reserve
    HashMap::new(),
    0.0,
)?;

let pipeline = Pipeline::builder()
    .scorer(Box::new(RecencyScorer))
    .slicer(Box::new(GreedySlice))
    .placer(Box::new(ChronologicalPlacer))
    .build()?;

let result = pipeline.run(&items, &budget)?;
assert!(!result.is_empty());
# Ok(())
# }
```

### Multi-scorer pipeline

A more realistic configuration combining `KindScorer` and `RecencyScorer`
through a weighted composite. `QuotaSlice` reserves at least 20% of the
budget for tool outputs while capping messages at 50%. `UShapedPlacer`
positions the highest-scored items at the start and end of the window where
LLM attention is strongest.

```rust
use std::collections::HashMap;
use chrono::Utc;
use cupel::*;

# fn main() -> Result<(), CupelError> {
let now = Utc::now();

let items = vec![
    ContextItemBuilder::new("System: you are a code reviewer", 25)
        .kind(ContextKind::new("SystemPrompt")?)
        .timestamp(now - chrono::Duration::seconds(60))
        .pinned(true)
        .build()?,
    ContextItemBuilder::new("User: review my PR", 10)
        .kind(ContextKind::new("Message")?)
        .timestamp(now - chrono::Duration::seconds(50))
        .build()?,
    ContextItemBuilder::new("Memory: user prefers concise feedback", 12)
        .kind(ContextKind::new("Memory")?)
        .timestamp(now - chrono::Duration::seconds(45))
        .build()?,
    ContextItemBuilder::new("Tool: git diff output (3k tokens)", 350)
        .kind(ContextKind::new("ToolOutput")?)
        .timestamp(now - chrono::Duration::seconds(5))
        .build()?,
    ContextItemBuilder::new("Tool: lint warnings", 80)
        .kind(ContextKind::new("ToolOutput")?)
        .timestamp(now - chrono::Duration::seconds(3))
        .build()?,
];

// Weighted composite: 60% kind relevance, 40% recency
let scorer = CompositeScorer::new(vec![
    (Box::new(ScaledScorer::new(Box::new(KindScorer::with_default_weights()))), 0.6),
    (Box::new(ScaledScorer::new(Box::new(RecencyScorer))), 0.4),
])?;

// Reserve 20% for tool outputs, cap messages at 50%
let quotas = vec![
    QuotaEntry::new(ContextKind::new("ToolOutput")?, 20.0, 80.0)?,
    QuotaEntry::new(ContextKind::new("Message")?, 0.0, 50.0)?,
];
let slicer = QuotaSlice::new(quotas, Box::new(GreedySlice))?;

let budget = ContextBudget::new(2048, 800, 400, HashMap::new(), 0.0)?;

let pipeline = Pipeline::builder()
    .scorer(Box::new(scorer))
    .slicer(Box::new(slicer))
    .placer(Box::new(UShapedPlacer))
    .build()?;

let result = pipeline.run(&items, &budget)?;
// Pinned system prompt is always included regardless of scoring
assert!(result.iter().any(|item| item.content().contains("code reviewer")));
# Ok(())
# }
```

## Scorers

| Scorer | Use case | Mechanism |
|--------|----------|-----------|
| `RecencyScorer` | Prefer recent items | Rank-based; requires timestamp |
| `PriorityScorer` | Explicit importance ranking | Rank-based; requires priority field |
| `KindScorer` | Weight by content type | Absolute; configurable weight map |
| `TagScorer` | Weighted tag matching | Absolute; configurable tag weights |
| `FrequencyScorer` | Boost commonly-tagged items | Relative; requires tags |
| `ReflexiveScorer` | Pass through future\_relevance\_hint | Absolute; requires hint field |
| `CompositeScorer` | Combine multiple strategies | Weighted average; meta-scorer |
| `ScaledScorer` | Normalize any scorer to \[0,1\] | Min-max normalization; wrapper |

## Serde support

Enable the `serde` feature for `Serialize`/`Deserialize` on all model types:

```toml
[dependencies]
cupel = { version = "1.2", features = ["serde"] }
```

`ContextBudget` and `ContextItem` validate constraints on deserialization —
invalid JSON is rejected at the boundary, not at pipeline runtime.

## License

MIT
