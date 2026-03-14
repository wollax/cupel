namespace Wollax.Cupel.Json;

/// <summary>
/// Options for configuring JSON serialization behavior.
/// </summary>
public sealed class CupelJsonOptions
{
    /// <summary>
    /// Gets or sets whether the serialized JSON should be indented for readability.
    /// Defaults to <c>false</c> (compact JSON).
    /// </summary>
    public bool WriteIndented { get; set; }
}
