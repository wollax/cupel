namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by how recently they were timestamped, using a configurable
/// <see cref="DecayCurve"/> to map age to a score in [0.0, 1.0].
/// </summary>
/// <remarks>
/// <para>
/// The current time is provided via a <see cref="System.TimeProvider"/> injection,
/// enabling deterministic testing without real wall-clock access.
/// </para>
/// <para>
/// Items without a timestamp receive <c>nullTimestampScore</c>.
/// Future-dated items (timestamp after now) are clamped to age zero, producing
/// a score of 1.0 for <see cref="DecayCurve.Exponential"/> and
/// <see cref="DecayCurve.Window"/> curves.
/// </para>
/// </remarks>
public sealed class DecayScorer : IScorer
{
    private readonly TimeProvider _timeProvider;
    private readonly DecayCurve _curve;
    private readonly double _nullTimestampScore;

    /// <summary>
    /// Creates a <see cref="DecayScorer"/>.
    /// </summary>
    /// <param name="timeProvider">
    /// Wall-clock provider. Use <see cref="TimeProvider.System"/> in production;
    /// subclass for tests.
    /// </param>
    /// <param name="curve">Decay curve to apply.</param>
    /// <param name="nullTimestampScore">
    /// Score returned for items with no timestamp. Must be in [0.0, 1.0].
    /// Defaults to 0.5.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="nullTimestampScore"/> is outside [0.0, 1.0].
    /// </exception>
    public DecayScorer(TimeProvider timeProvider, DecayCurve curve, double nullTimestampScore = 0.5)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(curve);

        if (nullTimestampScore < 0.0 || nullTimestampScore > 1.0)
            throw new ArgumentOutOfRangeException(nameof(nullTimestampScore), "must be in [0.0, 1.0]");

        _timeProvider = timeProvider;
        _curve = curve;
        _nullTimestampScore = nullTimestampScore;
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (item.Timestamp is null)
            return _nullTimestampScore;

        var rawAge = _timeProvider.GetUtcNow() - item.Timestamp.Value;
        var age = rawAge < TimeSpan.Zero ? TimeSpan.Zero : rawAge;

        return Compute(age);
    }

    private double Compute(TimeSpan age)
    {
        return _curve switch
        {
            DecayCurve.Exponential e =>
                Math.Pow(2.0, -(age.TotalSeconds / e.HalfLife.TotalSeconds)),

            DecayCurve.Window w =>
                age < w.MaxAge ? 1.0 : 0.0,

            DecayCurve.Step s => ComputeStep(s.Windows, age),

            _ => throw new InvalidOperationException($"Unknown curve type: {_curve.GetType().Name}")
        };
    }

    private static double ComputeStep(IReadOnlyList<(TimeSpan MaxAge, double Score)> windows, TimeSpan age)
    {
        for (var i = 0; i < windows.Count; i++)
        {
            if (windows[i].MaxAge > age)
                return windows[i].Score;
        }
        // Fall through to last window's score
        return windows[windows.Count - 1].Score;
    }
}
