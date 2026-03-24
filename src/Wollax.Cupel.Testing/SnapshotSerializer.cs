using System.Text.Json;
using System.Text.Json.Serialization;
using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Testing;

/// <summary>
/// Serializes <see cref="SelectionReport"/> to stable, deterministic JSON for snapshot comparison.
/// </summary>
internal static class SnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    /// Serializes a <see cref="SelectionReport"/> to indented JSON with camelCase naming and string enums.
    /// </summary>
    public static string Serialize(SelectionReport report)
    {
        return JsonSerializer.Serialize(report, Options);
    }
}
