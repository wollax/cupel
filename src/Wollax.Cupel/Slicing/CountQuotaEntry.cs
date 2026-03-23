namespace Wollax.Cupel.Slicing;

/// <summary>
/// Per-kind count constraint for <see cref="CountQuotaSlice"/>.
/// Specifies an absolute minimum (<see cref="RequireCount"/>) and maximum (<see cref="CapCount"/>)
/// number of items of a given <see cref="ContextKind"/> to include.
/// </summary>
public sealed class CountQuotaEntry
{
    /// <summary>Gets the <see cref="ContextKind"/> this entry constrains.</summary>
    public ContextKind Kind { get; }

    /// <summary>Gets the minimum number of items of this kind that must be included.</summary>
    public int RequireCount { get; }

    /// <summary>Gets the maximum number of items of this kind that may be included.</summary>
    public int CapCount { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CountQuotaEntry"/> class.
    /// </summary>
    /// <param name="kind">The <see cref="ContextKind"/> to constrain.</param>
    /// <param name="requireCount">Minimum item count. Must be ≥ 0.</param>
    /// <param name="capCount">Maximum item count. Must be > 0 and ≥ <paramref name="requireCount"/>.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="requireCount"/> is negative, <paramref name="capCount"/> is non-positive,
    /// or <paramref name="requireCount"/> exceeds <paramref name="capCount"/>.
    /// </exception>
    public CountQuotaEntry(ContextKind kind, int requireCount, int capCount)
    {
        if (requireCount < 0)
            throw new ArgumentException("RequireCount must be >= 0.", nameof(requireCount));
        if (capCount <= 0)
            throw new ArgumentException("CapCount must be > 0.", nameof(capCount));
        if (requireCount > capCount)
            throw new ArgumentException(
                $"RequireCount ({requireCount}) cannot exceed CapCount ({capCount}).",
                nameof(requireCount));

        Kind = kind;
        RequireCount = requireCount;
        CapCount = capCount;
    }
}
