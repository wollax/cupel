using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Extensible type-safe enumeration for context item kinds.
/// Provides well-known values and supports custom user-defined kinds.
/// </summary>
[JsonConverter(typeof(ContextKindJsonConverter))]
public sealed class ContextKind : IEquatable<ContextKind>
{
    public static readonly ContextKind Message = new("Message");
    public static readonly ContextKind Document = new("Document");
    public static readonly ContextKind ToolOutput = new("ToolOutput");
    public static readonly ContextKind Memory = new("Memory");
    public static readonly ContextKind SystemPrompt = new("SystemPrompt");

    public string Value { get; }

    public ContextKind(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public bool Equals(ContextKind? other)
        => other is not null
        && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ContextKind);

    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(ContextKind? left, ContextKind? right)
        => Equals(left, right);

    public static bool operator !=(ContextKind? left, ContextKind? right)
        => !Equals(left, right);
}

/// <summary>
/// JSON converter for <see cref="ContextKind"/> that serializes/deserializes as a plain string value.
/// </summary>
public sealed class ContextKindJsonConverter : JsonConverter<ContextKind>
{
    public override ContextKind Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("ContextKind value cannot be null.");
        return new ContextKind(value);
    }

    public override void Write(
        Utf8JsonWriter writer, ContextKind value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }

    public override ContextKind ReadAsPropertyName(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("ContextKind property name cannot be null.");
        return new ContextKind(value);
    }

    public override void WriteAsPropertyName(
        Utf8JsonWriter writer, ContextKind value, JsonSerializerOptions options)
    {
        writer.WritePropertyName(value.Value);
    }
}
