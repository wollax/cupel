using OpenTelemetry.Trace;

namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>
/// Extension methods for registering Cupel instrumentation with the OpenTelemetry SDK.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Registers the canonical <c>Wollax.Cupel</c> <see cref="System.Diagnostics.ActivitySource"/>
    /// with the tracer provider so that Cupel pipeline Activities are captured.
    /// </summary>
    /// <param name="builder">The <see cref="TracerProviderBuilder"/> to configure.</param>
    /// <returns>The <paramref name="builder"/> for chaining.</returns>
    /// <remarks>
    /// This is equivalent to calling <c>builder.AddSource("Wollax.Cupel")</c> but avoids
    /// hard-coding the source name in host code. Combine with
    /// <see cref="CupelOpenTelemetryTraceCollector"/> to bridge pipeline execution into
    /// the OpenTelemetry trace pipeline.
    /// </remarks>
    public static TracerProviderBuilder AddCupelInstrumentation(this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.AddSource(CupelOpenTelemetryTraceCollector.SourceName);
    }
}
