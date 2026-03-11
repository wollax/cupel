using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// The result of a context pipeline execution.
/// Contains the selected items and an optional diagnostic report.
/// </summary>
public sealed record ContextResult
{
    /// <summary>The context items selected and ordered by the pipeline.</summary>
    public required IReadOnlyList<ContextItem> Items { get; init; }

    /// <summary>
    /// Total token count across all selected items.
    /// Computed without LINQ to avoid delegate allocations.
    /// </summary>
    public int TotalTokens
    {
        get
        {
            var total = 0;
            for (var i = 0; i < Items.Count; i++)
                total += Items[i].Tokens;
            return total;
        }
    }

    /// <summary>
    /// Diagnostic report of the selection process.
    /// Null when tracing is disabled (i.e., <see cref="NullTraceCollector"/> was used).
    /// </summary>
    public SelectionReport? Report { get; init; }
}
