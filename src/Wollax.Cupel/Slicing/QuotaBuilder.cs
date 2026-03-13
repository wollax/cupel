namespace Wollax.Cupel.Slicing;

/// <summary>
/// Fluent builder for <see cref="QuotaSet"/> with configuration-time validation.
/// Validates all constraints when <see cref="Build"/> is called.
/// </summary>
public sealed class QuotaBuilder
{
    private readonly Dictionary<ContextKind, double> _requires = new();
    private readonly Dictionary<ContextKind, double> _caps = new();

    /// <summary>
    /// Sets the minimum percentage allocation for the specified <paramref name="kind"/>.
    /// If called multiple times for the same kind, the last value wins.
    /// </summary>
    /// <param name="kind">The context kind to constrain.</param>
    /// <param name="minPercent">Minimum percentage (0-100).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minPercent"/> is less than 0 or greater than 100.
    /// </exception>
    public QuotaBuilder Require(ContextKind kind, double minPercent)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentOutOfRangeException.ThrowIfNegative(minPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minPercent, 100);

        _requires[kind] = minPercent;
        return this;
    }

    /// <summary>
    /// Sets the maximum percentage allocation for the specified <paramref name="kind"/>.
    /// If called multiple times for the same kind, the last value wins.
    /// </summary>
    /// <param name="kind">The context kind to constrain.</param>
    /// <param name="maxPercent">Maximum percentage (0-100).</param>
    /// <returns>This builder for fluent chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="maxPercent"/> is less than 0 or greater than 100.
    /// </exception>
    public QuotaBuilder Cap(ContextKind kind, double maxPercent)
    {
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentOutOfRangeException.ThrowIfNegative(maxPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(maxPercent, 100);

        _caps[kind] = maxPercent;
        return this;
    }

    /// <summary>
    /// Validates all constraints and builds an immutable <see cref="QuotaSet"/>.
    /// </summary>
    /// <returns>A validated, immutable <see cref="QuotaSet"/>.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no quotas have been configured.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when Require exceeds Cap for the same kind, or when the sum of all Require
    /// values exceeds 100%.
    /// </exception>
    public QuotaSet Build()
    {
        if (_requires.Count == 0 && _caps.Count == 0)
        {
            throw new InvalidOperationException(
                "At least one Require or Cap must be configured before building a QuotaSet.");
        }

        // Validate Require <= Cap for each Kind that has both
        foreach (var kvp in _requires)
        {
            if (_caps.TryGetValue(kvp.Key, out var capValue) && kvp.Value > capValue)
            {
                throw new ArgumentException(
                    $"Require ({kvp.Value}%) exceeds Cap ({capValue}%) for Kind '{kvp.Key}'.");
            }
        }

        // Validate sum of all Requires <= 100%
        var totalRequired = 0.0;
        foreach (var kvp in _requires)
        {
            totalRequired += kvp.Value;
        }

        if (totalRequired > 100)
        {
            throw new ArgumentException(
                $"Sum of all Require values ({totalRequired}%) exceeds 100%.");
        }

        return new QuotaSet(
            new Dictionary<ContextKind, double>(_requires),
            new Dictionary<ContextKind, double>(_caps));
    }
}
