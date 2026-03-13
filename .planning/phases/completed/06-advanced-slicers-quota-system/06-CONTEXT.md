# Phase 6: Advanced Slicers & Quota System - Context

**Gathered:** 2026-03-13
**Status:** Ready for planning

<domain>
## Phase Boundary

Deliver three new slicer implementations: KnapsackSlice (provably optimal selection via 0/1 knapsack with bucket discretization), QuotaSlice (semantic balance via percentage-based Require/Cap constraints), and StreamSlice (unbounded IAsyncEnumerable sources without full materialization). QuotaSlice is a decorator that wraps other slicers. Pinned item + quota conflicts produce clear, actionable error messages.

</domain>

<decisions>
## Implementation Decisions

### QuotaSlice constraint model
- Require(Kind, minPercent) and Cap(Kind, maxPercent) are composable on the same Kind — `Require(Code, 20%) + Cap(Code, 40%)` means "20–40% Code"
- Validation happens at configuration time (builder), not at slice time — invalid quota sets never reach runtime
- Validation rules: Require ≤ Cap for the same Kind; sum of all Requires ≤ 100%
- When insufficient items exist to fill a required quota: best-effort selection + trace warning event (not an error)
- Pinned items do not count against quotas — they bypass scoring and enter at the Placer stage, outside QuotaSlice's visibility

### KnapsackSlice discretization
- Bucket size is configurable via constructor parameter, defaulting to 100 tokens
- Zero-token items are treated as zero-weight items in the knapsack formulation (not auto-included separately like GreedySlice)
- Trace event emitted when KnapsackSlice produces the same selection as GreedySlice would have — helps users evaluate whether the overhead is justified
- Document a guidance threshold for when KnapsackSlice adds value over GreedySlice (item count, token variance characteristics)

### StreamSlice processing model
- New `IAsyncSlicer` interface alongside existing `ISlicer` — StreamSlice implements `IAsyncSlicer`, not `ISlicer`
- `IAsyncSlicer` method signature takes `IAsyncEnumerable<ScoredItem>` and `CancellationToken`, returns `Task<IReadOnlyList<ContextItem>>`
- Pipeline detects which interface the configured slicer implements and dispatches accordingly
- Budget-full is the primary stopping condition — once TargetTokens is reached, cancel the upstream enumerable via CancellationToken
- Scoring uses micro-batch strategy at the pipeline level: buffer N items from async source, score the batch (preserving rank-based scorer semantics within each batch), feed to StreamSlice incrementally
- Batch size is configurable on StreamSlice

### Slicer composition
- QuotaSlice is a decorator that wraps an inner `ISlicer` — e.g., `new QuotaSlice(innerSlicer: new GreedySlice(), quotas)`
- QuotaSlice partitions candidates by Kind, applies quotas to determine per-Kind token budgets, delegates to inner slicer for selection within each partition
- KnapsackSlice inside QuotaSlice is a supported combination — optimal selection within each quota band
- Single composition mechanism (decorator), no sequential chaining of slicers
- Builder API has quota-specific fluent methods: `builder.UseGreedySlice().WithQuotas(q => ...)` rather than manual constructor composition

### Claude's Discretion
- KnapsackSlice bucket size default (100 proposed, can adjust based on benchmarks)
- StreamSlice default micro-batch size
- Internal data structures for QuotaSlice's per-Kind partitioning
- Exact `IAsyncSlicer` interface name and namespace placement
- How QuotaSlice allocates "unassigned" budget (tokens not claimed by any Require) across Kinds
- TraceEvent specifics for quota undersupply warnings and knapsack-matches-greedy events

</decisions>

<specifics>
## Specific Ideas

- KnapsackSlice uses 0/1 knapsack with bucket discretization — not unbounded knapsack or fractional knapsack
- GreedySlice is the reference implementation: KnapsackSlice success criteria require "equal or better budget utilization" vs GreedySlice on test cases
- Existing zero-allocation discipline applies to KnapsackSlice and QuotaSlice Score() paths — for loops with indexer access, no LINQ/foreach/closures
- Stable sort pattern from Phase 5 (double Score, int Index tuple array + Array.Sort with static comparison delegate) should be reused where applicable

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope

</deferred>

---

*Phase: 06-advanced-slicers-quota-system*
*Context gathered: 2026-03-13*
