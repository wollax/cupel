namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Detailed report of the selection process.
/// Populated when a <see cref="DiagnosticTraceCollector"/> is used.
/// </summary>
/// <remarks>
/// Phase 7 expands this type with detailed per-item inclusion/exclusion data
/// and <see cref="ExclusionReason"/> annotations.
/// </remarks>
public sealed record SelectionReport
{
    /// <summary>Trace events captured during pipeline execution.</summary>
    public required IReadOnlyList<TraceEvent> Events { get; init; }
}
