namespace Wollax.Cupel.Diagnostics;

/// <summary>
/// Extension methods for running fork diagnostics across multiple pipeline configurations.
/// </summary>
public static class PolicySensitivityExtensions
{
    /// <summary>
    /// Runs each pipeline variant over the same items (with a budget override) and produces
    /// a structured diff of items whose inclusion status changed across variants.
    /// </summary>
    /// <param name="items">The candidate context items.</param>
    /// <param name="budget">The token budget to use for every variant (overrides each pipeline's stored budget).</param>
    /// <param name="variants">Labeled pipeline configurations to compare.</param>
    /// <returns>A report containing per-variant selection reports and a diff of items that swung.</returns>
    public static PolicySensitivityReport PolicySensitivity(
        IReadOnlyList<ContextItem> items,
        ContextBudget budget,
        params (string Label, CupelPipeline Pipeline)[] variants)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(variants);

        if (variants.Length < 2)
            throw new ArgumentException("At least two variants are required for a sensitivity comparison.", nameof(variants));

        // Run each variant and collect labeled reports.
        var labeledReports = new (string Label, SelectionReport Report)[variants.Length];
        for (var i = 0; i < variants.Length; i++)
        {
            var result = variants[i].Pipeline.DryRunWithBudget(items, budget);
            labeledReports[i] = (variants[i].Label, result.Report!);
        }

        // Build content-keyed status map: content → List<(Label, ItemStatus)>
        var statusMap = new Dictionary<string, List<(string Label, ItemStatus Status)>>(StringComparer.Ordinal);

        for (var v = 0; v < labeledReports.Length; v++)
        {
            var label = labeledReports[v].Label;
            var report = labeledReports[v].Report;

            // Mark included items
            for (var i = 0; i < report.Included.Count; i++)
            {
                var content = report.Included[i].Item.Content;
                if (!statusMap.TryGetValue(content, out var list))
                {
                    list = new List<(string, ItemStatus)>(variants.Length);
                    statusMap[content] = list;
                }
                list.Add((label, ItemStatus.Included));
            }

            // Mark excluded items
            for (var i = 0; i < report.Excluded.Count; i++)
            {
                var content = report.Excluded[i].Item.Content;
                if (!statusMap.TryGetValue(content, out var list))
                {
                    list = new List<(string, ItemStatus)>(variants.Length);
                    statusMap[content] = list;
                }
                list.Add((label, ItemStatus.Excluded));
            }
        }

        // Filter to entries where statuses disagree (at least one Included and one Excluded).
        var diffs = new List<PolicySensitivityDiffEntry>();
        foreach (var kvp in statusMap)
        {
            var statuses = kvp.Value;
            var hasIncluded = false;
            var hasExcluded = false;

            for (var i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].Status == ItemStatus.Included)
                    hasIncluded = true;
                else
                    hasExcluded = true;

                if (hasIncluded && hasExcluded)
                    break;
            }

            if (hasIncluded && hasExcluded)
            {
                diffs.Add(new PolicySensitivityDiffEntry
                {
                    Content = kvp.Key,
                    Statuses = statuses
                });
            }
        }

        return new PolicySensitivityReport
        {
            Variants = labeledReports,
            Diffs = diffs
        };
    }

    /// <summary>
    /// Runs each policy variant over the same items (with the given budget) and produces
    /// a structured diff of items whose inclusion status changed across variants.
    /// </summary>
    /// <param name="items">The candidate context items.</param>
    /// <param name="budget">The token budget to use for every variant.</param>
    /// <param name="variants">Labeled policy configurations to compare.</param>
    /// <returns>A report containing per-variant selection reports and a diff of items that swung.</returns>
    /// <remarks>
    /// Policies are converted to temporary pipelines internally using <see cref="PipelineBuilder.WithPolicy"/>.
    /// <see cref="CupelPolicy"/> does not support count-based quota configurations —
    /// callers needing count-quota fork diagnostics must use the pipeline-based overload.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="items"/>, <paramref name="budget"/>, or <paramref name="variants"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Fewer than two variants were provided.</exception>
    public static PolicySensitivityReport PolicySensitivity(
        IReadOnlyList<ContextItem> items,
        ContextBudget budget,
        params (string Label, CupelPolicy Policy)[] variants)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(budget);
        ArgumentNullException.ThrowIfNull(variants);

        if (variants.Length < 2)
            throw new ArgumentException("At least two variants are required for a sensitivity comparison.", nameof(variants));

        // Run each policy variant by building a temporary pipeline, then DryRunWithBudget.
        var labeledReports = new (string Label, SelectionReport Report)[variants.Length];
        for (var i = 0; i < variants.Length; i++)
        {
            var tempPipeline = CupelPipeline.CreateBuilder()
                .WithBudget(budget)
                .WithPolicy(variants[i].Policy)
                .Build();
            var result = tempPipeline.DryRunWithBudget(items, budget);
            labeledReports[i] = (variants[i].Label, result.Report!);
        }

        // Content-keyed diff: identical algorithm to the pipeline-based overload.
        var statusMap = new Dictionary<string, List<(string Label, ItemStatus Status)>>(StringComparer.Ordinal);
        for (var v = 0; v < labeledReports.Length; v++)
        {
            var label = labeledReports[v].Label;
            var report = labeledReports[v].Report;
            for (var i = 0; i < report.Included.Count; i++)
            {
                var content = report.Included[i].Item.Content;
                if (!statusMap.TryGetValue(content, out var list))
                { list = new List<(string, ItemStatus)>(variants.Length); statusMap[content] = list; }
                list.Add((label, ItemStatus.Included));
            }
            for (var i = 0; i < report.Excluded.Count; i++)
            {
                var content = report.Excluded[i].Item.Content;
                if (!statusMap.TryGetValue(content, out var list))
                { list = new List<(string, ItemStatus)>(variants.Length); statusMap[content] = list; }
                list.Add((label, ItemStatus.Excluded));
            }
        }

        var diffs = new List<PolicySensitivityDiffEntry>();
        foreach (var kvp in statusMap)
        {
            var statuses = kvp.Value;
            var hasIncluded = false;
            var hasExcluded = false;
            for (var i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].Status == ItemStatus.Included) hasIncluded = true;
                else hasExcluded = true;
                if (hasIncluded && hasExcluded) break;
            }
            if (hasIncluded && hasExcluded)
                diffs.Add(new PolicySensitivityDiffEntry { Content = kvp.Key, Statuses = statuses });
        }

        return new PolicySensitivityReport { Variants = labeledReports, Diffs = diffs };
    }
}
