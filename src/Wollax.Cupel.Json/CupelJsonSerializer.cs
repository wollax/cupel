using System.Text.Json;

namespace Wollax.Cupel.Json;

/// <summary>
/// Public facade for serializing and deserializing Cupel domain objects to/from JSON.
/// Uses source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// for AOT-compatible, reflection-free serialization.
/// </summary>
public static class CupelJsonSerializer
{
    // Matches the [JsonStringEnumMemberName] values on ScorerType members
    private static readonly string[] BuiltInScorerTypes =
        ["recency", "priority", "kind", "tag", "frequency", "reflexive"];

    /// <summary>
    /// Serializes a <see cref="CupelPolicy"/> to a JSON string.
    /// </summary>
    /// <param name="policy">The policy to serialize.</param>
    /// <param name="options">Optional serialization options.</param>
    /// <returns>A JSON string representation of the policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    public static string Serialize(CupelPolicy policy, CupelJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(policy);
        var context = GetContext(options);
        return JsonSerializer.Serialize(policy, context.CupelPolicy);
    }

    /// <summary>
    /// Serializes a <see cref="CupelPolicy"/> to a JSON string using default options.
    /// </summary>
    /// <param name="policy">The policy to serialize.</param>
    /// <returns>A JSON string representation of the policy.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="policy"/> is null.</exception>
    public static string Serialize(CupelPolicy policy) => Serialize(policy, null);

    /// <summary>
    /// Deserializes a JSON string to a <see cref="CupelPolicy"/>.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional serialization options.</param>
    /// <returns>A deserialized <see cref="CupelPolicy"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized,
    /// or when constructor validation fails during deserialization.</exception>
    public static CupelPolicy Deserialize(string json, CupelJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length == 0)
        {
            throw new JsonException("JSON input cannot be empty.");
        }

        var context = GetContext(options);

        try
        {
            return JsonSerializer.Deserialize(json, context.CupelPolicy)
                ?? throw new JsonException("Policy cannot be null. Received JSON literal 'null'.");
        }
        catch (JsonException ex) when (ContainsUnknownScorerType(json))
        {
            throw BuildUnknownScorerTypeException(ex, json, options);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException($"$: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="CupelPolicy"/> using default options.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A deserialized <see cref="CupelPolicy"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized.</exception>
    public static CupelPolicy Deserialize(string json) => Deserialize(json, null);

    /// <summary>
    /// Serializes a <see cref="ContextBudget"/> to a JSON string.
    /// </summary>
    /// <param name="budget">The budget to serialize.</param>
    /// <param name="options">Optional serialization options.</param>
    /// <returns>A JSON string representation of the budget.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="budget"/> is null.</exception>
    public static string Serialize(ContextBudget budget, CupelJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var context = GetContext(options);
        return JsonSerializer.Serialize(budget, context.ContextBudget);
    }

    /// <summary>
    /// Serializes a <see cref="ContextBudget"/> to a JSON string using default options.
    /// </summary>
    /// <param name="budget">The budget to serialize.</param>
    /// <returns>A JSON string representation of the budget.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="budget"/> is null.</exception>
    public static string Serialize(ContextBudget budget) => Serialize(budget, null);

    /// <summary>
    /// Deserializes a JSON string to a <see cref="ContextBudget"/>.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <param name="options">Optional serialization options.</param>
    /// <returns>A deserialized <see cref="ContextBudget"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized,
    /// or when constructor validation fails during deserialization.</exception>
    public static ContextBudget DeserializeBudget(string json, CupelJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (json.Length == 0)
        {
            throw new JsonException("JSON input cannot be empty.");
        }

        var context = GetContext(options);

        try
        {
            return JsonSerializer.Deserialize(json, context.ContextBudget)
                ?? throw new JsonException("Budget cannot be null. Received JSON literal 'null'.");
        }
        catch (ArgumentException ex)
        {
            throw new JsonException($"$: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="ContextBudget"/> using default options.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A deserialized <see cref="ContextBudget"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized.</exception>
    public static ContextBudget DeserializeBudget(string json) => DeserializeBudget(json, null);

    private static bool ContainsUnknownScorerType(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("scorers", out var scorers) &&
                scorers.ValueKind == JsonValueKind.Array)
            {
                var builtInSet = new HashSet<string>(BuiltInScorerTypes, StringComparer.OrdinalIgnoreCase);
                foreach (var scorer in scorers.EnumerateArray())
                {
                    if (scorer.TryGetProperty("type", out var typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String)
                    {
                        var typeName = typeElement.GetString()!;
                        if (!builtInSet.Contains(typeName))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // If we can't parse, it's not an unknown scorer type issue
        }

        return false;
    }

    private static JsonException BuildUnknownScorerTypeException(
        JsonException innerException, string json, CupelJsonOptions? options)
    {
        var unknownTypeName = ExtractUnknownScorerTypeName(json);

        var builtInList = string.Join(", ", BuiltInScorerTypes);
        var message = $"Unknown scorer type '{unknownTypeName}'. Known built-in types: {builtInList}.";

        if (options is not null && options.RegisteredScorerNames.Count > 0)
        {
            var customList = string.Join(", ", options.RegisteredScorerNames);
            message += $" Custom registered types: {customList}.";
        }

        message += " Use CupelJsonOptions.RegisterScorer() to register custom scorer types.";

        return new JsonException(message, innerException.Path, innerException.LineNumber,
            innerException.BytePositionInLine, innerException);
    }

    private static string ExtractUnknownScorerTypeName(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("scorers", out var scorers) &&
                scorers.ValueKind == JsonValueKind.Array)
            {
                var builtInSet = new HashSet<string>(BuiltInScorerTypes, StringComparer.OrdinalIgnoreCase);
                foreach (var scorer in scorers.EnumerateArray())
                {
                    if (scorer.TryGetProperty("type", out var typeElement) &&
                        typeElement.ValueKind == JsonValueKind.String)
                    {
                        var typeName = typeElement.GetString()!;
                        if (!builtInSet.Contains(typeName))
                        {
                            return typeName;
                        }
                    }
                }
            }
        }
        catch
        {
            // If we can't parse, fall through
        }

        return "<unknown>";
    }

    private static CupelJsonContext GetContext(CupelJsonOptions? options)
    {
        if (options is null || !options.WriteIndented)
        {
            return CupelJsonContext.Default;
        }

        return new CupelJsonContext(new JsonSerializerOptions(CupelJsonContext.Default.Options)
        {
            WriteIndented = true
        });
    }
}
