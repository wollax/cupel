namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Detailed report of the selection process.
/// Populated when a <see cref="DiagnosticTraceCollector"/> is used.
/// </summary>
public sealed record SelectionReport
{
    /// <summary>Trace events captured during pipeline execution.</summary>
    public required IReadOnlyList<TraceEvent> Events { get; init; }

    /// <summary>Items that were included in the final selection.</summary>
    public required IReadOnlyList<IncludedItem> Included { get; init; }

    /// <summary>Items that were excluded from the final selection, ordered by score descending.</summary>
    public required IReadOnlyList<ExcludedItem> Excluded { get; init; }

    /// <summary>Total number of candidate items considered by the pipeline.</summary>
    public required int TotalCandidates { get; init; }

    /// <summary>Total tokens across all candidate items considered.</summary>
    public required int TotalTokensConsidered { get; init; }
}
