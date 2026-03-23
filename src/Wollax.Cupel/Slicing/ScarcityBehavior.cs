namespace Wollax.Cupel.Slicing;

/// <summary>Behavior when candidate pool cannot satisfy a RequireCount constraint at run time.</summary>
public enum ScarcityBehavior
{
    /// <summary>Continue with best-effort selection; record a <see cref="Wollax.Cupel.Diagnostics.CountRequirementShortfall"/>.</summary>
    Degrade = 0,

    /// <summary>Throw an <see cref="InvalidOperationException"/> when scarcity is detected.</summary>
    Throw = 1
}
