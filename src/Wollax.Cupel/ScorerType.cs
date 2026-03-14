namespace Wollax.Cupel;

/// <summary>
/// Identifies a built-in scorer algorithm used in a <see cref="CupelPolicy"/>.
/// </summary>
public enum ScorerType
{
    /// <summary>Scores items by temporal proximity — recent items rank higher.</summary>
    Recency,

    /// <summary>Scores items by their explicit priority value.</summary>
    Priority,

    /// <summary>Scores items by their <see cref="ContextKind"/> category.</summary>
    Kind,

    /// <summary>Scores items by matching user-defined tag weights.</summary>
    Tag,

    /// <summary>Scores items by how frequently their content appears.</summary>
    Frequency,

    /// <summary>Scores items by their self-reported future relevance hint.</summary>
    Reflexive
}
