using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Budget constraint model that controls how much context the pipeline can select.
/// Validates all inputs at construction time — no invalid budget can exist at runtime.
/// </summary>
public sealed class ContextBudget : IEquatable<ContextBudget>
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

    /// <summary>Pre-computed sum of all <see cref="ReservedSlots"/> values. Avoids repeated iteration during pipeline execution.</summary>
    [JsonIgnore]
    internal int TotalReservedTokens { get; }

    /// <summary>Sum of OutputReserve and all ReservedSlots token counts.</summary>
    [JsonIgnore]
    public int TotalReserved => OutputReserve + TotalReservedTokens;

    /// <summary>
    /// Token capacity not committed to output or reserved slots.
    /// Computed as MaxTokens - OutputReserve - sum(ReservedSlots).
    /// May be negative if the budget is over-committed.
    /// </summary>
    [JsonIgnore]
    public int UnreservedCapacity => MaxTokens - TotalReserved;

    /// <summary>Returns true if any unreserved capacity remains.</summary>
    [JsonIgnore]
    public bool HasCapacity => UnreservedCapacity > 0;

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

        if (outputReserve > maxTokens)
        {
            throw new ArgumentException(
                $"OutputReserve ({outputReserve}) cannot exceed MaxTokens ({maxTokens}).",
                nameof(outputReserve));
        }

        if (reservedSlots is not null)
        {
            foreach (var kvp in reservedSlots)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(kvp.Value, $"reservedSlots[{kvp.Key}]");
            }
        }

        MaxTokens = maxTokens;
        TargetTokens = targetTokens;
        OutputReserve = outputReserve;
        ReservedSlots = reservedSlots ?? new Dictionary<ContextKind, int>();
        EstimationSafetyMarginPercent = estimationSafetyMarginPercent;

        var total = 0;
        foreach (var kvp in ReservedSlots)
            total += kvp.Value;
        TotalReservedTokens = total;
    }

    public bool Equals(ContextBudget? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;

        return MaxTokens == other.MaxTokens
            && TargetTokens == other.TargetTokens
            && OutputReserve == other.OutputReserve
            && EstimationSafetyMarginPercent.Equals(other.EstimationSafetyMarginPercent)
            && ReservedSlotsEqual(ReservedSlots, other.ReservedSlots);
    }

    public override bool Equals(object? obj) => Equals(obj as ContextBudget);

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(MaxTokens, TargetTokens, OutputReserve, EstimationSafetyMarginPercent);
        foreach (var kvp in ReservedSlots.OrderBy(k => k.Key.Value, StringComparer.OrdinalIgnoreCase))
        {
            hash = HashCode.Combine(hash, kvp.Key, kvp.Value);
        }
        return hash;
    }

    private static bool ReservedSlotsEqual(
        IReadOnlyDictionary<ContextKind, int> left,
        IReadOnlyDictionary<ContextKind, int> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var kvp in left)
        {
            if (!right.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                return false;
        }
        return true;
    }
}

/// <summary>
/// JSON converter for <see cref="IReadOnlyDictionary{ContextKind, Int32}"/> that serializes
/// ContextKind keys as their string values.
/// </summary>
public sealed class ContextKindDictionaryConverter : JsonConverter<IReadOnlyDictionary<ContextKind, int>>
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
