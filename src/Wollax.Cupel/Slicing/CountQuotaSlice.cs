using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Slicing;

/// <summary>
/// Decorator slicer that enforces absolute minimum/maximum item counts per <see cref="ContextKind"/>
/// using a two-phase COUNT-DISTRIBUTE-BUDGET algorithm.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 1 — Count-Satisfy:</b> For each entry with <see cref="CountQuotaEntry.RequireCount"/> &gt; 0,
/// the top-N candidates of that kind (sorted by score descending) are committed to the selection.
/// Their token cost is accumulated as <c>preAllocatedTokens</c> and the committed items are removed
/// from the residual candidate pool.
/// </para>
/// <para>
/// <b>Phase 2 — Delegate:</b> The inner slicer receives the residual candidate pool and a reduced
/// budget (<c>MaxTokens</c> unchanged, <c>TargetTokens</c> reduced by <c>preAllocatedTokens</c>).
/// </para>
/// <para>
/// <b>Phase 3 — Cap Enforcement:</b> Items returned by the inner slicer are filtered against the
/// per-kind cap. Items that would exceed the cap are excluded and a
/// <see cref="ExclusionReason.CountCapExceeded"/> trace event is recorded.
/// </para>
/// </remarks>
public sealed class CountQuotaSlice : ISlicer, IQuotaPolicy
{
    private const string KnapsackGuardMessage =
        "CountQuotaSlice does not support KnapsackSlice as the inner slicer in this version. " +
        "Use GreedySlice as the inner slicer. " +
        "A CountConstrainedKnapsackSlice will be provided in a future release.";

    private readonly ISlicer _innerSlicer;
    private readonly IReadOnlyList<CountQuotaEntry> _entries;
    private readonly ScarcityBehavior _scarcity;

    /// <summary>
    /// Gets the shortfalls recorded during the most recent <see cref="Slice"/> call.
    /// Each entry describes a kind whose candidate pool could not satisfy the configured
    /// <see cref="CountQuotaEntry.RequireCount"/>. Empty when all requirements were met.
    /// </summary>
    /// <remarks>
    /// This property is an inspection surface for unit tests and diagnostics.
    /// It is populated after each call to <see cref="Slice"/> and is not part of the
    /// <see cref="ISlicer"/> contract.
    /// </remarks>
    public IReadOnlyList<CountRequirementShortfall> LastShortfalls { get; private set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CountQuotaSlice"/> class.
    /// </summary>
    /// <param name="innerSlicer">
    /// The inner slicer to delegate Phase 2 item selection to.
    /// Must not be a <see cref="KnapsackSlice"/> instance.
    /// </param>
    /// <param name="entries">Per-kind count constraints.</param>
    /// <param name="scarcity">Behavior when candidate pool cannot satisfy a RequireCount constraint.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="innerSlicer"/> or <paramref name="entries"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="innerSlicer"/> is a <see cref="KnapsackSlice"/> instance.
    /// </exception>
    public CountQuotaSlice(
        ISlicer innerSlicer,
        IReadOnlyList<CountQuotaEntry> entries,
        ScarcityBehavior scarcity = ScarcityBehavior.Degrade)
    {
        ArgumentNullException.ThrowIfNull(innerSlicer);
        ArgumentNullException.ThrowIfNull(entries);

        if (innerSlicer is KnapsackSlice)
            throw new ArgumentException(KnapsackGuardMessage, nameof(innerSlicer));

        _innerSlicer = innerSlicer;
        _entries = entries;
        _scarcity = scarcity;
    }

    /// <inheritdoc />
    public IReadOnlyList<QuotaConstraint> GetConstraints()
    {
        var constraints = new List<QuotaConstraint>(_entries.Count);
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            constraints.Add(new QuotaConstraint(
                entry.Kind,
                QuotaConstraintMode.Count,
                entry.RequireCount,
                entry.CapCount));
        }
        return constraints;
    }

    /// <inheritdoc />
    public IReadOnlyList<ContextItem> Slice(
        IReadOnlyList<ScoredItem> scoredItems,
        ContextBudget budget,
        ITraceCollector traceCollector)
    {
        ArgumentNullException.ThrowIfNull(scoredItems);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(traceCollector);

        if (scoredItems.Count == 0 || budget.TargetTokens <= 0)
        {
            LastShortfalls = [];
            return [];
        }

        // Build fast lookup from Kind -> entry
        var entryByKind = new Dictionary<ContextKind, CountQuotaEntry>(_entries.Count);
        for (var i = 0; i < _entries.Count; i++)
        {
            entryByKind[_entries[i].Kind] = _entries[i];
        }

        // --- Phase 1: Count-Satisfy ---
        // Group candidates by kind
        var byKind = new Dictionary<ContextKind, List<ScoredItem>>();
        for (var i = 0; i < scoredItems.Count; i++)
        {
            var kind = scoredItems[i].Item.Kind;
            if (!byKind.TryGetValue(kind, out var list))
            {
                list = new List<ScoredItem>();
                byKind[kind] = list;
            }
            list.Add(scoredItems[i]);
        }

        // Sort each partition by score descending (candidates arrive pre-sorted per ISlicer contract
        // but we sort per-kind to be safe)
        foreach (var kvp in byKind)
        {
            kvp.Value.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        }

        // Committed items from Phase 1
        var committed = new List<ContextItem>();
        // selectedCount tracks committed counts per kind (used for Phase 3 cap enforcement)
        var selectedCount = new Dictionary<ContextKind, int>();
        var preAllocatedTokens = 0;
        var shortfalls = new List<CountRequirementShortfall>();

        // Items committed in Phase 1 are excluded from the residual pool
        var committedSet = new HashSet<ContextItem>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            if (entry.RequireCount <= 0) continue;

            var satisfied = 0;
            if (byKind.TryGetValue(entry.Kind, out var candidates))
            {
                for (var j = 0; j < candidates.Count && satisfied < entry.RequireCount; j++)
                {
                    var item = candidates[j].Item;
                    committed.Add(item);
                    committedSet.Add(item);
                    preAllocatedTokens += item.Tokens;
                    satisfied++;
                }
            }

            // Initialize selectedCount for this kind
            selectedCount[entry.Kind] = satisfied;

            if (satisfied < entry.RequireCount)
            {
                if (_scarcity == ScarcityBehavior.Throw)
                {
                    throw new InvalidOperationException(
                        $"CountQuotaSlice: candidate pool for kind '{entry.Kind}' has {satisfied} items but RequireCount is {entry.RequireCount}.");
                }

                shortfalls.Add(new CountRequirementShortfall(entry.Kind, entry.RequireCount, satisfied));

                if (traceCollector.IsEnabled)
                {
                    traceCollector.RecordItemEvent(new TraceEvent
                    {
                        Stage = PipelineStage.Slice,
                        Duration = TimeSpan.Zero,
                        ItemCount = satisfied,
                        Message = $"CountQuotaSlice: shortfall for kind '{entry.Kind}': required {entry.RequireCount}, satisfied {satisfied}."
                    });
                }
            }
        }

        LastShortfalls = shortfalls;

        // --- Phase 2: Build residual pool and delegate ---
        var residual = new List<ScoredItem>(scoredItems.Count - committedSet.Count);
        for (var i = 0; i < scoredItems.Count; i++)
        {
            if (!committedSet.Contains(scoredItems[i].Item))
            {
                residual.Add(scoredItems[i]);
            }
        }

        var residualTarget = Math.Max(0, budget.TargetTokens - preAllocatedTokens);
        var residualBudget = new ContextBudget(
            maxTokens: budget.MaxTokens,
            targetTokens: Math.Min(residualTarget, budget.MaxTokens));

        var innerSelected = residual.Count > 0 && residualBudget.TargetTokens > 0
            ? _innerSlicer.Slice(residual, residualBudget, traceCollector)
            : (IReadOnlyList<ContextItem>)[];

        // --- Phase 3: Cap enforcement on inner slicer output ---
        var result = new List<ContextItem>(committed.Count + innerSelected.Count);

        // Add Phase 1 committed items first
        for (var i = 0; i < committed.Count; i++)
        {
            result.Add(committed[i]);
        }

        // Filter Phase 2 results by cap
        for (var i = 0; i < innerSelected.Count; i++)
        {
            var item = innerSelected[i];
            var kind = item.Kind;

            // Get current count for this kind
            selectedCount.TryGetValue(kind, out var count);

            // Check if this kind has a cap
            if (entryByKind.TryGetValue(kind, out var entry) && count >= entry.CapCount)
            {
                // Cap exceeded — exclude this item
                if (traceCollector.IsEnabled)
                {
                    traceCollector.RecordItemEvent(new TraceEvent
                    {
                        Stage = PipelineStage.Slice,
                        Duration = TimeSpan.Zero,
                        ItemCount = 0,
                        Message = $"CountQuotaSlice: item excluded — kind '{kind}' reached cap {entry.CapCount} (ExclusionReason.CountCapExceeded)."
                    });
                }
                continue;
            }

            result.Add(item);
            selectedCount[kind] = count + 1;
        }

        return result;
    }
}
