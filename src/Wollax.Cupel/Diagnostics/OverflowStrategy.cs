using System.Text.Json.Serialization;

namespace Wollax.Cupel.Diagnostics;

/// <summary>Strategy for handling token budget overflow after selection.</summary>
public enum OverflowStrategy
{
    /// <summary>Throw an exception when selected items exceed the budget.</summary>
    [JsonStringEnumMemberName("throw")]
    Throw,

    /// <summary>Truncate selected items to fit within the budget.</summary>
    [JsonStringEnumMemberName("truncate")]
    Truncate,

    /// <summary>Proceed with the over-budget selection and report the overflow.</summary>
    [JsonStringEnumMemberName("proceed")]
    Proceed
}
