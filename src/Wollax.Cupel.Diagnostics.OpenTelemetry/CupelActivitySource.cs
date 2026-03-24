using System.Diagnostics;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>
/// Central ActivitySource for all Cupel OpenTelemetry instrumentation.
/// Register <see cref="SourceName"/> with your OpenTelemetry builder to receive Activities:
/// <code>
/// tracerProviderBuilder.AddSource(CupelActivitySource.SourceName)
/// </code>
/// </summary>
public static class CupelActivitySource
{
    /// <summary>
    /// The name of the Cupel ActivitySource. Use this constant when configuring
    /// your OpenTelemetry tracing pipeline.
    /// </summary>
    // Intentionally not `const` so that callers referencing this field are not baked-in
    // to a literal at compile time; still a single well-known string.
    public static readonly string SourceName = "Wollax.Cupel";

    internal static readonly ActivitySource Source = new(SourceName);
}
