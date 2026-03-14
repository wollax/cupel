# Data Model

The Cupel data model consists of three primary types and three supporting enumerations. These types flow through every stage of the pipeline.

## Primary Types

- **[ContextItem](data-model/context-item.md)** — An immutable record representing a single piece of context (a message, document, tool output, etc.). Every scorer, slicer, and placer operates on ContextItem instances.

- **[ContextBudget](data-model/context-budget.md)** — The budget constraint model that controls how many tokens the pipeline can select. Validated at construction time so that no invalid budget can exist at runtime.

- **[ScoredItem](data-model/enumerations.md#scoreditem)** — A pair of (ContextItem, Score) produced by the scoring stage and consumed by subsequent pipeline stages.

## Enumerations

- **[ContextKind](data-model/enumerations.md#contextkind)** — An extensible string enumeration classifying the type of a context item (Message, Document, ToolOutput, Memory, SystemPrompt).

- **[ContextSource](data-model/enumerations.md#contextsource)** — An extensible string enumeration identifying the origin of a context item (Chat, Tool, Rag).

- **[OverflowStrategy](data-model/enumerations.md#overflowstrategy)** — A closed enumeration controlling behavior when selected items exceed the token budget (Throw, Truncate, Proceed).

## Immutability

All data model types are immutable. Once constructed, a ContextItem or ContextBudget cannot be modified. Pipeline stages produce new collections rather than mutating inputs. This invariant simplifies reasoning about pipeline behavior and enables safe concurrent access.
