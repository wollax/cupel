using OpenTelemetry.Trace;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>
/// Extension methods for registering Cupel instrumentation with an OpenTelemetry
/// <see cref="TracerProviderBuilder"/>.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Registers the <c>Wollax.Cupel</c> <see cref="System.Diagnostics.ActivitySource"/>
    /// so the OpenTelemetry SDK captures Cupel pipeline Activities.
    /// </summary>
    /// <param name="builder">The tracer provider builder to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <remarks>
    /// This is equivalent to calling <c>builder.AddSource("Wollax.Cupel")</c> but avoids
    /// hard-coding the source name string. Use <see cref="CupelActivitySource.SourceName"/>
    /// if you need to reference the name elsewhere.
    /// </remarks>
    public static TracerProviderBuilder AddCupelInstrumentation(this TracerProviderBuilder builder) =>
        builder.AddSource(CupelActivitySource.SourceName);
}
