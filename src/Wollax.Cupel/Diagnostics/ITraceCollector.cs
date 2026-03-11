namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Contract for collecting trace events during pipeline execution.
/// Callers should check <see cref="IsEnabled"/> before constructing trace payloads
/// to avoid unnecessary allocations on disabled code paths.
/// </summary>
public interface ITraceCollector
{
    /// <summary>
    /// Gets a value indicating whether trace collection is active.
    /// When <c>false</c>, callers should skip trace event construction entirely.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Records a stage-level trace event (duration, item count).
    /// Always recorded when <see cref="IsEnabled"/> is <c>true</c>.
    /// </summary>
    /// <param name="traceEvent">The stage-level event to record.</param>
    void RecordStageEvent(TraceEvent traceEvent);

    /// <summary>
    /// Records an item-level trace event (individual scores, exclusion reasons).
    /// May be filtered based on the collector's detail level configuration.
    /// </summary>
    /// <param name="traceEvent">The item-level event to record.</param>
    void RecordItemEvent(TraceEvent traceEvent);
}
