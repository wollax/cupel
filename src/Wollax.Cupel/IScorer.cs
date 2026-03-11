namespace Wollax.Cupel;

/// <summary>
/// Assigns a relevance score to a context item.
/// Output is conventionally 0.0–1.0 (documented, not enforced by type).
/// Scorers rank items — they do not eliminate or select.
/// </summary>
public interface IScorer
{
    /// <summary>
    /// Scores a single item in the context of the full candidate set.
    /// </summary>
    /// <param name="item">The item to score.</param>
    /// <param name="allItems">The complete candidate set, enabling relative scoring.</param>
    /// <returns>A relevance score, conventionally 0.0–1.0.</returns>
    double Score(ContextItem item, IReadOnlyList<ContextItem> allItems);
}
