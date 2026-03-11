namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// A single trace event captured during pipeline execution.
/// Readonly record struct for zero-allocation construction on the stack.
/// </summary>
public readonly record struct TraceEvent
{
    /// <summary>The pipeline stage that produced this event.</summary>
    public required PipelineStage Stage { get; init; }

    /// <summary>Wall-clock duration of the stage.</summary>
    /// <remarks>
    /// For item-level events this value is <see cref="TimeSpan.Zero"/> because
    /// individual item processing is not independently timed.
    /// </remarks>
    public required TimeSpan Duration { get; init; }

    /// <summary>Number of items processed in this stage.</summary>
    public required int ItemCount { get; init; }
}
