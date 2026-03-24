namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Detailed report of the selection process.
/// Populated when an <see cref="ITraceCollector"/> with tracing enabled is used.
/// </summary>
public sealed record SelectionReport : IEquatable<SelectionReport>
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

    /// <summary>
    /// Unmet count requirements from <c>CountQuotaSlice</c>, when the candidate pool
    /// could not satisfy a <c>RequireCount</c> constraint at run time.
    /// Empty when no count requirements were configured or all were satisfied.
    /// </summary>
    public IReadOnlyList<CountRequirementShortfall> CountRequirementShortfalls { get; init; } = [];

    /// <inheritdoc />
    public bool Equals(SelectionReport? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return TotalCandidates == other.TotalCandidates
            && TotalTokensConsidered == other.TotalTokensConsidered
            && Events.SequenceEqual(other.Events)
            && Included.SequenceEqual(other.Included)
            && Excluded.SequenceEqual(other.Excluded)
            && CountRequirementShortfalls.SequenceEqual(other.CountRequirementShortfalls);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(TotalCandidates);
        hash.Add(TotalTokensConsidered);
        hash.Add(Events.Count);
        hash.Add(Included.Count);
        hash.Add(Excluded.Count);
        hash.Add(CountRequirementShortfalls.Count);
        return hash.ToHashCode();
    }
}
