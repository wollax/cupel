namespace Wollax.Cupel.Slicing;

/// <summary>
/// Immutable validated quota configuration holding Require (minimum %) and Cap (maximum %)
/// constraints per <see cref="ContextKind"/>. Created exclusively via <see cref="QuotaBuilder.Build"/>.
/// </summary>
public sealed class QuotaSet
{
    private readonly IReadOnlyDictionary<ContextKind, double> _requires;
    private readonly IReadOnlyDictionary<ContextKind, double> _caps;

    internal QuotaSet(
        IReadOnlyDictionary<ContextKind, double> requires,
        IReadOnlyDictionary<ContextKind, double> caps)
    {
        _requires = requires;
        _caps = caps;
    }

    /// <summary>
    /// Gets the minimum percentage allocation for the specified <paramref name="kind"/>.
    /// Returns 0 if no Require was configured for this kind.
    /// </summary>
    public double GetRequire(ContextKind kind) =>
        _requires.TryGetValue(kind, out var value) ? value : 0;

    /// <summary>
    /// Gets the maximum percentage allocation for the specified <paramref name="kind"/>.
    /// Returns 100 if no Cap was configured for this kind.
    /// </summary>
    public double GetCap(ContextKind kind) =>
        _caps.TryGetValue(kind, out var value) ? value : 100;

    /// <summary>
    /// All distinct <see cref="ContextKind"/> values that have either a Require or Cap configured.
    /// </summary>
    public IReadOnlyCollection<ContextKind> Kinds
    {
        get
        {
            var kinds = new HashSet<ContextKind>();
            foreach (var kvp in _requires)
            {
                kinds.Add(kvp.Key);
            }
            foreach (var kvp in _caps)
            {
                kinds.Add(kvp.Key);
            }
            return kinds;
        }
    }
}
