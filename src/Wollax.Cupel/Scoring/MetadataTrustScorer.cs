using System.Globalization;

namespace Wollax.Cupel.Scoring;

/// <summary>
/// Scores an item by reading the <c>cupel:trust</c> key from its metadata dictionary.
/// Accepts both native <see langword="double"/> values and parseable <see langword="string"/>
/// representations (D059). Unrecognised types, absent keys, parse failures, and non-finite
/// values all fall back to <c>defaultScore</c>. Valid values are clamped to [0.0, 1.0].
/// </summary>
public sealed class MetadataTrustScorer : IScorer
{
    private readonly double _defaultScore;

    /// <summary>
    /// Creates a <see cref="MetadataTrustScorer"/>.
    /// </summary>
    /// <param name="defaultScore">
    /// Score returned when the <c>cupel:trust</c> key is absent, unparseable, or non-finite.
    /// Must be in [0.0, 1.0]. Defaults to 0.5.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="defaultScore"/> is outside [0.0, 1.0].
    /// </exception>
    public MetadataTrustScorer(double defaultScore = 0.5)
    {
        if (defaultScore < 0.0 || defaultScore > 1.0)
            throw new ArgumentOutOfRangeException(nameof(defaultScore), defaultScore, "defaultScore must be in [0.0, 1.0]");

        _defaultScore = defaultScore;
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.Metadata.TryGetValue("cupel:trust", out var raw))
            return _defaultScore;

        double value;

        // D059: check double before string so native double callers get their value directly.
        if (raw is double d)
        {
            value = d;
        }
        else if (raw is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
        }
        else
        {
            return _defaultScore;
        }

        if (!double.IsFinite(value))
            return _defaultScore;

        return Math.Clamp(value, 0.0, 1.0);
    }
}
