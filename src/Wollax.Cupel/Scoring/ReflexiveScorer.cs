namespace Wollax.Cupel.Scoring;

/// <summary>
/// Passes through the item's FutureRelevanceHint, clamped to [0.0, 1.0].
/// Null hints score 0.0. Ignores allItems entirely.
/// </summary>
public sealed class ReflexiveScorer : IScorer
{
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.FutureRelevanceHint.HasValue)
            return 0.0;

        var value = item.FutureRelevanceHint.Value;
        if (!double.IsFinite(value))
            return 0.0;

        return Math.Clamp(value, 0.0, 1.0);
    }
}
