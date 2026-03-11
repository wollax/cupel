using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Immutable data model representing a single piece of context in the pipeline.
/// Every scorer, slicer, and placer operates on ContextItem instances.
/// </summary>
public sealed record ContextItem
{
    /// <summary>The textual content of this context item.</summary>
    [JsonPropertyName("content")]
    public required string Content { get; init; }

    /// <summary>The token count for this context item.</summary>
    [JsonPropertyName("tokens")]
    public required int Tokens { get; init; }

    /// <summary>The kind of context item. Defaults to <see cref="ContextKind.Message"/>.</summary>
    [JsonPropertyName("kind")]
    public ContextKind Kind { get; init; } = ContextKind.Message;

    /// <summary>The source of this context item. Defaults to <see cref="ContextSource.Chat"/>.</summary>
    [JsonPropertyName("source")]
    public ContextSource Source { get; init; } = ContextSource.Chat;

    /// <summary>Optional priority override (higher = more important).</summary>
    [JsonPropertyName("priority")]
    public int? Priority { get; init; }

    /// <summary>Descriptive tags for filtering and scoring.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Arbitrary key-value metadata.</summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();

    /// <summary>When this context item was created or observed.</summary>
    [JsonPropertyName("timestamp")]
    public DateTimeOffset? Timestamp { get; init; }

    /// <summary>Hint for future relevance scoring, conventionally 0.0–1.0.</summary>
    [JsonPropertyName("futureRelevanceHint")]
    public double? FutureRelevanceHint { get; init; }

    /// <summary>Whether this item is pinned (always included regardless of scoring).</summary>
    [JsonPropertyName("pinned")]
    public bool Pinned { get; init; }

    /// <summary>Original token count before any summarization or truncation.</summary>
    [JsonPropertyName("originalTokens")]
    public int? OriginalTokens { get; init; }
}
