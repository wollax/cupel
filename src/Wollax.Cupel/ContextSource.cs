using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wollax.Cupel;

/// <summary>
/// Extensible type-safe enumeration for context item sources.
/// Provides well-known values and supports custom user-defined sources.
/// </summary>
[JsonConverter(typeof(ContextSourceJsonConverter))]
public sealed class ContextSource : IEquatable<ContextSource>
{
    public static readonly ContextSource Chat = new("Chat");
    public static readonly ContextSource Tool = new("Tool");
    public static readonly ContextSource Rag = new("Rag");

    public string Value { get; }

    public ContextSource(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public bool Equals(ContextSource? other)
        => other is not null
        && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => Equals(obj as ContextSource);

    public override int GetHashCode()
        => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    public static bool operator ==(ContextSource? left, ContextSource? right)
        => Equals(left, right);

    public static bool operator !=(ContextSource? left, ContextSource? right)
        => !Equals(left, right);
}

internal sealed class ContextSourceJsonConverter : JsonConverter<ContextSource>
{
    public override ContextSource Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()
            ?? throw new JsonException("ContextSource value cannot be null.");
        return new ContextSource(value);
    }

    public override void Write(
        Utf8JsonWriter writer, ContextSource value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}
