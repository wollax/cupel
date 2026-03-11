using System.Collections.Frozen;

namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by looking up their <see cref="ContextKind"/> in a weight dictionary.
/// Unknown kinds score 0.0.
/// </summary>
public sealed class KindScorer : IScorer
{
    private static readonly FrozenDictionary<ContextKind, double> DefaultWeights =
        new Dictionary<ContextKind, double>
        {
            [ContextKind.SystemPrompt] = 1.0,
            [ContextKind.Memory] = 0.8,
            [ContextKind.ToolOutput] = 0.6,
            [ContextKind.Document] = 0.4,
            [ContextKind.Message] = 0.2
        }.ToFrozenDictionary();

    private readonly FrozenDictionary<ContextKind, double> _weights;

    /// <summary>
    /// Creates a <see cref="KindScorer"/> with default weight mappings.
    /// </summary>
    public KindScorer() : this(DefaultWeights) { }

    /// <summary>
    /// Creates a <see cref="KindScorer"/> with custom weight mappings.
    /// </summary>
    /// <param name="weights">
    /// Weight dictionary keyed by <see cref="ContextKind"/>.
    /// All weights must be finite and non-negative.
    /// </param>
    public KindScorer(IReadOnlyDictionary<ContextKind, double> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);

        foreach (var kvp in weights)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(kvp.Value, $"weights[{kvp.Key}]");
            if (!double.IsFinite(kvp.Value))
                throw new ArgumentOutOfRangeException($"weights[{kvp.Key}]", kvp.Value, "Weight must be finite.");
        }

        _weights = weights as FrozenDictionary<ContextKind, double>
            ?? weights.ToFrozenDictionary();
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        return _weights.TryGetValue(item.Kind, out var weight) ? weight : 0.0;
    }
}
