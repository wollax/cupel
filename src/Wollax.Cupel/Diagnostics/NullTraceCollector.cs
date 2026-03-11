namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Zero-cost no-op trace collector. Returns <c>false</c> for <see cref="IsEnabled"/>,
/// enabling callers to skip trace payload construction entirely.
/// Use <see cref="Instance"/> — the constructor is private.
/// </summary>
public sealed class NullTraceCollector : ITraceCollector
{
    /// <summary>Singleton instance of the null trace collector.</summary>
    public static readonly NullTraceCollector Instance = new();

    private NullTraceCollector() { }

    /// <inheritdoc />
    public bool IsEnabled => false;

    /// <inheritdoc />
    public void RecordStageEvent(TraceEvent traceEvent) { }

    /// <inheritdoc />
    public void RecordItemEvent(TraceEvent traceEvent) { }
}
