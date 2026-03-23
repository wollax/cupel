namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>
/// Controls the amount of detail the OpenTelemetry bridge emits.
/// Higher tiers include all data from lower tiers plus additional events.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>
/// <term><see cref="StageOnly"/></term>
/// <description>Root pipeline Activity with budget attributes, plus one child Activity per stage with item counts and timing. No per-item events.</description>
/// </item>
/// <item>
/// <term><see cref="StageAndExclusions"/></term>
/// <description>Everything in <see cref="StageOnly"/>, plus <c>cupel.exclusion</c> events for each excluded item with kind, tokens, and reason.</description>
/// </item>
/// <item>
/// <term><see cref="Full"/></term>
/// <description>
/// Everything in <see cref="StageAndExclusions"/>, plus <c>cupel.item.included</c> events for each included item.
/// <para><b>Warning:</b> This tier can produce high-cardinality traces when the pipeline processes many items.
/// Use with care in production environments.</para>
/// </description>
/// </item>
/// </list>
/// </remarks>
public enum CupelOpenTelemetryVerbosity
{
    /// <summary>
    /// Emits root and stage Activities with budget, item counts, and timing.
    /// No per-item events. Safe for high-throughput production use.
    /// </summary>
    StageOnly = 0,

    /// <summary>
    /// Adds <c>cupel.exclusion</c> events for each excluded item.
    /// Moderate cardinality — suitable for most production workloads.
    /// </summary>
    StageAndExclusions = 1,

    /// <summary>
    /// Adds <c>cupel.item.included</c> events for each included item.
    /// <para><b>High cardinality.</b> Use only for debugging or low-volume workloads.</para>
    /// </summary>
    Full = 2
}
