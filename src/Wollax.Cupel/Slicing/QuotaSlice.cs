using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Slicing;

/// <summary>
/// Decorator slicer that partitions candidates by <see cref="ContextKind"/> and enforces
/// percentage-based quota constraints (Require minimum, Cap maximum) per kind before
/// delegating to an inner <see cref="ISlicer"/>.
/// </summary>
/// <remarks>
/// <para>
/// Without quotas, a high-scoring kind can dominate the budget. QuotaSlice ensures
/// minimum representation (<see cref="QuotaSet.GetRequire"/>) and prevents
/// over-representation (<see cref="QuotaSet.GetCap"/>) per kind.
/// </para>
/// <para>
/// Pinned items are excluded before slicing in the pipeline — they do not count
/// against quotas.
/// </para>
/// </remarks>
public sealed class QuotaSlice : ISlicer
{
    private readonly ISlicer _innerSlicer;

    /// <summary>
    /// Gets the quota configuration used by this slicer.
    /// Exposed for pipeline-level conflict detection (e.g., pinned items exceeding a Cap).
    /// </summary>
    public QuotaSet Quotas { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="QuotaSlice"/> class.
    /// </summary>
    /// <param name="innerSlicer">The inner slicer to delegate per-kind slicing to.</param>
    /// <param name="quotas">The validated quota configuration.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="innerSlicer"/> or <paramref name="quotas"/> is null.
    /// </exception>
    public QuotaSlice(ISlicer innerSlicer, QuotaSet quotas)
    {
        ArgumentNullException.ThrowIfNull(innerSlicer);
        ArgumentNullException.ThrowIfNull(quotas);
        _innerSlicer = innerSlicer;
        Quotas = quotas;
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
            return [];
        }

        // 1. Partition candidates by ContextKind
        var partitions = new Dictionary<ContextKind, List<ScoredItem>>();
        for (var i = 0; i < scoredItems.Count; i++)
        {
            var kind = scoredItems[i].Item.Kind;
            if (!partitions.TryGetValue(kind, out var list))
            {
                list = new List<ScoredItem>();
                partitions[kind] = list;
            }
            list.Add(scoredItems[i]);
        }

        // 2. Calculate candidate token mass per kind
        var candidateTokenMass = new Dictionary<ContextKind, long>();
        foreach (var kvp in partitions)
        {
            long mass = 0;
            for (var i = 0; i < kvp.Value.Count; i++)
            {
                mass += kvp.Value[i].Item.Tokens;
            }
            candidateTokenMass[kvp.Key] = mass;
        }

        // 3. Calculate per-kind budgets
        var targetTokens = budget.TargetTokens;
        var kindBudgets = new Dictionary<ContextKind, int>();

        // 3a. Compute require and cap tokens for configured kinds
        var requireTokens = new Dictionary<ContextKind, int>();
        var capTokens = new Dictionary<ContextKind, int>();
        var configuredKinds = Quotas.Kinds;

        foreach (var kind in configuredKinds)
        {
            requireTokens[kind] = (int)(Quotas.GetRequire(kind) / 100.0 * targetTokens);
            capTokens[kind] = (int)(Quotas.GetCap(kind) / 100.0 * targetTokens);
        }

        // 3b. Total required tokens
        var totalRequired = 0;
        foreach (var kvp in requireTokens)
        {
            totalRequired += kvp.Value;
        }

        // 3c. Unassigned budget after all requires (floor at 0)
        var unassignedBudget = Math.Max(0, targetTokens - totalRequired);

        // 3d. Compute total candidate token mass for proportional distribution
        // Include all kinds that can receive more budget (not at cap)
        long totalMassForDistribution = 0;
        foreach (var kvp in partitions)
        {
            var kind = kvp.Key;
            var cap = capTokens.TryGetValue(kind, out var c) ? c : targetTokens;
            var require = requireTokens.TryGetValue(kind, out var r) ? r : 0;
            // Only distribute to kinds that have room above their require
            if (cap > require)
            {
                totalMassForDistribution += candidateTokenMass[kind];
            }
        }

        // 3e. Distribute unassigned budget proportionally
        foreach (var kvp in partitions)
        {
            var kind = kvp.Key;
            var require = requireTokens.TryGetValue(kind, out var r) ? r : 0;
            var cap = capTokens.TryGetValue(kind, out var c) ? c : targetTokens;

            var proportional = 0;
            if (totalMassForDistribution > 0 && cap > require)
            {
                proportional = (int)((long)unassignedBudget * candidateTokenMass[kind] / totalMassForDistribution);
            }

            var kindBudget = require + proportional;

            // Clamp to cap
            if (kindBudget > cap)
            {
                kindBudget = cap;
            }

            kindBudgets[kind] = kindBudget;
        }

        // 4. Per-kind slicing
        var allSelected = new List<ContextItem>();

        foreach (var kvp in partitions)
        {
            var kind = kvp.Key;
            var kindPartition = kvp.Value;
            var kindBudget = kindBudgets.TryGetValue(kind, out var kb) ? kb : 0;

            if (kindBudget <= 0)
            {
                continue;
            }

            var cap = capTokens.TryGetValue(kind, out var c) ? c : targetTokens;
            var subBudget = new ContextBudget(
                maxTokens: cap,
                targetTokens: kindBudget);

            var selected = _innerSlicer.Slice(kindPartition, subBudget, traceCollector);

            for (var i = 0; i < selected.Count; i++)
            {
                allSelected.Add(selected[i]);
            }

            // 5. Check for insufficient items for required kinds
            if (traceCollector.IsEnabled)
            {
                var require = requireTokens.TryGetValue(kind, out var r) ? r : 0;
                if (require > 0)
                {
                    var selectedTokens = 0;
                    for (var i = 0; i < selected.Count; i++)
                    {
                        selectedTokens += selected[i].Tokens;
                    }

                    if (selectedTokens < require)
                    {
                        traceCollector.RecordItemEvent(new TraceEvent
                        {
                            Stage = PipelineStage.Slice,
                            Duration = TimeSpan.Zero,
                            ItemCount = selected.Count,
                            Message = $"WARNING: Kind '{kind}' selected {selectedTokens} tokens, below the required {require} tokens. Insufficient items for quota."
                        });
                    }
                }
            }
        }

        return allSelected;
    }
}
