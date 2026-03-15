# Cupel

Context management library for coding agents. Given a set of context items and a token budget, Cupel determines the optimal context window — maximizing information density while respecting LLM attention mechanics.

Available in **.NET** and **Rust**.

## Install

**.NET:**
```bash
dotnet add package Wollax.Cupel
```

Optional companions:
```bash
dotnet add package Wollax.Cupel.Extensions.DependencyInjection
dotnet add package Wollax.Cupel.Tiktoken
dotnet add package Wollax.Cupel.Json
```

**Rust:**
```toml
[dependencies]
cupel = "1.1"

# Optional: serialization support
cupel = { version = "1.1", features = ["serde"] }
```

## Quick Start

```csharp
using Wollax.Cupel;
using Wollax.Cupel.Scoring;

var budget = new ContextBudget(maxTokens: 4000, targetTokens: 3000);

var pipeline = CupelPipeline.CreateBuilder()
    .WithBudget(budget)
    .AddScorer(new RecencyScorer(), weight: 0.6)
    .AddScorer(new PriorityScorer(), weight: 0.4)
    .UseGreedySlice()
    .Build();

var items = new[]
{
    new ContextItem { Content = "System prompt", Tokens = 100, Kind = ContextKind.SystemPrompt },
    new ContextItem { Content = "User message", Tokens = 50, Kind = ContextKind.Message },
    new ContextItem { Content = "Tool output", Tokens = 2000, Kind = ContextKind.ToolOutput },
};

ContextResult result = pipeline.Execute(items);
// result.Items — optimally selected and placed context
// result.TotalTokens — total tokens used
// result.Report — why each item was included/excluded
```

## Named Policies

Skip manual configuration with opinionated presets:

```csharp
var pipeline = CupelPipeline.CreateBuilder()
    .WithBudget(budget)
    .WithPolicy(CupelPresets.Chat())
    .Build();
```

Available presets: `Chat`, `CodeReview`, `Rag`, `DocumentQa`, `ToolUse`, `LongRunning`, `Debugging`

## Pipeline

Cupel executes a fixed 5-stage pipeline:

```
Classify → Score → Deduplicate → Slice → Place
```

- **Classify** — categorize items by kind
- **Score** — rank items using composable scorers (0.0–1.0)
- **Deduplicate** — remove byte-exact duplicates
- **Slice** — select items within budget (greedy, knapsack, quota, or stream)
- **Place** — order items for optimal attention (U-shaped or chronological)

Pinned items bypass scoring and are always included.

## Scorers

| Scorer | Signal |
|--------|--------|
| `RecencyScorer` | Temporal proximity (relative rank) |
| `PriorityScorer` | Explicit priority value |
| `KindScorer` | Content kind weights |
| `TagScorer` | Tag-based categorical boosting |
| `FrequencyScorer` | Reference frequency |
| `ReflexiveScorer` | Caller-supplied FutureRelevanceHint |
| `CompositeScorer` | Weighted combination of any scorers |
| `ScaledScorer` | Normalizes any scorer to 0–1 range |

## Slicers

| Slicer | Strategy |
|--------|----------|
| `GreedySlice` | O(N log N) fill by score/token ratio |
| `KnapsackSlice` | 0-1 DP for optimal budget utilization |
| `QuotaSlice` | Semantic quotas: `Require(Kind, min%)` / `Cap(Kind, max%)` |
| `StreamSlice` | Online selection for `IAsyncEnumerable` sources |

## Explainability

Every inclusion and exclusion has a traceable reason:

```csharp
ContextResult result = pipeline.DryRun(items);
SelectionReport report = result.Report!;

foreach (var included in report.Included)
    Console.WriteLine($"+ {included.Item.Content} (score: {included.Score:F2}, reason: {included.Reason})");

foreach (var excluded in report.Excluded)
    Console.WriteLine($"- {excluded.Item.Content} (score: {excluded.Score:F2}, reason: {excluded.Reason})");
```

## Packages

| Package | Purpose |
|---------|---------|
| `Wollax.Cupel` | Core library (zero dependencies) |
| `Wollax.Cupel.Extensions.DependencyInjection` | Microsoft.Extensions.DI integration |
| `Wollax.Cupel.Tiktoken` | Token counting via Microsoft.ML.Tokenizers |
| `Wollax.Cupel.Json` | JSON policy serialization |

## Rust Crate

The [`cupel`](https://crates.io/crates/cupel) crate implements the full conformance specification in Rust. See `crates/cupel/` for source and [docs.rs](https://docs.rs/cupel) for API documentation.

Features:
- Full conformance with all 28 required test vectors
- Optional `serde` feature for Serialize/Deserialize on all public types
- Validation-on-deserialize for `ContextBudget` (constructor invariants enforced)
- 3 runnable examples: `basic_pipeline`, `serde_roundtrip`, `quota_slicing`

## Specification

Cupel's algorithm is documented as a [language-agnostic specification](spec/) with 28 conformance test vectors. Both the .NET and Rust implementations pass all conformance vectors.

## License

MIT
