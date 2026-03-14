using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Identifies a built-in placer strategy used in a <see cref="CupelPolicy"/>.
/// </summary>
public enum PlacerType
{
    /// <summary>Places items in chronological order by timestamp.</summary>
    [JsonStringEnumMemberName("chronological")]
    Chronological,

    /// <summary>Places highest-scored items at the start and end (U-shaped attention optimization).</summary>
    [JsonStringEnumMemberName("uShaped")]
    UShaped
}
