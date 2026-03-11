using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Budget constraint model that controls how much context the pipeline can select.
/// Validates all inputs at construction time — no invalid budget can exist at runtime.
/// </summary>
public sealed class ContextBudget
{
    /// <summary>Hard limit — the model's context window ceiling.</summary>
    [JsonPropertyName("maxTokens")]
    public int MaxTokens { get; }

    /// <summary>Soft goal — the slicer aims for this token count.</summary>
    [JsonPropertyName("targetTokens")]
    public int TargetTokens { get; }

    /// <summary>Token count subtracted from MaxTokens for output generation.</summary>
    [JsonPropertyName("outputReserve")]
    public int OutputReserve { get; }

    /// <summary>Guaranteed budget carved out per <see cref="ContextKind"/>.</summary>
    [JsonPropertyName("reservedSlots")]
    [JsonConverter(typeof(ContextKindDictionaryConverter))]
    public IReadOnlyDictionary<ContextKind, int> ReservedSlots { get; }

    /// <summary>Percentage buffer for estimation safety (0–100).</summary>
    [JsonPropertyName("estimationSafetyMarginPercent")]
    public double EstimationSafetyMarginPercent { get; }

    [JsonConstructor]
    public ContextBudget(
        int maxTokens,
        int targetTokens,
        int outputReserve = 0,
        IReadOnlyDictionary<ContextKind, int>? reservedSlots = null,
        double estimationSafetyMarginPercent = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(targetTokens);
        ArgumentOutOfRangeException.ThrowIfNegative(outputReserve);
        ArgumentOutOfRangeException.ThrowIfNegative(estimationSafetyMarginPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(estimationSafetyMarginPercent, 100);

        if (targetTokens > maxTokens)
        {
            throw new ArgumentException(
                $"TargetTokens ({targetTokens}) cannot exceed MaxTokens ({maxTokens}).",
                nameof(targetTokens));
        }

        MaxTokens = maxTokens;
        TargetTokens = targetTokens;
        OutputReserve = outputReserve;
        ReservedSlots = reservedSlots ?? new Dictionary<ContextKind, int>();
        EstimationSafetyMarginPercent = estimationSafetyMarginPercent;
    }
}

internal sealed class ContextKindDictionaryConverter : JsonConverter<IReadOnlyDictionary<ContextKind, int>>
{
    public override IReadOnlyDictionary<ContextKind, int> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected start of object for ReservedSlots.");
        }

        var dictionary = new Dictionary<ContextKind, int>();

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                return dictionary;
            }

            var key = reader.GetString()
                ?? throw new JsonException("ReservedSlots key cannot be null.");

            reader.Read();
            var value = reader.GetInt32();

            dictionary[new ContextKind(key)] = value;
        }

        throw new JsonException("Unexpected end of JSON for ReservedSlots.");
    }

    public override void Write(
        Utf8JsonWriter writer, IReadOnlyDictionary<ContextKind, int> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        foreach (var kvp in value)
        {
            writer.WriteNumber(kvp.Key.Value, kvp.Value);
        }

        writer.WriteEndObject();
    }
}
