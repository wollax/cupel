namespace Wollax.Cupel.Scoring;

/// <summary>
/// Returns a configurable multiplier (<c>boost</c>) when a metadata key matches a configured
/// value, and <c>1.0</c> (the neutral multiplier) otherwise.
/// </summary>
/// <remarks>
/// <para>
/// <b>Multiplicative semantics:</b> Unlike <see cref="MetadataTrustScorer"/> (which returns an
/// absolute score), <c>MetadataKeyScorer</c> returns a multiplier intended for use in a
/// <see cref="CompositeScorer"/>. A matching item returns <c>boost</c> (e.g. <c>1.5</c>);
/// a non-matching item returns <c>1.0</c>. Returned values are NOT clamped to [0.0, 1.0].
/// </para>
/// <para>
/// Value comparison is string-to-string. Metadata values stored as non-string types are
/// converted via <c>ToString()</c> before comparison.
/// </para>
/// </remarks>
public sealed class MetadataKeyScorer : IScorer
{
    private readonly string _key;
    private readonly string _value;
    private readonly double _boost;

    /// <summary>
    /// Creates a <see cref="MetadataKeyScorer"/> that returns <paramref name="boost"/> for items
    /// where <c>metadata[key] == value</c>, and <c>1.0</c> for all other items.
    /// </summary>
    /// <param name="key">The metadata key to match.</param>
    /// <param name="value">The exact value to match (string comparison).</param>
    /// <param name="boost">
    /// The multiplier returned for matching items. Must be a finite value greater than 0.0.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="boost"/> is zero, negative, or non-finite (NaN or infinity).
    /// </exception>
    public MetadataKeyScorer(string key, string value, double boost)
    {
        if (!double.IsFinite(boost) || boost <= 0.0)
            throw new ArgumentException("boost must be a finite value greater than 0.0", nameof(boost));

        _key = key;
        _value = value;
        _boost = boost;
    }

    /// <inheritdoc />
    public double Score(ContextItem item, IReadOnlyList<ContextItem> allItems)
    {
        if (!item.Metadata.TryGetValue(_key, out var raw))
            return 1.0;

        var rawString = raw is string s ? s : raw?.ToString();
        return rawString == _value ? _boost : 1.0;
    }
}
