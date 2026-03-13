namespace Wollax.Cupel.Scoring;

/// <summary>
/// Combines multiple <see cref="IScorer"/> instances via weighted average.
/// Weights are relative and pre-normalized at construction time.
/// Cycle detection via DFS with <see cref="ReferenceEqualityComparer"/> runs at construction.
/// </summary>
public sealed class CompositeScorer : IScorer
{
    private readonly IScorer[] _scorers;
    private readonly double[] _normalizedWeights;

    /// <summary>
    /// Creates a composite scorer from weighted scorer entries.
    /// </summary>
    /// <param name="entries">
    /// One or more (scorer, weight) pairs. Weights are relative — (2, 1) produces
    /// identical results to (0.6, 0.3). All weights must be positive and finite.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="entries"/> is null, or any entry has a null scorer.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="entries"/> is empty, or a cycle is detected in the scorer graph.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Any entry has a zero, negative, infinite, or NaN weight.
    /// </exception>
    public CompositeScorer(IReadOnlyList<(IScorer Scorer, double Weight)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        if (entries.Count == 0)
            throw new ArgumentException("At least one scorer entry is required.", nameof(entries));

        var scorers = new IScorer[entries.Count];
        var weights = new double[entries.Count];
        var totalWeight = 0.0;

        for (var i = 0; i < entries.Count; i++)
        {
            var (scorer, weight) = entries[i];
            ArgumentNullException.ThrowIfNull(scorer, $"entries[{i}].Scorer");
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(weight, $"entries[{i}].Weight");

            if (!double.IsFinite(weight))
                throw new ArgumentOutOfRangeException($"entries[{i}].Weight", weight, "Weight must be finite.");

            scorers[i] = scorer;
            weights[i] = weight;
            totalWeight += weight;
        }

        // Normalize weights so they sum to 1.0
        for (var i = 0; i < weights.Length; i++)
            weights[i] /= totalWeight;

        // Cycle detection: DFS traversal of the scorer DAG
        DetectCycles(scorers);

        _scorers = scorers;
        _normalizedWeights = weights;
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        var result = 0.0;
        for (var i = 0; i < _scorers.Length; i++)
            result += _scorers[i].Score(item, allItems) * _normalizedWeights[i];
        return result;
    }

    private static void DetectCycles(IScorer[] children)
    {
        var visited = new HashSet<IScorer>(ReferenceEqualityComparer.Instance);
        var inPath = new HashSet<IScorer>(ReferenceEqualityComparer.Instance);

        for (var i = 0; i < children.Length; i++)
            DetectCyclesCore(children[i], visited, inPath);
    }

    private static void DetectCyclesCore(
        IScorer node,
        HashSet<IScorer> visited,
        HashSet<IScorer> inPath)
    {
        // Two-set DFS: 'visited' tracks fully-explored nodes to avoid re-traversal;
        // 'inPath' tracks the current recursion stack to detect back-edges (cycles).
        if (!visited.Add(node))
        {
            if (inPath.Contains(node))
                throw new ArgumentException(
                    $"Cycle detected: scorer '{node.GetType().Name}' appears in its own dependency graph.",
                    "entries");
            return;
        }

        inPath.Add(node);

        switch (node)
        {
            case CompositeScorer composite:
                for (var i = 0; i < composite._scorers.Length; i++)
                    DetectCyclesCore(composite._scorers[i], visited, inPath);
                break;
            case ScaledScorer scaled:
                DetectCyclesCore(scaled.Inner, visited, inPath);
                break;
        }

        inPath.Remove(node);
    }
}
