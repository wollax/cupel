using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Identifies a built-in scorer algorithm used in a <see cref="CupelPolicy"/>.
/// </summary>
public enum ScorerType
{
    /// <summary>Scores items by temporal proximity — recent items rank higher.</summary>
    [JsonStringEnumMemberName("recency")]
    Recency,

    /// <summary>Scores items by their explicit priority value.</summary>
    [JsonStringEnumMemberName("priority")]
    Priority,

    /// <summary>Scores items by their <see cref="ContextKind"/> category.</summary>
    [JsonStringEnumMemberName("kind")]
    Kind,

    /// <summary>Scores items by matching user-defined tag weights.</summary>
    [JsonStringEnumMemberName("tag")]
    Tag,

    /// <summary>Scores items by how frequently their content appears.</summary>
    [JsonStringEnumMemberName("frequency")]
    Frequency,

    /// <summary>Scores items by their self-reported future relevance hint.</summary>
    [JsonStringEnumMemberName("reflexive")]
    Reflexive
}
