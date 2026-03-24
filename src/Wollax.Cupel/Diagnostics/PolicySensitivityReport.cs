namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Fork diagnostic: runs the same item set through multiple pipeline configurations
/// and reports which items changed inclusion status across variants.
/// </summary>
public sealed record PolicySensitivityReport
{
    /// <summary>
    /// Labeled selection reports, one per pipeline variant.
    /// </summary>
    public required IReadOnlyList<(string Label, SelectionReport Report)> Variants { get; init; }

    /// <summary>
    /// Items whose inclusion status differs across at least two variants.
    /// </summary>
    public required IReadOnlyList<PolicySensitivityDiffEntry> Diffs { get; init; }
}
