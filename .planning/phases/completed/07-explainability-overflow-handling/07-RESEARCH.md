# Phase 7: Explainability & Overflow Handling - Research

**Researched:** 2026-03-13
**Mode:** Ecosystem (codebase-focused, zero external dependencies)

---

## Standard Stack

This is a pure BCL project. No external packages. All patterns use built-in .NET 10 types.

| Concern | BCL Types to Use | Confidence |
|---------|-----------------|------------|
| Immutable report records | `sealed record` with `required` init-only props | HIGH |
| Read-only collections | `IReadOnlyList<T>` backed by `List<T>` or arrays | HIGH |
| Strategy enum | Plain `enum` (not smart enum pattern) | HIGH |
| Observer callback | `Action<T>` delegate | HIGH |
| Exception for overflow | `OverflowException` (BCL, semantically correct) | HIGH |
| Value equality in tests | Sealed records provide structural equality by default | HIGH |
| Builder pattern | Existing `PipelineBuilder` pattern — add fields + fluent methods | HIGH |

---

## Architecture Patterns

### 1. Report Accumulation via Internal Builder (HIGH confidence)

**Pattern:** Use an internal `ReportBuilder` class that accumulates included/excluded items as the pipeline progresses through stages. The pipeline's `Execute()` method creates one when tracing is enabled, passes it alongside `ITraceCollector`, and finalizes it into a `SelectionReport` at the end.

**Why not modify ITraceCollector:** The trace collector is a public interface focused on events. Adding item tracking would break ISP and require changes to all implementations (DiagnosticTraceCollector, NullTraceCollector, any user implementations). A separate internal builder keeps concerns clean.

**Shape:**
```csharp
// Internal — not part of public API
internal sealed class ReportBuilder
{
    private readonly List<IncludedItem> _included = [];
    private readonly List<ExcludedItem> _excluded = [];
    private int _totalCandidates;
    private int _totalTokensConsidered;

    public void RecordExcluded(ContextItem item, double score, ExclusionReason reason, ContextItem? deduplicatedAgainst = null) { ... }
    public void RecordIncluded(ContextItem item, double score, InclusionReason reason) { ... }
    public void SetCandidateStats(int totalCandidates, int totalTokensConsidered) { ... }

    public SelectionReport Build(IReadOnlyList<TraceEvent> events) { ... }
}
```

**Key insight from codebase:** The pipeline currently uses `trace.IsEnabled` to gate trace work. The ReportBuilder should follow the same pattern — only instantiated when diagnostic tracing is active (i.e., `traceCollector is DiagnosticTraceCollector`). This preserves the zero-cost path for production use.

### 2. DryRun() as Thin Wrapper Over Execute() (HIGH confidence)

**Pattern:** `DryRun()` calls the same internal pipeline logic as `Execute()`, but forces a `DiagnosticTraceCollector` with `TraceDetailLevel.Item` to ensure full report population.

**Implementation approach — extract shared method:**
```csharp
public ContextResult Execute(IReadOnlyList<ContextItem> items, ITraceCollector? traceCollector = null)
    => ExecuteCore(items, traceCollector ?? NullTraceCollector.Instance, isDryRun: false);

public ContextResult DryRun(IReadOnlyList<ContextItem> items)
    => ExecuteCore(items, new DiagnosticTraceCollector(TraceDetailLevel.Item), isDryRun: true);

private ContextResult ExecuteCore(IReadOnlyList<ContextItem> items, ITraceCollector trace, bool isDryRun) { ... }
```

**The `isDryRun` flag:** The CONTEXT.md says DryRun() "internally creates a DiagnosticTraceCollector regardless of what the caller passes." Since DryRun() has no traceCollector parameter, this is handled naturally — DryRun always creates its own collector. The `isDryRun` boolean may not be needed at all if the only difference is the forced trace collector. However, if future behavior differs (e.g., DryRun skips side effects), the flag provides a clean extension point.

**Recommendation:** Start without `isDryRun` flag. DryRun() simply calls `ExecuteCore` with a forced DiagnosticTraceCollector. If distinct behavior is needed later, add the flag then.

### 3. ExclusionReason Tracking Without ISlicer Changes (HIGH confidence)

**Critical finding:** The current `ISlicer.Slice()` returns `IReadOnlyList<ContextItem>` — only surviving items. It does NOT report which items were excluded or why. Changing this interface would be a breaking change.

**Pattern — diff-based tracking:** The pipeline already knows which items went into each stage and which came out. Exclusion reasons can be determined by diffing:

1. **Classify stage:** Items with `Tokens < 0` → `ExclusionReason.NegativeTokens`
2. **Dedup stage:** Items in `scored[]` but not in `deduped[]` → `ExclusionReason.Deduplicated` (with reference to the surviving item via the `bestByContent` dictionary)
3. **Slice stage:** Items in `sorted[]` but not in `slicedItems` → `ExclusionReason.BudgetExceeded` (for standard slicers) or quota-related reasons
4. **QuotaSlice specifics:** Need to determine if an item was excluded due to cap vs require displacement. This is harder since QuotaSlice is a decorator.

**QuotaSlice exclusion reason challenge:**
The QuotaSlice partitions items by kind and delegates to the inner slicer. The pipeline gets back a flat `IReadOnlyList<ContextItem>` with no information about which kind-partition an item was excluded from. To distinguish `QuotaCapExceeded` from `BudgetExceeded`, the pipeline can check: if the slicer is a `QuotaSlice`, compare each excluded item's kind against quota caps. If the kind's selected tokens are at its cap, the reason is `QuotaCapExceeded`. Otherwise, `BudgetExceeded`.

**Recommendation:** Handle this in the pipeline's post-slice diff logic. The pipeline already does `instanceof QuotaSlice` checks (line 242 in CupelPipeline.cs). Extend this pattern.

### 4. OverflowStrategy as Configuration on Pipeline (HIGH confidence)

**Pattern:** Store the strategy enum + optional callback on the pipeline (passed via builder). Apply after the MERGE PINNED step (after line 239 in current CupelPipeline.cs), before PLACE.

**Overflow detection point:** After merging pinned + sliced items, compute total tokens. If `totalTokens > budget.TargetTokens`, overflow has occurred. This is the post-slice overflow the CONTEXT.md scopes to.

**Strategy implementations:**
- `Throw`: throw `OverflowException` with message including tokens over budget and item count
- `Truncate`: remove lowest-scored non-pinned items from `merged[]` until within budget
- `Proceed`: invoke `Action<OverflowEvent>` callback if provided, continue

### 5. Evolving SelectionReport Backward-Compatibly (HIGH confidence)

**Current stub:**
```csharp
public sealed record SelectionReport
{
    public required IReadOnlyList<TraceEvent> Events { get; init; }
}
```

**Evolution approach:** Add new `required` properties. This is technically a breaking change for anyone constructing `SelectionReport` directly (they'd need to provide new required properties), but since the type is only constructed internally by the pipeline, this is safe. The `required` keyword ensures the pipeline always populates all fields.

However, if `required` is a concern for binary compatibility, use non-required properties with sensible defaults:
```csharp
public IReadOnlyList<IncludedItem> Included { get; init; } = [];
public IReadOnlyList<ExcludedItem> Excluded { get; init; } = [];
public int TotalCandidates { get; init; }
public int TotalTokensConsidered { get; init; }
```

**Recommendation:** Use `required` for `Included` and `Excluded` (matching the existing `Events` pattern). Use non-required for `TotalCandidates` and `TotalTokensConsidered` (they have natural defaults of 0). The library hasn't shipped to NuGet yet (PublicAPI.Shipped.txt is empty), so binary compat isn't a concern.

---

## Don't Hand-Roll

| Problem | Use Instead | Why |
|---------|------------|-----|
| Value equality for report types | `sealed record` | C# records generate `Equals`, `GetHashCode`, `ToString` automatically |
| Collection immutability | `IReadOnlyList<T>` with `.AsReadOnly()` or array | Don't create custom immutable list types |
| Null trace fast-path | Existing `NullTraceCollector.Instance` pattern | Already established in codebase |
| Builder validation | Follow existing `PipelineBuilder.Build()` pattern | Throws `InvalidOperationException` with clear messages |
| Exception type for overflow | `OverflowException` (BCL) | Semantically correct, well-known, includes message |
| Tracking item sets | `HashSet<ContextItem>(ReferenceEqualityComparer.Instance)` | Already used in pipeline (line 215) for diff operations |

---

## Common Pitfalls

### 1. Breaking the ISlicer Interface (HIGH confidence)
**Pitfall:** Temptation to add exclusion reason output to `ISlicer.Slice()`. This breaks all existing implementations.
**Prevention:** Use diff-based tracking in the pipeline itself. The pipeline knows what went in and what came out.

### 2. Report Allocation on Hot Path (HIGH confidence)
**Pitfall:** Allocating `IncludedItem`/`ExcludedItem` records when no one asked for a report.
**Prevention:** Only create `ReportBuilder` when `traceCollector is DiagnosticTraceCollector`. The existing `NullTraceCollector` fast-path must remain zero-cost.

### 3. ExclusionReason for Quota-Excluded Items (MEDIUM confidence)
**Pitfall:** Incorrectly attributing all post-slice exclusions to `BudgetExceeded` when some are actually `QuotaCapExceeded` or `QuotaRequireDisplaced`.
**Prevention:** When slicer is `QuotaSlice`, use quota metadata to classify exclusion reasons. Check if excluded item's kind is at its cap boundary.

### 4. DryRun() Idempotency with Stateful Scorers (MEDIUM confidence)
**Pitfall:** If a scorer has side effects (e.g., mutable frequency counter), DryRun() would trigger those side effects.
**Prevention:** Document that DryRun() calls the scorer. If scorers are stateful, DryRun() reflects that state. The CONTEXT.md explicitly states "calling DryRun() twice with the same input produces identical results" — this is a property of the pipeline + scorer combination, not something DryRun() can guarantee alone. Document this constraint.

### 5. OverflowStrategy.Truncate Removing Pinned Items (HIGH confidence)
**Pitfall:** Truncate strategy removes items by lowest score. Pinned items have score 1.0 so they'd survive, but this needs to be explicit.
**Prevention:** Truncate should explicitly skip pinned items. Remove only from non-pinned scored items, ordered by ascending score.

### 6. Mutable List Exposure in Report (HIGH confidence)
**Pitfall:** Exposing the internal `List<IncludedItem>` directly as `IReadOnlyList<T>` allows casting back to `List<T>`.
**Prevention:** Use `.ToArray()` or `List<T>.AsReadOnly()` when building the final report.

### 7. ExclusionReason Enum Expansion Breaking Existing Code (LOW confidence)
**Pitfall:** The existing `ExclusionReason` has 4 values. CONTEXT.md specifies ~8. Adding new enum values is generally backward-compatible in C#, but switch expressions without a default case will get warnings.
**Prevention:** Add the new values. This is pre-1.0; breaking changes are acceptable. Update PublicAPI.Unshipped.txt.

### 8. Circular Reference in DeduplicatedAgainst (LOW confidence)
**Pitfall:** `ExcludedItem.DeduplicatedAgainst` references a `ContextItem`. Since `ContextItem` is a sealed record, this is a value-type reference (not a graph cycle). No issue.
**Prevention:** None needed — `ContextItem` is immutable, no circular reference concern.

---

## Code Examples

### IncludedItem / ExcludedItem Records

```csharp
namespace Wollax.Cupel.Diagnostics;

/// <summary>Reason an item was included in the final selection.</summary>
public enum InclusionReason
{
    /// <summary>Item was included based on its score and budget availability.</summary>
    Scored,
    /// <summary>Item was pinned (always included regardless of scoring).</summary>
    Pinned,
    /// <summary>Item has zero tokens and is included at no cost.</summary>
    ZeroToken
}

/// <summary>An item included in the pipeline selection, with its score and reason.</summary>
public sealed record IncludedItem
{
    public required ContextItem Item { get; init; }
    public required double Score { get; init; }
    public required InclusionReason Reason { get; init; }
}

/// <summary>An item excluded from the pipeline selection, with its score and reason.</summary>
public sealed record ExcludedItem
{
    public required ContextItem Item { get; init; }
    public required double Score { get; init; }
    public required ExclusionReason Reason { get; init; }
    /// <summary>
    /// When <see cref="Reason"/> is <see cref="ExclusionReason.Deduplicated"/>,
    /// references the item that was kept instead.
    /// </summary>
    public ContextItem? DeduplicatedAgainst { get; init; }
}
```

### Expanded ExclusionReason Enum

```csharp
public enum ExclusionReason
{
    /// <summary>Item did not fit within the token budget.</summary>
    BudgetExceeded,
    /// <summary>Item scored below the selection threshold.</summary>
    ScoredTooLow,
    /// <summary>Item was removed during deduplication.</summary>
    Deduplicated,
    /// <summary>Item was excluded because its kind's quota cap was reached.</summary>
    QuotaCapExceeded,
    /// <summary>Item was displaced to make room for a kind's required quota.</summary>
    QuotaRequireDisplaced,
    /// <summary>Item had negative token count and was skipped.</summary>
    NegativeTokens,
    /// <summary>Item was overridden by a pinned item.</summary>
    PinnedOverride,
    /// <summary>Item was excluded by an external filter.</summary>
    Filtered
}
```

**Note:** This replaces the existing 4-value enum. The old `LowScore` maps to `ScoredTooLow`, `Duplicate` maps to `Deduplicated`, `QuotaExceeded` splits into `QuotaCapExceeded` and `QuotaRequireDisplaced`. This is a breaking change to the enum values — the numeric values shift. Since PublicAPI.Shipped.txt is empty (nothing shipped), this is safe. Update PublicAPI.Unshipped.txt to reflect the new values.

### OverflowStrategy + OverflowEvent

```csharp
namespace Wollax.Cupel;

/// <summary>Strategy for handling token overflow after slicing and pinned item merging.</summary>
public enum OverflowStrategy
{
    /// <summary>Throw an exception when overflow occurs.</summary>
    Throw,
    /// <summary>Remove lowest-scored non-pinned items until within budget.</summary>
    Truncate,
    /// <summary>Continue with all items, optionally invoking an observer.</summary>
    Proceed
}

namespace Wollax.Cupel.Diagnostics;

/// <summary>Details about a token overflow event.</summary>
public sealed record OverflowEvent
{
    public required int TokensOverBudget { get; init; }
    public required IReadOnlyList<ContextItem> OverflowingItems { get; init; }
    public required ContextBudget Budget { get; init; }
}
```

### Builder Integration

```csharp
// In PipelineBuilder:
private OverflowStrategy _overflowStrategy = OverflowStrategy.Throw;
private Action<OverflowEvent>? _onOverflow;

public PipelineBuilder WithOverflowStrategy(OverflowStrategy strategy)
{
    _overflowStrategy = strategy;
    _onOverflow = null;
    return this;
}

public PipelineBuilder WithOverflowStrategy(OverflowStrategy strategy, Action<OverflowEvent> onOverflow)
{
    ArgumentNullException.ThrowIfNull(onOverflow);
    if (strategy != OverflowStrategy.Proceed)
        throw new ArgumentException("Observer callback is only supported with OverflowStrategy.Proceed.", nameof(strategy));
    _overflowStrategy = strategy;
    _onOverflow = onOverflow;
    return this;
}
```

### Diff-Based Exclusion Tracking in Pipeline

```csharp
// After CLASSIFY stage — track negative-token exclusions:
if (reportBuilder is not null && item.Tokens < 0)
{
    reportBuilder.RecordExcluded(item, score: 0.0, ExclusionReason.NegativeTokens);
}

// After DEDUP stage — track deduplicated exclusions:
if (reportBuilder is not null)
{
    for (var i = 0; i < scored.Length; i++)
    {
        if (!bestByContent.TryGetValue(scored[i].Item.Content, out var bestIdx) || bestIdx != i)
        {
            var keptItem = scored[bestByContent[scored[i].Item.Content]].Item;
            reportBuilder.RecordExcluded(scored[i].Item, scored[i].Score,
                ExclusionReason.Deduplicated, deduplicatedAgainst: keptItem);
        }
    }
}

// After SLICE stage — track budget/quota exclusions:
if (reportBuilder is not null)
{
    for (var i = 0; i < sorted.Length; i++)
    {
        if (!slicedSet.Contains(sorted[i].Item))
        {
            var reason = DetermineSliceExclusionReason(sorted[i], _slicer);
            reportBuilder.RecordExcluded(sorted[i].Item, sorted[i].Score, reason);
        }
    }
}
```

### DryRun() Implementation

```csharp
/// <summary>
/// Executes the pipeline in diagnostic mode, always producing a full report.
/// </summary>
/// <remarks>
/// DryRun() shares the same pipeline logic as <see cref="Execute"/>.
/// Streaming sources should be materialized before calling DryRun(),
/// as IAsyncEnumerable sources may yield different items on re-enumeration.
/// </remarks>
public ContextResult DryRun(IReadOnlyList<ContextItem> items)
{
    ArgumentNullException.ThrowIfNull(items);
    var trace = new DiagnosticTraceCollector(TraceDetailLevel.Item);
    return ExecuteCore(items, trace);
}
```

### Evolved SelectionReport

```csharp
public sealed record SelectionReport
{
    /// <summary>Trace events captured during pipeline execution.</summary>
    public required IReadOnlyList<TraceEvent> Events { get; init; }

    /// <summary>Items included in the final selection.</summary>
    public required IReadOnlyList<IncludedItem> Included { get; init; }

    /// <summary>Items excluded from the final selection, ordered by score descending.</summary>
    public required IReadOnlyList<ExcludedItem> Excluded { get; init; }

    /// <summary>Total number of candidate items before any filtering.</summary>
    public int TotalCandidates { get; init; }

    /// <summary>Total tokens across all candidate items before selection.</summary>
    public int TotalTokensConsidered { get; init; }
}
```

---

## Discretion Recommendations

These are areas the CONTEXT.md left to Claude's discretion.

### 1. DryRun() Reuses Execute() via Extracted Core Method

**Recommendation:** Extract `ExecuteCore(IReadOnlyList<ContextItem> items, ITraceCollector trace)` as a private method. Both `Execute()` and `DryRun()` delegate to it. This avoids code duplication and ensures behavioral parity.

**Confidence:** HIGH — this is the standard refactoring pattern and the codebase already has all logic in one method.

### 2. Report Builder Accumulation Through Stages

**Recommendation:** Create an `internal sealed class ReportBuilder` that lives alongside the pipeline execution. It is created only when `traceCollector is DiagnosticTraceCollector`. Each stage in `ExecuteCore` checks `if (reportBuilder is not null)` before recording — mirroring the existing `if (sw is not null)` pattern for timing.

The ReportBuilder is passed as a local variable through the method, not stored on the pipeline class (pipeline is immutable and reusable).

**Confidence:** HIGH — follows the existing codebase patterns exactly.

### 3. ISlicer Does NOT Need Changes

**Recommendation:** Do not modify `ISlicer`. Exclusion tracking is handled entirely in the pipeline via set-difference operations. The pipeline already computes `slicedSet` (line 215) to diff slicer output against sorted input. Extend this existing pattern.

**Confidence:** HIGH — changing ISlicer would be a public API break and is unnecessary.

### 4. PublicAPI.Unshipped.txt Entries

**Recommendation:** All new types need entries. Key additions:
- `InclusionReason` enum (3 values)
- `ExclusionReason` enum (replace 4 values with 8 values)
- `IncludedItem` sealed record
- `ExcludedItem` sealed record
- `OverflowStrategy` enum (3 values)
- `OverflowEvent` sealed record
- `SelectionReport` new properties (Included, Excluded, TotalCandidates, TotalTokensConsidered)
- `CupelPipeline.DryRun()` method
- `PipelineBuilder.WithOverflowStrategy()` overloads

**Confidence:** HIGH — this follows existing project conventions visible in PublicAPI.Unshipped.txt.

### 5. Exception Type for OverflowStrategy.Throw

**Recommendation:** Use `OverflowException` (BCL type in `System`). It's semantically perfect — "the result of an arithmetic, casting, or conversion operation is an overflow." The token budget overflow maps directly.

Message format:
```
"Token overflow: {tokensOverBudget} tokens over TargetTokens budget ({budget.TargetTokens}). {overflowingItems.Count} items exceed budget after pinned item merging. Configure OverflowStrategy.Truncate or OverflowStrategy.Proceed to handle this automatically."
```

**Confidence:** HIGH — `OverflowException` is the right semantic fit and the CONTEXT.md explicitly mentions "throws OverflowException with details."

### 6. How the Report Builder Accumulates Items

**Recommendation:** The ReportBuilder accumulates items stage-by-stage:

| Stage | What gets recorded |
|-------|--------------------|
| Classify | Excluded: items with `Tokens < 0` → `NegativeTokens`. Candidate stats computed. |
| Score | No exclusions. Scores stored for later use. |
| Deduplicate | Excluded: duplicate items → `Deduplicated` with `DeduplicatedAgainst` reference. |
| Slice | Excluded: items not in slicer output → `BudgetExceeded` or `QuotaCapExceeded`/`QuotaRequireDisplaced` (if QuotaSlice). |
| Overflow | If Truncate: additional excluded items → `BudgetExceeded`. |
| Merge + Place | Included: pinned items → `Pinned`, zero-token items → `ZeroToken`, scored items → `Scored`. |

The final `Excluded` list is sorted by score descending before inclusion in the report.

**Confidence:** HIGH

---

## Open Questions

### 1. ScoredTooLow vs BudgetExceeded Distinction (MEDIUM confidence)

The current slicers (GreedySlice, KnapsackSlice) exclude items because they don't fit in the budget, not because they scored "too low." There's no explicit score threshold. `ScoredTooLow` would only apply if a minimum score threshold is added in the future.

**Recommendation:** For now, all slicer-excluded items get `BudgetExceeded`. Reserve `ScoredTooLow` for a future minimum-score-threshold feature. Include it in the enum now for forward compatibility but don't assign it in this phase.

### 2. Filtered ExclusionReason (LOW confidence)

The CONTEXT.md includes `Filtered` as an exclusion reason, but there's no filter stage in the current pipeline. This appears to be a forward-compatibility placeholder.

**Recommendation:** Include in the enum but don't assign it in this phase. Document it as reserved for future filter stages.

### 3. PinnedOverride ExclusionReason (MEDIUM confidence)

`PinnedOverride` is listed but its semantics are unclear from the current pipeline. Pinned items don't directly displace scored items — they reduce the available budget before slicing. An item excluded because the budget was reduced by pinned items would be `BudgetExceeded`, not `PinnedOverride`.

**Recommendation:** `PinnedOverride` should be used specifically when `OverflowStrategy.Truncate` removes scored items to make room after pinned items cause overflow. This is a post-slice operation distinct from the slicer's own budget-based exclusions.

---

*Phase: 07-explainability-overflow-handling*
*Research completed: 2026-03-13*
