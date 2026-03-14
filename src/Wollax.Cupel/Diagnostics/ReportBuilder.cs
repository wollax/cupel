namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Internal builder that accumulates inclusion/exclusion data during pipeline
/// execution and produces a <see cref="SelectionReport"/>.
/// </summary>
internal sealed class ReportBuilder
{
    private readonly List<IncludedItem> _included = [];
    private readonly List<(ExcludedItem Item, int Index)> _excluded = [];
    private int _totalCandidates;
    private int _totalTokensConsidered;

    /// <summary>Records an item as included in the final selection.</summary>
    public void AddIncluded(ContextItem item, double score, InclusionReason reason)
    {
        _included.Add(new IncludedItem
        {
            Item = item,
            Score = score,
            Reason = reason
        });
    }

    /// <summary>Records an item as excluded from the final selection.</summary>
    public void AddExcluded(
        ContextItem item,
        double score,
        ExclusionReason reason,
        ContextItem? deduplicatedAgainst = null)
    {
        var excludedItem = new ExcludedItem
        {
            Item = item,
            Score = score,
            Reason = reason,
            DeduplicatedAgainst = deduplicatedAgainst
        };
        _excluded.Add((excludedItem, _excluded.Count));
    }

    /// <summary>Sets the total number of candidate items considered.</summary>
    public void SetTotalCandidates(int count) => _totalCandidates = count;

    /// <summary>Sets the total tokens across all candidate items.</summary>
    public void SetTotalTokensConsidered(int tokens) => _totalTokensConsidered = tokens;

    /// <summary>
    /// Builds the final <see cref="SelectionReport"/>.
    /// Excluded items are sorted by score descending with stable index tiebreaking.
    /// </summary>
    public SelectionReport Build(IReadOnlyList<TraceEvent> events)
    {
        // Sort excluded by score descending, stable by insertion order
        var sortedExcluded = _excluded.ToArray();
        Array.Sort(sortedExcluded, static (a, b) =>
        {
            var scoreComparison = b.Item.Score.CompareTo(a.Item.Score);
            return scoreComparison != 0 ? scoreComparison : a.Index.CompareTo(b.Index);
        });

        var excludedItems = new ExcludedItem[sortedExcluded.Length];
        for (var i = 0; i < sortedExcluded.Length; i++)
        {
            excludedItems[i] = sortedExcluded[i].Item;
        }

        return new SelectionReport
        {
            Events = events is TraceEvent[] arr ? arr : [.. events],
            Included = _included.ToArray(),
            Excluded = excludedItems,
            TotalCandidates = _totalCandidates,
            TotalTokensConsidered = _totalTokensConsidered
        };
    }
}
