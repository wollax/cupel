namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by summing matched tag weights normalized against total configured weight.
/// Tags not in the weight map are ignored. Empty tags produce 0.0.
/// </summary>
public sealed class TagScorer : IScorer
{
    private readonly IReadOnlyDictionary<string, double> _tagWeights;
    private readonly double _totalWeight;

    /// <summary>
    /// Creates a <see cref="TagScorer"/> with the given tag weight map.
    /// </summary>
    /// <param name="tagWeights">
    /// Tag weight dictionary. Use <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// when constructing the dictionary for case-insensitive matching.
    /// </param>
    public TagScorer(IReadOnlyDictionary<string, double> tagWeights)
    {
        _tagWeights = tagWeights;

        var total = 0.0;
        // Pre-compute total weight using for-loop (no LINQ)
        // IReadOnlyDictionary doesn't support indexed access,
        // so we use foreach here (constructor only, not Score path)
        foreach (var kvp in tagWeights)
        {
            total += kvp.Value;
        }
        _totalWeight = total;
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (_totalWeight == 0.0 || item.Tags.Count == 0)
            return 0.0;

        var matchedSum = 0.0;

        for (var i = 0; i < item.Tags.Count; i++)
        {
            if (_tagWeights.TryGetValue(item.Tags[i], out var weight))
            {
                matchedSum += weight;
            }
        }

        return matchedSum / _totalWeight;
    }
}
