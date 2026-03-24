namespace Wollax.Cupel.Diagnostics.OpenTelemetry;

/// <summary>Controls the detail level of OpenTelemetry Activity output from Cupel.</summary>
public enum CupelVerbosity
{
    /// <summary>Emits only the root pipeline Activity and one Activity per stage.</summary>
    StageOnly = 0,

    /// <summary>Emits stage Activities plus per-item exclusion Events.</summary>
    StageAndExclusions = 1,

    /// <summary>Emits stage Activities, exclusion Events, and inclusion Events for every item.</summary>
    Full = 2
}
