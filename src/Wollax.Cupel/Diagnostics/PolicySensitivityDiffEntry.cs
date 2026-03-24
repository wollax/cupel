namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Whether an item was included or excluded by a particular pipeline variant.
/// </summary>
public enum ItemStatus
{
    /// <summary>The item was selected by the pipeline variant.</summary>
    Included,

    /// <summary>The item was not selected by the pipeline variant.</summary>
    Excluded
}

/// <summary>
/// A single content string whose inclusion status differs across pipeline variants.
/// </summary>
public sealed record PolicySensitivityDiffEntry
{
    /// <summary>The textual content of the item that swung between included/excluded.</summary>
    public required string Content { get; init; }

    /// <summary>Per-variant inclusion status, in the same order as the parent report's <see cref="PolicySensitivityReport.Variants"/>.</summary>
    public required IReadOnlyList<(string Label, ItemStatus Status)> Statuses { get; init; }
}
