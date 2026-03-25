using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Slicing;

/// <summary>
/// A slicer that combines count-based quota enforcement with knapsack-optimal selection.
/// </summary>
/// <remarks>
/// <para>
/// <b>Phase 1 — Count-Satisfy:</b> For each entry with <see cref="CountQuotaEntry.RequireCount"/> &gt; 0,
/// the top-N candidates of that kind (sorted by score descending) are committed to the selection.
/// Their token cost is accumulated as <c>preAllocatedTokens</c> and the committed items are removed
/// from the residual candidate pool.
/// </para>
/// <para>
/// <b>Phase 2 — Knapsack Delegate:</b> The inner <see cref="KnapsackSlice"/> receives the residual
/// candidate pool and a reduced budget. Output is sorted by score descending before Phase 3.
/// </para>
/// <para>
/// <b>Phase 3 — Cap Enforcement:</b> Items returned by the knapsack are filtered against the
/// per-kind cap. The <c>selectedCount</c> is seeded from Phase 1 committed counts so that committed
/// items are counted against the cap. Items that would exceed the cap are excluded and a
/// <see cref="ExclusionReason.CountCapExceeded"/> trace event is recorded.
/// </para>
/// </remarks>
public sealed class CountConstrainedKnapsackSlice : ISlicer, IQuotaPolicy
{
    private readonly KnapsackSlice _knapsack;
    private readonly IReadOnlyList<CountQuotaEntry> _entries;
    private readonly ScarcityBehavior _scarcity;

    /// <summary>
    /// Gets the shortfalls recorded during the most recent <see cref="Slice"/> call.
    /// Each entry describes a kind whose candidate pool could not satisfy the configured
    /// <see cref="CountQuotaEntry.RequireCount"/>. Empty when all requirements were met.
    /// </summary>
    public IReadOnlyList<CountRequirementShortfall> LastShortfalls { get; private set; } = [];

    /// <summary>
    /// Gets the per-kind count constraints configured for this slicer.
    /// Exposed internally for pipeline cap-classification logic.
    /// </summary>
    internal IReadOnlyList<CountQuotaEntry> Entries => _entries;

    /// <summary>
    /// Initializes a new instance of the <see cref="CountConstrainedKnapsackSlice"/> class.
    /// </summary>
    /// <param name="entries">Per-kind count constraints.</param>
    /// <param name="knapsack">The <see cref="KnapsackSlice"/> used for Phase 2 selection.</param>
    /// <param name="scarcity">Behavior when candidate pool cannot satisfy a RequireCount constraint.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="entries"/> or <paramref name="knapsack"/> is null.
    /// </exception>
    public CountConstrainedKnapsackSlice(
        IReadOnlyList<CountQuotaEntry> entries,
        KnapsackSlice knapsack,
        ScarcityBehavior scarcity = ScarcityBehavior.Degrade)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(knapsack);

        _entries = entries;
        _knapsack = knapsack;
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

        // Sort each partition by score descending
        foreach (var kvp in byKind)
        {
            kvp.Value.Sort(static (a, b) => b.Score.CompareTo(a.Score));
        }

        // Committed items from Phase 1
        var committed = new List<ContextItem>();
        // selectedCount tracks committed counts per kind (seeded from Phase 1 for Phase 3 cap enforcement — D181)
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

            // Seed selectedCount from Phase 1 committed counts (D181)
            selectedCount[entry.Kind] = satisfied;

            if (satisfied < entry.RequireCount)
            {
                if (_scarcity == ScarcityBehavior.Throw)
                {
                    throw new InvalidOperationException(
                        $"CountConstrainedKnapsackSlice: candidate pool for kind '{entry.Kind}' has {satisfied} items but RequireCount is {entry.RequireCount}.");
                }

                shortfalls.Add(new CountRequirementShortfall(entry.Kind, entry.RequireCount, satisfied));

                if (traceCollector.IsEnabled)
                {
                    traceCollector.RecordItemEvent(new TraceEvent
                    {
                        Stage = PipelineStage.Slice,
                        Duration = TimeSpan.Zero,
                        ItemCount = satisfied,
                        Message = $"CountConstrainedKnapsackSlice: shortfall for kind '{entry.Kind}': required {entry.RequireCount}, satisfied {satisfied}."
                    });
                }
            }
        }

        LastShortfalls = shortfalls;

        // --- Phase 2: Build residual pool and delegate to knapsack ---
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

        IReadOnlyList<ContextItem> innerSelected;
        if (residual.Count > 0 && residualBudget.TargetTokens > 0)
        {
            // Build score lookup for re-sorting Phase 2 output (D180)
            var scoreByContent = new Dictionary<string, double>(residual.Count);
            for (var i = 0; i < residual.Count; i++)
            {
                scoreByContent[residual[i].Item.Content] = residual[i].Score;
            }

            var knapsackResult = _knapsack.Slice(residual, residualBudget, traceCollector);

            // Sort Phase 2 output by score descending before Phase 3 cap loop (D180)
            innerSelected = knapsackResult
                .OrderByDescending(item => scoreByContent.GetValueOrDefault(item.Content, 0.0))
                .ToList();
        }
        else
        {
            innerSelected = [];
        }

        // --- Phase 3: Cap enforcement on knapsack output ---
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
                        Message = $"CountConstrainedKnapsackSlice: item excluded — kind '{kind}' reached cap {entry.CapCount} (ExclusionReason.CountCapExceeded)."
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
