using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Determines the final ordering of selected context items.
/// Placers position items — they do not filter or re-score.
/// </summary>
public interface IPlacer
{
    /// <summary>
    /// Orders the selected items for optimal placement in the context window.
    /// </summary>
    /// <param name="items">The items selected by the slicer, with their scores.</param>
    /// <param name="traceCollector">Trace collector for diagnostic events.</param>
    /// <returns>The items in their final order.</returns>
    IReadOnlyList<ContextItem> Place(
        IReadOnlyList<ScoredItem> items,
        ITraceCollector traceCollector);
}
