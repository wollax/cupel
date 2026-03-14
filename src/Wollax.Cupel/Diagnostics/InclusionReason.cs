namespace Wollax.Cupel.Diagnostics;

/// <summary>Reason an item was included in the final selection.</summary>
public enum InclusionReason
{
    /// <summary>Item was included based on its computed score within the available budget.</summary>
    Scored,

    /// <summary>Item was pinned and therefore always included regardless of score.</summary>
    Pinned,

    /// <summary>Item has zero tokens and was included at no budget cost.</summary>
    ZeroToken
}
