using System.Runtime.CompilerServices;
using Wollax.Cupel.Slicing;

namespace Wollax.Cupel;

/// <summary>
/// Extension methods on <see cref="CupelPipeline"/> for budget simulation.
/// These methods orchestrate internal <see cref="CupelPipeline.DryRunWithBudget"/>
/// calls to answer questions about pipeline selection at different token budgets.
/// </summary>
public static class BudgetSimulationExtensions
{
    private const string QuotaSliceMonotonicityMessage =
        "GetMarginalItems requires monotonic item inclusion. QuotaSlice produces non-monotonic inclusion as budget changes shift percentage allocations.";

    private const string FindMinBudgetMonotonicityMessage =
        "FindMinBudgetFor requires monotonic item inclusion. QuotaSlice and CountQuotaSlice produce non-monotonic inclusion as budget changes shift allocations. Use a GreedySlice or KnapsackSlice inner slicer for budget simulation.";

    /// <summary>
    /// Identify which items are included in a full-budget run but excluded when the budget
    /// is reduced by <paramref name="slackTokens"/>.
    /// </summary>
    /// <param name="pipeline">The pipeline to simulate against.</param>
    /// <param name="items">The candidate items.</param>
    /// <param name="budget">The full budget to simulate with (overrides the pipeline's stored budget).</param>
    /// <param name="slackTokens">Token count to subtract from the budget for the reduced run.</param>
    /// <returns>Items present in the full-budget run but absent from the reduced-budget run, compared by reference equality.</returns>
    /// <exception cref="InvalidOperationException">The pipeline's slicer is <see cref="QuotaSlice"/>, which is non-monotonic.</exception>
    public static IReadOnlyList<ContextItem> GetMarginalItems(
        this CupelPipeline pipeline,
        IReadOnlyList<ContextItem> items,
        ContextBudget budget,
        int slackTokens)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(budget);

        if (pipeline.Slicer is QuotaSlice)
            throw new InvalidOperationException(QuotaSliceMonotonicityMessage);

        if (slackTokens == 0)
            return Array.Empty<ContextItem>();

        // Full-budget dry run
        var primaryResult = pipeline.DryRunWithBudget(items, budget);

        // Reduced-budget dry run
        var reducedBudget = new ContextBudget(
            maxTokens: budget.MaxTokens - slackTokens,
            targetTokens: budget.TargetTokens - slackTokens,
            outputReserve: budget.OutputReserve);

        var marginResult = pipeline.DryRunWithBudget(items, reducedBudget);

        // Build a set of items included in the reduced run (by reference)
        var marginSet = new HashSet<ContextItem>(ReferenceEqualityComparer.Instance);
        var marginReport = marginResult.Report!;
        for (var i = 0; i < marginReport.Included.Count; i++)
        {
            marginSet.Add(marginReport.Included[i].Item);
        }

        // Diff: items in primary but not in margin
        var primaryReport = primaryResult.Report!;
        var marginal = new List<ContextItem>();
        for (var i = 0; i < primaryReport.Included.Count; i++)
        {
            var item = primaryReport.Included[i].Item;
            if (!marginSet.Contains(item))
            {
                marginal.Add(item);
            }
        }

        return marginal;
    }

    /// <summary>
    /// Find the minimum token budget (within a search ceiling) at which <paramref name="targetItem"/>
    /// would be included in the selection result. Uses binary search over real dry runs.
    /// </summary>
    /// <param name="pipeline">The pipeline to simulate against.</param>
    /// <param name="items">The candidate items.</param>
    /// <param name="targetItem">The item to search for. Must be an element of <paramref name="items"/>.</param>
    /// <param name="searchCeiling">Maximum budget to search up to. Must be &gt;= <paramref name="targetItem"/>.Tokens.</param>
    /// <returns>The minimum token budget at which <paramref name="targetItem"/> is included, or <c>null</c> if not selectable within the ceiling.</returns>
    /// <exception cref="ArgumentException"><paramref name="targetItem"/> is not in <paramref name="items"/>, or <paramref name="searchCeiling"/> is below the target's token count.</exception>
    /// <exception cref="InvalidOperationException">The pipeline's slicer is <see cref="QuotaSlice"/> or <see cref="CountQuotaSlice"/>, which are non-monotonic.</exception>
    public static int? FindMinBudgetFor(
        this CupelPipeline pipeline,
        IReadOnlyList<ContextItem> items,
        ContextItem targetItem,
        int searchCeiling)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(targetItem);

        if (pipeline.Slicer is QuotaSlice or CountQuotaSlice)
            throw new InvalidOperationException(FindMinBudgetMonotonicityMessage);

        // Precondition: targetItem must be in items (by reference)
        var found = false;
        for (var i = 0; i < items.Count; i++)
        {
            if (ReferenceEquals(items[i], targetItem))
            {
                found = true;
                break;
            }
        }
        if (!found)
            throw new ArgumentException("targetItem must be an element of items", nameof(targetItem));

        if (searchCeiling < targetItem.Tokens)
            throw new ArgumentException("searchCeiling must be >= targetItem.Tokens", nameof(searchCeiling));

        // Binary search over [targetItem.Tokens, searchCeiling]
        var low = targetItem.Tokens;
        var high = searchCeiling;

        while (high - low > 1)
        {
            var mid = low + (high - low) / 2;
            var midBudget = new ContextBudget(maxTokens: mid, targetTokens: mid);
            var report = pipeline.DryRunWithBudget(items, midBudget);

            if (ContainsItem(report, targetItem))
            {
                high = mid;
            }
            else
            {
                low = mid;
            }
        }

        // Verify at low first (binary search may not have tested the exact lower bound),
        // then at high (the candidate minimum from the search).
        var lowBudget = new ContextBudget(maxTokens: low, targetTokens: low);
        var lowReport = pipeline.DryRunWithBudget(items, lowBudget);

        if (ContainsItem(lowReport, targetItem))
            return low;

        var finalBudget = new ContextBudget(maxTokens: high, targetTokens: high);
        var finalReport = pipeline.DryRunWithBudget(items, finalBudget);

        if (ContainsItem(finalReport, targetItem))
            return high;

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ContainsItem(ContextResult result, ContextItem targetItem)
    {
        var included = result.Report!.Included;
        for (var i = 0; i < included.Count; i++)
        {
            if (ReferenceEquals(included[i].Item, targetItem))
                return true;
        }
        return false;
    }
}
