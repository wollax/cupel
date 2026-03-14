# Phase 7: Explainability & Overflow Handling - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Ship explainability and overflow handling for the Cupel pipeline. SelectionReport evolves from its Phase 2 stub to carry included items (with scores and inclusion reasons), excluded items (with scores and ExclusionReason enum), and summary stats. DryRun() is a separate method that always produces a full report. OverflowStrategy governs what happens when sliced items exceed budget after combining with pinned items.

</domain>

<decisions>
## Implementation Decisions

### ExclusionReason taxonomy
- Fine-grained enum (~8 values): `BudgetExceeded`, `ScoredTooLow`, `Deduplicated`, `QuotaCapExceeded`, `QuotaRequireDisplaced`, `NegativeTokens`, `PinnedOverride`, `Filtered`
- Excluded items carry their computed score — a 0.8-scored item excluded due to budget tells the user "this was good but didn't fit"
- Pinned items that override quota caps appear as a special category in the report — they're "included via pin, overriding cap" (not standard included, not excluded)
- Deduplicated items reference which item they were deduplicated against — e.g., "excluded: item X (deduplicated — kept item Y with score 0.9)"

### IncludedItem / ExcludedItem types
- Both are sealed records — value equality for test assertions, immutability matches report's read-only nature
- `IncludedItem`: item + score + `InclusionReason` enum (Scored, Pinned, ZeroToken)
- `ExcludedItem`: item + score + `ExclusionReason` enum + optional `ContextItem? DeduplicatedAgainst` reference

### SelectionReport structure
- Evolves existing Phase 2 stub (which has `Events` only)
- Adds: `IReadOnlyList<IncludedItem> Included`, `IReadOnlyList<ExcludedItem> Excluded`
- Adds: `int TotalCandidates`, `int TotalTokensConsidered` summary stats
- Retains: `IReadOnlyList<TraceEvent> Events` for existing trace consumers
- Excluded items ordered by score descending — "best items that didn't make it" is the most actionable view
- Per-stage timing stays in trace layer only — report is about what/why, not how fast

### DryRun() API shape
- Separate method on `CupelPipeline`: `pipeline.DryRun(items)` → `ContextResult`
- Returns same `ContextResult` type with `Report` always populated — callers can swap Execute() ↔ DryRun() without changing consuming code
- DryRun() internally creates a `DiagnosticTraceCollector` regardless of what the caller passes — always produces full report
- Sync-only — no `DryRunStreamAsync()`. Streaming sources are non-idempotent (may yield different items on re-enumeration), violating DryRun's "same input → same output" contract. Document that streaming users should materialize first.
- Idempotency: calling DryRun() twice with the same input produces identical results

### OverflowStrategy scope and configuration
- Only governs the post-slice phase: when sliced items + pinned items exceed TargetTokens
- Pinned items alone exceeding MaxTokens remains a hard `InvalidOperationException` (configuration error, unchanged)
- Configured on builder: `builder.WithOverflowStrategy(OverflowStrategy.Truncate)` — no execute-time override
- Default strategy: `OverflowStrategy.Throw` (fail-fast, no silent data loss)
- Three strategies:
  - `Throw` — throws `OverflowException` with details
  - `Truncate` — removes lowest-scored items first until within budget
  - `Proceed` — continues with optional observer callback
- Observer callback: `Action<OverflowEvent>` — simple delegate, no interface
- `OverflowEvent` is a sealed record: `int TokensOverBudget`, `IReadOnlyList<ContextItem> OverflowingItems`, `ContextBudget Budget`
- Builder API: `builder.WithOverflowStrategy(OverflowStrategy.Proceed, onOverflow: details => ...)`
- `Throw` and `Truncate` don't accept a callback

### Claude's Discretion
- Internal implementation of how DryRun() reuses Execute() logic without duplication
- How IncludedItem/ExcludedItem records are populated during pipeline execution (tracking through stages)
- Whether ExclusionReason tracking requires changes to ISlicer or is handled externally
- PublicAPI.Unshipped.txt entries for all new types
- Exact exception type and message for OverflowStrategy.Throw
- How the report builder accumulates items through pipeline stages

</decisions>

<specifics>
## Specific Ideas

- The pipeline currently discards exclusion information — items that don't survive slicing simply disappear. Phase 7 needs to track what was dropped and why at each stage.
- DryRun() and Execute() should share the same pipeline logic. DryRun() just forces tracing on and populates the report.
- The existing `SelectionReport { Events }` stub needs backward-compatible evolution — existing code that reads Events should still work.
- OverflowStrategy.Truncate reuses the same score-descending ordering the pipeline already computes.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 07-explainability-overflow-handling*
*Context gathered: 2026-03-13*
