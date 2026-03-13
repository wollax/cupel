namespace Wollax.Cupel.Scoring;

/// <summary>
/// Wraps any <see cref="IScorer"/> and normalizes its output to [0, 1] via min-max
/// normalization across the candidate set. When all raw scores are identical
/// (degenerate case), returns 0.5.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Score"/> method performs a two-pass computation: first it scans all
/// items to find the min and max raw scores from the inner scorer, then it normalizes
/// the current item's raw score using <c>(raw - min) / (max - min)</c>.
/// </para>
/// <para>
/// Performance: O(N) per call (one inner scorer invocation per item). When the pipeline
/// scores all N items, total cost is O(N^2) inner scorer invocations.
/// </para>
/// </remarks>
public sealed class ScaledScorer : IScorer
{
    private readonly IScorer _inner;

    /// <summary>
    /// Initializes a new <see cref="ScaledScorer"/> wrapping the specified inner scorer.
    /// </summary>
    /// <param name="inner">The scorer whose output will be normalized to [0, 1].</param>
    /// <exception cref="ArgumentNullException"><paramref name="inner"/> is <see langword="null"/>.</exception>
    public ScaledScorer(IScorer inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>
    /// Gets the inner scorer, for use by cycle detection in <c>CompositeScorer</c>.
    /// </summary>
    internal IScorer Inner => _inner;

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        var rawScore = _inner.Score(item, allItems);

        var min = double.MaxValue;
        var max = double.MinValue;

        for (var i = 0; i < allItems.Count; i++)
        {
            var s = _inner.Score(allItems[i], allItems);
            if (s < min) min = s;
            if (s > max) max = s;
        }

        if (max == min)
            return 0.5;

        return (rawScore - min) / (max - min);
    }
}
