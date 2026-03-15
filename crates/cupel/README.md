# cupel

Context window management pipeline for LLM applications.

Given a set of context items (messages, documents, tool outputs, memory) and a token budget, Cupel determines the optimal context window — maximizing information density while respecting the attention mechanics of any LLM.

## Features

- **Fixed pipeline**: Classify → Score → Deduplicate → Slice → Place
- **Composable scoring**: 6 built-in scorers + composite + scaled
- **Multiple slicing strategies**: Greedy, Knapsack, Quota, Stream
- **Attention-aware placement**: U-shaped and chronological placers
- **Full explainability**: Every inclusion/exclusion has a traceable reason
- **Declarative policies**: Named presets for common use cases

## Usage

```rust
use cupel::prelude::*;

// Coming soon — source files migrate in Phase 17
```

## License

MIT
