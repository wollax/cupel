namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Records an overflow event where selected items exceeded the token budget.
/// </summary>
public sealed record OverflowEvent
{
    /// <summary>Number of tokens over the budget limit.</summary>
    public required int TokensOverBudget { get; init; }

    /// <summary>The items that caused the overflow.</summary>
    public required IReadOnlyList<ContextItem> OverflowingItems { get; init; }

    /// <summary>The budget that was exceeded.</summary>
    public required ContextBudget Budget { get; init; }
}
