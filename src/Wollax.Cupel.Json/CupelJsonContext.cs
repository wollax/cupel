using System.Text.Json.Serialization;
using Wollax.Cupel;

namespace Wollax.Cupel.Json;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(CupelPolicy))]
[JsonSerializable(typeof(ContextBudget))]
internal partial class CupelJsonContext : JsonSerializerContext
{
}
