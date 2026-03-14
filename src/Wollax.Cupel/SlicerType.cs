namespace Wollax.Cupel;

/// <summary>
/// Identifies a built-in slicer strategy used in a <see cref="CupelPolicy"/>.
/// </summary>
public enum SlicerType
{
    /// <summary>Greedy top-N selection — fast, good-enough budget fitting.</summary>
    Greedy,

    /// <summary>Knapsack-based selection — provably optimal budget fitting via dynamic programming.</summary>
    Knapsack
}
