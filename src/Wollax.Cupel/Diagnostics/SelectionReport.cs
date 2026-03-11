namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Detailed report of the selection process.
/// Populated when a <see cref="DiagnosticTraceCollector"/> is used.
/// </summary>
public sealed record SelectionReport
{
    /// <summary>Trace events captured during pipeline execution.</summary>
    public required IReadOnlyList<TraceEvent> Events { get; init; }
}
