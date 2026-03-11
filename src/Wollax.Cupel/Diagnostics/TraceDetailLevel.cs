namespace Wollax.Cupel.Diagnostics;

/// <summary>Verbosity level for trace collection.</summary>
public enum TraceDetailLevel
{
    /// <summary>Stage-level events only (durations, item counts).</summary>
    Stage = 0,

    /// <summary>Stage-level plus per-item events (individual scores, exclusion reasons).</summary>
    Item = 1
}
