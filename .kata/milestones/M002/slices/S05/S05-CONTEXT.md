---
id: S05
milestone: M002
status: ready
---

# S05: Cupel.Testing Vocabulary Design — Context

## Goal

Write `spec/src/testing/vocabulary.md` defining ≥10 named assertion patterns over `SelectionReport`, each with a precise spec (what it asserts, tolerance, tie-breaking, error message format), and link the chapter from `spec/src/SUMMARY.md`.

## Why this Slice

The `Wollax.Cupel.Testing` NuGet package cannot be implemented until the assertion vocabulary is designed. Shipping assertion methods with ambiguous semantics (e.g. "what counts as high-scoring?" or "what counts as at-edge?") locks in an unstable API surface from day one. The vocabulary document is the prerequisite contract that M003 implementation can build against without re-debating semantics.

## Scope

### In Scope

- `spec/src/testing/vocabulary.md` — a complete vocabulary document containing:
  - ≥10 named assertion patterns over `SelectionReport`, covering at minimum:
    - `IncludeItemWith` — asserts a specific item appears in `Included`
    - `ExcludeItemWith` — asserts a specific item appears in `Excluded`
    - `HaveKindInIncluded` — asserts at least one item of a given kind is in `Included`
    - `HaveAtLeastNExclusions` — asserts `Excluded.Count >= N`
    - `HaveNoExclusionsForKind` — asserts no item of a given kind appears in `Excluded`
    - `ExcludeWithReason` — asserts a specific item is excluded with a specific `ExclusionReason`
    - `HaveTokenUtilizationAbove` — asserts `total_tokens_considered / budget` is above a threshold
    - `HaveBudgetUtilizationAbove` — asserts sum of `Included` item tokens / budget above a threshold
    - `HaveKindDiversity` — asserts at least N distinct kinds in `Included`
    - `PlaceItemAtEdge` — asserts a specific named item is at position 0 or position `(included.Count - 1)` in the `Included` list
  - For each pattern: what it asserts (precise, no undefined terms), tolerance (if applicable), tie-breaking behavior, error message format on failure
  - Explicit note: no snapshot assertions until `SelectionReport` ordering stability for ties is guaranteed (D041)
  - Note: S05 brainstorm output (testing-vocabulary-angles-report.md) may surface additional patterns beyond these 10; include them if well-specified
- `spec/src/SUMMARY.md` updated to include the new chapter under Testing
- Create `spec/src/testing/` directory structure if it doesn't exist

### Out of Scope

- Implementation (no .NET or Rust code — D039 is locked)
- FluentAssertions dependency or any external assertion library (D041 is locked)
- Snapshot/ApprovalTests assertions (D041 is locked — ordering stability is a precondition)
- Assertions that depend on undefined relative thresholds: any assertion referencing "high-scoring" must take an explicit N parameter, not a vague qualifier
- Rust-specific assertion API (the vocabulary is language-agnostic; Rust ergonomics are a M003 implementation concern)
- Conformance test vectors (TOML files) — the vocabulary document specifies patterns in prose + example; executable vectors are M003 implementation-phase work

## Constraints

### Precision requirements (non-negotiable — these are the precision risk the roadmap names)

- **`PlaceItemAtEdge`**: "at edge" = position 0 or position `(included.Count - 1)` in the `Included` list. Nothing more. Tie-breaking: if multiple items share the edge score, the assertion passes if the named item occupies one of those boundary positions, not merely if it has the same score as the edge item.
- **"High-scoring" is banned as a vague qualifier**: no assertion may use "high-scoring" without an explicit N parameter. Any pattern that needs to identify top items takes `topN: int` as a required parameter. Example: `PlaceTopNAtEdges(topN: 3)` not `PlaceHighScorersAtEdges()`.
- **Error message format**: on failure, each assertion shows only the relevant field from `SelectionReport`, not the full report. The message must include: assertion name, what was expected, what was actually found. Example: `HaveKindInIncluded("ToolOutput") failed: Included contains kinds [Message, SystemPrompt] — no ToolOutput items found`.
- **No ambiguous terms in any pattern spec**: before the chapter is done, each pattern must be checked against the spec's own language — if a reviewer would need to guess what "edge", "high-scoring", "recent", or "important" means, the pattern is incomplete.

### Other constraints

- No implementation code (D039)
- No FluentAssertions dependency or snapshot assertions (D041)
- `SelectionReport` semantics are fixed: `Included` is in final placed order; `Excluded` is score-descending with insertion-order tiebreak. All assertions must be specified against these exact semantics.
- The vocabulary document is language-agnostic — it describes what each assertion checks, not how it is implemented in any language

## Integration Points

### Consumes

- `spec/src/diagnostics/selection-report.md` — exact `SelectionReport` field semantics (ordering, types, derivation rules); must be read before specifying any assertion that depends on list ordering or field values
- `spec/src/diagnostics/exclusion-reasons.md` — exact `ExclusionReason` variant names and fields; needed for `ExcludeWithReason` pattern
- `.planning/brainstorms/2026-03-15T12-23-brainstorm/highvalue-report.md` (Decision 2) — vocabulary design framing, precision requirements, and the explicit call-out that `PlaceHighScorersAtEdges()` needs precise specification
- S01 brainstorm output (`testing-vocabulary-angles-report.md`) — additional assertion pattern candidates surfaced post-v1.2; read before writing to capture any patterns that emerged from post-v1.2 landscape

### Produces

- `spec/src/testing/vocabulary.md` — complete vocabulary (≥10 patterns, no undefined terms, all precision questions answered)
- `spec/src/SUMMARY.md` — updated with the new chapter

## Open Questions

- **`SelectionReport` extension methods from March 15**: `BudgetUtilization`, `KindDiversity`, `ExcludedItemCount` were reshaped into extension methods in the March 15 brainstorm but not slotted into any slice. Working assumption: they are the semantic basis for `HaveBudgetUtilizationAbove`, `HaveKindDiversity`, and `HaveAtLeastNExclusions` respectively. The vocabulary document should note this and define the exact computation for each (e.g. `BudgetUtilization = sum(included[i].tokens) / budget.targetTokens`).
- **Tolerance for numeric threshold assertions**: `HaveTokenUtilizationAbove(0.8)` and similar — should these use a floating-point epsilon for edge cases (e.g. utilization = 0.80000001 passes a `> 0.8` test)? Working assumption: numeric threshold assertions use `>=` (not `>`) with no epsilon, and the spec states the comparison explicitly. Verify during execution whether any test result precision issue arises.
- **Pattern count ceiling**: the milestone says 10-15. Working assumption: S05 should target exactly 10 well-specified patterns rather than 15 loosely-specified ones. If S01 brainstorm surfaces additional patterns that are clearly useful and can be precisely specified, add them. Do not pad to reach 15 with vague patterns.
