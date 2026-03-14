using System.Text.Json;

namespace Wollax.Cupel.Json;

/// <summary>
/// Public facade for serializing and deserializing Cupel domain objects to/from JSON.
/// Uses source-generated <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>
/// for AOT-compatible, reflection-free serialization.
/// </summary>
public static class CupelJsonSerializer
{
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
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized.</exception>
    public static CupelPolicy Deserialize(string json, CupelJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(json);
        var context = GetContext(options);
        return JsonSerializer.Deserialize(json, context.CupelPolicy)
            ?? throw new JsonException("Deserialized CupelPolicy was null.");
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
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized.</exception>
    public static ContextBudget DeserializeBudget(string json, CupelJsonOptions? options)
    {
        ArgumentNullException.ThrowIfNull(json);
        var context = GetContext(options);
        return JsonSerializer.Deserialize(json, context.ContextBudget)
            ?? throw new JsonException("Deserialized ContextBudget was null.");
    }

    /// <summary>
    /// Deserializes a JSON string to a <see cref="ContextBudget"/> using default options.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A deserialized <see cref="ContextBudget"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="json"/> is null.</exception>
    /// <exception cref="JsonException">Thrown when the JSON is malformed or cannot be deserialized.</exception>
    public static ContextBudget DeserializeBudget(string json) => DeserializeBudget(json, null);

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
