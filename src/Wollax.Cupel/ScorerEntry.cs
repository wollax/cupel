using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Describes a scorer configuration within a <see cref="CupelPolicy"/>.
/// Specifies the scorer type, its relative weight, and any type-specific configuration.
/// Validates all inputs at construction time — no invalid entry can exist at runtime.
/// </summary>
public sealed class ScorerEntry
{
    /// <summary>The built-in scorer algorithm to use.</summary>
    [JsonPropertyName("type")]
    public ScorerType Type { get; }

    /// <summary>Relative weight of this scorer in the composite. Must be positive and finite.</summary>
    [JsonPropertyName("weight")]
    public double Weight { get; }

    /// <summary>
    /// Optional per-<see cref="ContextKind"/> weight overrides for the <see cref="ScorerType.Kind"/> scorer.
    /// When null, the KindScorer uses its built-in defaults.
    /// </summary>
    [JsonPropertyName("kindWeights")]
    public IReadOnlyDictionary<ContextKind, double>? KindWeights { get; }

    /// <summary>
    /// Tag-to-weight mapping required when <see cref="Type"/> is <see cref="ScorerType.Tag"/>.
    /// Must not be null for Tag scorers.
    /// </summary>
    [JsonPropertyName("tagWeights")]
    public IReadOnlyDictionary<string, double>? TagWeights { get; }

    /// <summary>
    /// Creates a new scorer entry with the specified type, weight, and optional type-specific configuration.
    /// </summary>
    /// <param name="type">The built-in scorer algorithm to use.</param>
    /// <param name="weight">Relative weight — must be positive and finite.</param>
    /// <param name="kindWeights">Optional per-kind weight overrides for the Kind scorer.</param>
    /// <param name="tagWeights">Required tag-to-weight mapping when <paramref name="type"/> is <see cref="ScorerType.Tag"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="weight"/> is not positive or not finite.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="type"/> is <see cref="ScorerType.Tag"/> and <paramref name="tagWeights"/> is null.
    /// </exception>
    [JsonConstructor]
    public ScorerEntry(
        ScorerType type,
        double weight,
        IReadOnlyDictionary<ContextKind, double>? kindWeights = null,
        IReadOnlyDictionary<string, double>? tagWeights = null)
    {
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown ScorerType value.");

        if (!double.IsFinite(weight) || weight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(weight),
                weight, "Weight must be positive and finite.");
        }

        if (type == ScorerType.Tag && tagWeights is null)
        {
            throw new ArgumentException(
                "TagWeights must be specified when Type is Tag.", nameof(tagWeights));
        }

        Type = type;
        Weight = weight;
        KindWeights = kindWeights is not null ? new Dictionary<ContextKind, double>(kindWeights) : null;
        TagWeights = tagWeights is not null ? new Dictionary<string, double>(tagWeights) : null;
    }
}
