namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Buffered trace collector that captures events in an in-memory list
/// and optionally invokes a callback on each recorded event.
/// Item-level events are filtered by <see cref="TraceDetailLevel"/>.
/// </summary>
/// <remarks>
/// This type is not thread-safe. Do not share a single instance across
/// concurrent pipeline executions; create one collector per execution.
/// </remarks>
public sealed class DiagnosticTraceCollector : ITraceCollector
{
    private readonly List<TraceEvent> _events = [];
    private readonly Action<TraceEvent>? _callback;
    private readonly TraceDetailLevel _detailLevel;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticTraceCollector"/> class.
    /// </summary>
    /// <param name="detailLevel">
    /// Controls which events are captured. <see cref="TraceDetailLevel.Stage"/> captures
    /// stage-level events only; <see cref="TraceDetailLevel.Item"/> captures both.
    /// </param>
    /// <param name="callback">Optional callback invoked synchronously on each recorded event.</param>
    public DiagnosticTraceCollector(
        TraceDetailLevel detailLevel = TraceDetailLevel.Stage,
        Action<TraceEvent>? callback = null)
    {
        _detailLevel = detailLevel;
        _callback = callback;
    }

    /// <inheritdoc />
    public bool IsEnabled => true;

    /// <summary>Gets the list of recorded trace events in insertion order.</summary>
    public IReadOnlyList<TraceEvent> Events => _events;

    /// <inheritdoc />
    public void RecordStageEvent(TraceEvent traceEvent)
    {
        _events.Add(traceEvent);
        InvokeCallback(traceEvent);
    }

    /// <inheritdoc />
    public void RecordItemEvent(TraceEvent traceEvent)
    {
        if (_detailLevel < TraceDetailLevel.Item)
            return;

        _events.Add(traceEvent);
        InvokeCallback(traceEvent);
    }

    private void InvokeCallback(TraceEvent traceEvent)
    {
        try
        {
            _callback?.Invoke(traceEvent);
        }
        catch
        {
            // Tracing is observational — a throwing callback must never abort the pipeline.
        }
    }
}
