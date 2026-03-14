# CompositeScorer

The CompositeScorer combines multiple child scorers into a single score using a weighted average.

## Overview

CompositeScorer is a **composite scorer** — it delegates to a list of child scorers, each with an associated weight, and returns the weighted average of their outputs. This enables building rich scoring strategies from simple building blocks.

CompositeScorer is the primary mechanism for combining multiple scoring signals (e.g., "60% recency + 30% priority + 10% kind") into a single relevance score.

## Algorithm

### Scoring

```text
COMPOSITE-SCORE(item, allItems, scorers, normalizedWeights):
    // scorers: array of Scorer
    // normalizedWeights: array of float64, sum = 1.0

    result <- 0.0
    for i <- 0 to length(scorers) - 1:
        result <- result + scorers[i].Score(item, allItems) * normalizedWeights[i]
    return result
```

### Construction

At construction time, the raw weights are normalized so they sum to 1.0:

```text
CONSTRUCT-COMPOSITE(entries):
    // entries: list of (scorer, weight) pairs

    if length(entries) = 0:
        ERROR("At least one scorer entry is required")

    totalWeight <- 0.0
    for i <- 0 to length(entries) - 1:
        (scorer, weight) <- entries[i]
        if scorer = null:
            ERROR("Scorer must not be null")
        if weight <= 0.0:
            ERROR("Weight must be positive")
        if weight is not finite:
            ERROR("Weight must be finite")
        totalWeight <- totalWeight + weight

    normalizedWeights <- new array of length(entries)
    for i <- 0 to length(entries) - 1:
        normalizedWeights[i] <- entries[i].weight / totalWeight

    DETECT-CYCLES(entries)

    // Store scorers and normalizedWeights
```

### Weight Normalization

Weights are **relative**, not absolute. The following configurations produce identical results:

- `[(RecencyScorer, 3.0), (PriorityScorer, 1.0)]`
- `[(RecencyScorer, 0.75), (PriorityScorer, 0.25)]`

Both normalize to weights `[0.75, 0.25]`.

## Cycle Detection

CompositeScorer supports nesting — a CompositeScorer may contain other CompositeScorer or [ScaledScorer](scaled.md) instances as children. However, circular references are invalid and MUST be detected at construction time.

```text
DETECT-CYCLES(entries):
    visited <- empty set (by reference identity)
    inPath  <- empty set (by reference identity)
    for i <- 0 to length(entries) - 1:
        DETECT-CYCLES-DFS(entries[i].scorer, visited, inPath)

DETECT-CYCLES-DFS(node, visited, inPath):
    if node is in visited:
        if node is in inPath:
            ERROR("Cycle detected: scorer appears in its own dependency graph")
        return
    add node to visited
    add node to inPath

    if node is a CompositeScorer:
        for each child in node.children:
            DETECT-CYCLES-DFS(child, visited, inPath)
    else if node is a ScaledScorer:
        DETECT-CYCLES-DFS(node.inner, visited, inPath)

    remove node from inPath
```

The cycle detection traverses the scorer graph using depth-first search with **reference identity** comparison. A cycle exists when a scorer appears in its own ancestor path.

## Edge Cases

| Condition | Result |
|---|---|
| Empty entries list | Construction error |
| Single entry | Normalized weight is 1.0; effectively delegates to the child |
| All children return 0.0 | Composite returns 0.0 |
| All children return 1.0 | Composite returns 1.0 (weights sum to 1.0) |
| Nested CompositeScorer | Valid if acyclic; each child is scored recursively |
| Self-referential cycle | Construction error |

## Complexity

- **Construction:** O(*V* + *E*) for cycle detection, where *V* is the number of distinct scorer nodes and *E* is the number of parent-child edges.
- **Scoring:** O(*C* * cost_per_child) per item, where *C* is the number of child scorers. For a flat composite with simple children, this is O(*C*) per item.
- **Space:** O(*C*) for normalized weight storage, O(*V*) for cycle detection.

## Conformance Notes

- Weights MUST be positive (strictly greater than 0.0) and finite. Zero weights are rejected at construction time.
- Weight normalization MUST divide each weight by the sum of all weights. The normalized weights MUST sum to approximately 1.0 (subject to floating-point precision).
- Cycle detection MUST use reference identity, not structural equality. Two distinct CompositeScorer instances with identical configurations are not considered cyclic.
- Child scorers are invoked in order (index 0, 1, 2, ...). While the weighted sum is mathematically commutative, the invocation order is fixed for determinism.
