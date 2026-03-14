using System.Text.Json.Serialization;

namespace Wollax.Cupel.Slicing;

/// <summary>
/// Describes a quota constraint for a specific <see cref="ContextKind"/> within a <see cref="CupelPolicy"/>.
/// At least one of <see cref="MinPercent"/> or <see cref="MaxPercent"/> must be specified.
/// Validates all inputs at construction time — no invalid entry can exist at runtime.
/// </summary>
public sealed class QuotaEntry
{
    /// <summary>The context kind this quota applies to.</summary>
    [JsonPropertyName("kind")]
    public ContextKind Kind { get; }

    /// <summary>Minimum percentage allocation (0-100) for this kind, or null if unconstrained.</summary>
    [JsonPropertyName("minPercent")]
    public double? MinPercent { get; }

    /// <summary>Maximum percentage allocation (0-100) for this kind, or null if unconstrained.</summary>
    [JsonPropertyName("maxPercent")]
    public double? MaxPercent { get; }

    /// <summary>
    /// Creates a new quota entry with the specified kind and percentage constraints.
    /// </summary>
    /// <param name="kind">The context kind this quota applies to.</param>
    /// <param name="minPercent">Minimum percentage (0-100), or null if unconstrained.</param>
    /// <param name="maxPercent">Maximum percentage (0-100), or null if unconstrained.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when neither <paramref name="minPercent"/> nor <paramref name="maxPercent"/> is specified,
    /// or when <paramref name="minPercent"/> exceeds <paramref name="maxPercent"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minPercent"/> or <paramref name="maxPercent"/> is outside the 0-100 range.
    /// </exception>
    [JsonConstructor]
    public QuotaEntry(ContextKind kind, double? minPercent = null, double? maxPercent = null)
    {
        ArgumentNullException.ThrowIfNull(kind);

        if (minPercent is null && maxPercent is null)
        {
            throw new ArgumentException(
                "At least one of minPercent or maxPercent must be specified.",
                nameof(minPercent));
        }

        if (minPercent is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(minPercent.Value, nameof(minPercent));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(minPercent.Value, 100, nameof(minPercent));
        }

        if (maxPercent is not null)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxPercent.Value, nameof(maxPercent));
            ArgumentOutOfRangeException.ThrowIfGreaterThan(maxPercent.Value, 100, nameof(maxPercent));
        }

        if (minPercent is not null && maxPercent is not null && minPercent.Value > maxPercent.Value)
        {
            throw new ArgumentException(
                $"MinPercent ({minPercent}) cannot exceed MaxPercent ({maxPercent}).",
                nameof(minPercent));
        }

        Kind = kind;
        MinPercent = minPercent;
        MaxPercent = maxPercent;
    }
}
