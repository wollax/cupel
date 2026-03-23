using Wollax.Cupel.Diagnostics;

namespace Wollax.Cupel.Testing;

/// <summary>
/// Entry point for fluent assertions on <see cref="SelectionReport"/>.
/// </summary>
public static class SelectionReportExtensions
{
    /// <summary>
    /// Returns a <see cref="SelectionReportAssertionChain"/> for asserting against this report.
    /// </summary>
    public static SelectionReportAssertionChain Should(this SelectionReport report)
        => new SelectionReportAssertionChain(report);
}
