using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Identifies a built-in slicer strategy used in a <see cref="CupelPolicy"/>.
/// </summary>
public enum SlicerType
{
    /// <summary>Greedy top-N selection — fast, good-enough budget fitting.</summary>
    [JsonStringEnumMemberName("greedy")]
    Greedy,

    /// <summary>Knapsack-based selection — provably optimal budget fitting via dynamic programming.</summary>
    [JsonStringEnumMemberName("knapsack")]
    Knapsack
}
