namespace Wollax.Cupel;

/// <summary>
/// Pairs a context item with its computed relevance score.
/// </summary>
/// <param name="Item">The context item.</param>
/// <param name="Score">The computed relevance score, conventionally 0.0–1.0.</param>
public readonly record struct ScoredItem(ContextItem Item, double Score);
