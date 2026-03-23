namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Structured snapshot of a single pipeline stage's execution, suitable for
/// post-run bridge emission (e.g., OpenTelemetry Activity spans).
/// Captures stage identity, item counts in/out, and precise timing.
/// </summary>
public readonly record struct StageTraceSnapshot
{
    /// <summary>The pipeline stage this snapshot describes.</summary>
    public required PipelineStage Stage { get; init; }

    /// <summary>Number of items entering the stage.</summary>
    public required int ItemCountIn { get; init; }

    /// <summary>Number of items leaving the stage.</summary>
    public required int ItemCountOut { get; init; }

    /// <summary>Wall-clock duration of the stage.</summary>
    public required TimeSpan Duration { get; init; }
}
