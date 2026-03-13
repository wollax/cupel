namespace Wollax.Cupel.Scoring;

/// <summary>
/// Wraps any <see cref="IScorer"/> and normalizes its output to [0, 1] via min-max
/// normalization across the candidate set. When all raw scores are identical
/// (degenerate case), returns 0.5.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Score"/> method scans all items to find the min and max raw scores
/// from the inner scorer, capturing the target item's raw score during the same pass,
/// then normalizes using <c>(raw - min) / (max - min)</c>.
/// </para>
/// <para>
/// Performance: O(N) per call (one inner scorer invocation per item in <paramref name="allItems"/>).
/// When the pipeline scores all N items, total cost is O(N²) inner scorer invocations.
/// The target item must be present in <paramref name="allItems"/>.
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

    // Exposed for CompositeScorer cycle detection traversal.
    internal IScorer Inner => _inner;

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (allItems.Count == 0)
            return 0.5;

        var rawScore = double.NaN;
        var min = double.PositiveInfinity;
        var max = double.NegativeInfinity;

        for (var i = 0; i < allItems.Count; i++)
        {
            var s = _inner.Score(allItems[i], allItems);
            if (s < min) min = s;
            if (s > max) max = s;
            if (ReferenceEquals(allItems[i], item))
                rawScore = s;
        }

        if (max == min)
            return 0.5;

        return (rawScore - min) / (max - min);
    }
}
