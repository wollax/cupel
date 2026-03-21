using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Immutable data model representing a single piece of context in the pipeline.
/// Every scorer, slicer, and placer operates on ContextItem instances.
/// </summary>
public sealed record ContextItem
{
    /// <summary>
    /// The full textual content of this context item. Must not be null.
    /// Used directly by deduplication (content-based) and by scorers that inspect text.
    /// </summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>
    /// Token count for this item, measured in the target model's tokenization unit.
    /// Must be zero or positive — items with negative tokens are excluded by the pipeline
    /// before scoring. Zero-token items are always included when budget permits.
    /// </summary>
    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    /// <summary>
    /// Semantic category of this context item. Controls kind-based scoring weights and
    /// quota allocation. Defaults to <see cref="ContextKind.Message"/>.
    /// </summary>
    [JsonPropertyName("kind")]
    public ContextKind Kind { get; init; } = ContextKind.Message;

    /// <summary>
    /// Origin source of this context item — e.g. chat history, retrieval, or tool output.
    /// Used by source-aware scorers. Defaults to <see cref="ContextSource.Chat"/>.
    /// </summary>
    [JsonPropertyName("source")]
    public ContextSource Source { get; init; } = ContextSource.Chat;

    /// <summary>
    /// Optional explicit priority for this item (higher value = higher importance).
    /// When set, the <see cref="Scoring.PriorityScorer"/> uses this value directly.
    /// When null, the PriorityScorer treats the item as having no declared priority.
    /// </summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; init; }

    /// <summary>
    /// Descriptive tags attached to this item. Used by the <see cref="Scoring.TagScorer"/>
    /// to apply per-tag weight boosts. Empty list is valid and means no tag scoring applies.
    /// </summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>
    /// Arbitrary key-value metadata for application-defined use.
    /// Not used by any built-in scorer or slicer. Safe to leave empty.
    /// </summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();

    /// <summary>
    /// Wall-clock time when this item was created, observed, or last updated.
    /// Used by the <see cref="Scoring.RecencyScorer"/> to rank recent items higher.
    /// When null, the RecencyScorer treats this item as having no temporal signal.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>
    /// Self-reported hint for future-relevance scoring, conventionally in the range [0.0, 1.0].
    /// Higher values indicate the item is more likely to be relevant in future turns.
    /// Used by the <see cref="Scoring.ReflexiveScorer"/>. When null, no reflexive score is applied.
    /// </summary>
    [JsonPropertyName("futureRelevanceHint")]
    public double? FutureRelevanceHint { get; init; }

    /// <summary>
    /// When true, this item is always included in the output regardless of its score or budget.
    /// Pinned items are never passed through the slicer — they are merged after slicing.
    /// Pinned items override quota caps if their combined tokens exceed a quota limit.
    /// </summary>
    [JsonPropertyName("pinned")]
    public bool Pinned { get; init; }

    /// <summary>
    /// Token count of this item before any summarization or truncation was applied.
    /// When set, callers can compare <see cref="Tokens"/> to <c>OriginalTokens</c> to detect
    /// lossy compression. When null, no compression has been recorded. Must be zero or positive.
    /// </summary>
    [JsonPropertyName("originalTokens")]
    public int? OriginalTokens { get; init; }
}
