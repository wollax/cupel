# Introduction

**Cupel Specification Version 1.0.0**

This document is the language-agnostic specification for the **Cupel context selection algorithm**. Cupel selects, scores, and orders context items for inclusion in a language model's context window, subject to a token budget.

## Purpose

Large language model applications must decide which pieces of context (messages, documents, tool outputs, memories) to include in a finite context window. Cupel defines a deterministic pipeline that takes a set of candidate context items and a token budget, and produces an ordered list of selected items that fits within the budget.

This specification enables interoperable implementations across programming languages. An implementation that conforms to this specification will produce the same selected items in the same order as any other conforming implementation, given identical inputs.

## Scope

This specification defines:

- The **data model**: ContextItem, ContextBudget, ScoredItem, and supporting enumerations
- The **pipeline**: a fixed 6-stage transformation (Classify, Score, Deduplicate, Sort, Slice, Place)
- All **scorer algorithms**: RecencyScorer, PriorityScorer, KindScorer, TagScorer, FrequencyScorer, ReflexiveScorer, CompositeScorer, ScaledScorer
- All **slicer strategies**: GreedySlice, KnapsackSlice, QuotaSlice
- All **placer strategies**: ChronologicalPlacer, UShapedPlacer
- **Overflow handling**: Throw, Truncate, Proceed strategies

This specification does **not** define:

- Streaming or asynchronous pipeline execution (implementation-specific)
- Diagnostics, tracing, or observability infrastructure
- Dependency injection or builder APIs
- Serialization formats (JSON, etc.)
- Named policies or preset configurations
- Tokenizer implementations (the caller provides token counts)

## Conformance Model

A conforming implementation must exhibit **behavioral equivalence**: given the same input items, budget, scorer, slicer, and placer configuration, a conforming implementation must select the same items in the same order as the reference behavior defined in this specification.

Behavioral equivalence does **not** require bit-exact floating-point scores. Intermediate score values may differ due to floating-point evaluation order, provided the final selection and ordering are identical. Conformance tests compare selected item sets and their ordering, not exact score values. Where individual scores are verified (e.g., per-stage scorer tests), an epsilon tolerance of `1e-9` is used.

See [Conformance Levels](conformance/levels.md) for the full conformance requirements.

## Numeric Precision

All scoring computations MUST use **IEEE 754 64-bit double-precision floating-point** arithmetic. This includes:

- Individual scorer outputs
- Composite score aggregation (weighted averages)
- Score-based comparisons in sorting, deduplication, and slicing
- Density calculations in slicer algorithms

Implementations MUST NOT use 32-bit floats, fixed-point, or arbitrary-precision arithmetic for scoring. Integer arithmetic is permitted for token counts and budget calculations.

## Notation Conventions

Algorithms in this specification use CLRS-style pseudocode with the following conventions:

| Convention | Meaning |
|---|---|
| **bold lowercase** | Keywords: **for**, **if**, **else**, **return**, **while** |
| `monospace` | Variables, field names, function names |
| `<-` | Assignment (not `=`, to avoid confusion with equality) |
| `=` | Equality comparison |
| `[i]` | 0-based array/list indexing |
| `//` | Comments |
| `length(x)` | Number of elements in array or list `x` |

## Pipeline Overview

The Cupel pipeline is a fixed sequence of six stages. Every execution follows this order; stages cannot be reordered or skipped.

```mermaid
flowchart LR
    A[Candidate Items] --> B[Classify]
    B --> C[Score]
    C --> D[Deduplicate]
    D --> E[Sort]
    E --> F[Slice]
    F --> G[Place]
    G --> H[Ordered Context Window]
```

| Stage | Input | Output | Purpose |
|---|---|---|---|
| [Classify](pipeline/classify.md) | Candidate items | Pinned list + Scoreable list | Partition items; exclude invalid |
| [Score](pipeline/score.md) | Scoreable items | Scored items | Compute relevance scores |
| [Deduplicate](pipeline/deduplicate.md) | Scored items | Unique scored items | Remove duplicate content |
| [Sort](pipeline/sort.md) | Unique scored items | Sorted scored items | Order by score descending |
| [Slice](pipeline/slice.md) | Sorted scored items | Budget-fitting items | Select items within token budget |
| [Place](pipeline/place.md) | Sliced + pinned items | Ordered items | Determine final presentation order |

The pipeline operates on the principle of **ordinal-only scoring**: scorers assign relevance scores (rank), slicers select items within budget (drop), and placers determine presentation order (position). Each concern is strictly separated.
