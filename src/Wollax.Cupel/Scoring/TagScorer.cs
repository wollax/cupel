using System.Collections.Frozen;

namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by summing matched tag weights normalized against total configured weight.
/// Tags not in the weight map are ignored. Empty tags produce 0.0.
/// </summary>
public sealed class TagScorer : IScorer
{
    private readonly FrozenDictionary<string, double> _tagWeights;
    private readonly double _totalWeight;

    /// <summary>
    /// Creates a <see cref="TagScorer"/> with the given tag weight map.
    /// </summary>
    /// <param name="tagWeights">
    /// Tag weight dictionary. Use <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// when constructing the dictionary for case-insensitive matching.
    /// All weights must be non-negative.
    /// </param>
    public TagScorer(IReadOnlyDictionary<string, double> tagWeights)
    {
        ArgumentNullException.ThrowIfNull(tagWeights);

        var total = 0.0;
        // Pre-compute total weight and validate (constructor only, not Score path)
        foreach (var kvp in tagWeights)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(kvp.Value, $"tagWeights[{kvp.Key}]");
            if (!double.IsFinite(kvp.Value))
                throw new ArgumentOutOfRangeException($"tagWeights[{kvp.Key}]", kvp.Value, "Weight must be finite.");
            total += kvp.Value;
        }
        _tagWeights = tagWeights.ToFrozenDictionary(tagWeights is Dictionary<string, double> d
            ? d.Comparer
            : StringComparer.Ordinal);
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

        return Math.Min(matchedSum / _totalWeight, 1.0);
    }
}
