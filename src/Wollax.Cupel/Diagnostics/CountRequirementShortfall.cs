namespace Wollax.Cupel.Diagnostics;

/// <summary>Describes an unmet count requirement for a specific ContextKind.</summary>
public sealed record CountRequirementShortfall(
    ContextKind Kind,
    int RequiredCount,
    int SatisfiedCount);
