using System.Diagnostics.CodeAnalysis;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel;

/// <summary>
/// Provides opinionated preset policies for common LLM context management scenarios.
/// Each preset configures scorers, slicer, and placer strategies tuned for a specific use case.
/// </summary>
public static class CupelPresets
{
    /// <summary>
    /// Creates a policy optimized for conversational chat scenarios.
    /// Prioritizes recent messages with secondary kind-based scoring.
    /// Uses greedy selection and chronological placement.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for chat workloads.</returns>
    [Experimental("CUPEL001")]
    public static CupelPolicy Chat() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Recency, weight: 3),
                new ScorerEntry(ScorerType.Kind, weight: 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            name: "Chat",
            description: "Conversational chat — favors recent messages with kind-based secondary scoring.");

    /// <summary>
    /// Creates a policy optimized for code review scenarios.
    /// Balances kind and priority scoring with secondary recency.
    /// Uses greedy selection and chronological placement.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for code review workloads.</returns>
    [Experimental("CUPEL002")]
    public static CupelPolicy CodeReview() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Kind, weight: 2),
                new ScorerEntry(ScorerType.Priority, weight: 2),
                new ScorerEntry(ScorerType.Recency, weight: 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            name: "CodeReview",
            description: "Code review — balances kind and priority with secondary recency.");

    /// <summary>
    /// Creates a policy optimized for retrieval-augmented generation (RAG) scenarios.
    /// Heavily weights reflexive (self-reported relevance) scoring with secondary kind scoring.
    /// Uses greedy selection and U-shaped placement for attention optimization.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for RAG workloads.</returns>
    [Experimental("CUPEL003")]
    public static CupelPolicy Rag() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Reflexive, weight: 3),
                new ScorerEntry(ScorerType.Kind, weight: 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.UShaped,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            name: "Rag",
            description: "Retrieval-augmented generation — reflexive relevance with U-shaped placement.");

    /// <summary>
    /// Creates a policy optimized for document question-answering scenarios.
    /// Balances kind and reflexive scoring with secondary priority.
    /// Uses knapsack selection for optimal budget fitting and U-shaped placement.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for document QA workloads.</returns>
    [Experimental("CUPEL004")]
    public static CupelPolicy DocumentQa() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Kind, weight: 2),
                new ScorerEntry(ScorerType.Reflexive, weight: 2),
                new ScorerEntry(ScorerType.Priority, weight: 1),
            ],
            slicerType: SlicerType.Knapsack,
            placerType: PlacerType.UShaped,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            knapsackBucketSize: 100,
            name: "DocumentQa",
            description: "Document QA — kind and reflexive scoring with knapsack selection and U-shaped placement.");

    /// <summary>
    /// Creates a policy optimized for tool-use scenarios.
    /// Balances kind and recency scoring with secondary priority.
    /// Uses greedy selection and chronological placement.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for tool-use workloads.</returns>
    [Experimental("CUPEL005")]
    public static CupelPolicy ToolUse() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Kind, weight: 2),
                new ScorerEntry(ScorerType.Recency, weight: 2),
                new ScorerEntry(ScorerType.Priority, weight: 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            name: "ToolUse",
            description: "Tool use — kind and recency with secondary priority.");

    /// <summary>
    /// Creates a policy optimized for long-running conversation scenarios.
    /// Heavily weights recency with secondary frequency and kind scoring.
    /// Uses greedy selection and chronological placement.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for long-running workloads.</returns>
    [Experimental("CUPEL006")]
    public static CupelPolicy LongRunning() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Recency, weight: 3),
                new ScorerEntry(ScorerType.Frequency, weight: 1),
                new ScorerEntry(ScorerType.Kind, weight: 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            name: "LongRunning",
            description: "Long-running conversations — recency-dominant with frequency and kind.");

    /// <summary>
    /// Creates a policy optimized for debugging scenarios.
    /// Heavily weights priority with secondary kind and recency scoring.
    /// Uses greedy selection and chronological placement.
    /// </summary>
    /// <returns>A <see cref="CupelPolicy"/> configured for debugging workloads.</returns>
    [Experimental("CUPEL007")]
    public static CupelPolicy Debugging() =>
        new(
            scorers: [
                new ScorerEntry(ScorerType.Priority, weight: 3),
                new ScorerEntry(ScorerType.Kind, weight: 2),
                new ScorerEntry(ScorerType.Recency, weight: 1),
            ],
            slicerType: SlicerType.Greedy,
            placerType: PlacerType.Chronological,
            deduplicationEnabled: true,
            overflowStrategy: OverflowStrategy.Throw,
            name: "Debugging",
            description: "Debugging — priority-dominant with kind and recency.");
}
