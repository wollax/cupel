namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores items by looking up their <see cref="ContextKind"/> in a weight dictionary.
/// Unknown kinds score 0.0.
/// </summary>
public sealed class KindScorer : IScorer
{
    private static readonly Dictionary<ContextKind, double> DefaultWeights = new()
    {
        [ContextKind.SystemPrompt] = 1.0,
        [ContextKind.Memory] = 0.8,
        [ContextKind.ToolOutput] = 0.6,
        [ContextKind.Document] = 0.4,
        [ContextKind.Message] = 0.2
    };

    private readonly IReadOnlyDictionary<ContextKind, double> _weights;

    /// <summary>
    /// Creates a <see cref="KindScorer"/> with default weight mappings.
    /// </summary>
    public KindScorer() : this(DefaultWeights) { }

    /// <summary>
    /// Creates a <see cref="KindScorer"/> with custom weight mappings.
    /// </summary>
    /// <param name="weights">Weight dictionary keyed by <see cref="ContextKind"/>.</param>
    public KindScorer(IReadOnlyDictionary<ContextKind, double> weights)
    {
        _weights = weights;
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        return _weights.TryGetValue(item.Kind, out var weight) ? weight : 0.0;
    }
}
