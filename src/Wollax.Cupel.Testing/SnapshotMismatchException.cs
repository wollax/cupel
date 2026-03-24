namespace Wollax.Cupel.Testing;

/// <summary>
/// Exception thrown when a snapshot assertion detects a mismatch between
/// the actual <see cref="Wollax.Cupel.Diagnostics.SelectionReport"/> and the stored snapshot.
/// </summary>
public sealed class SnapshotMismatchException : SelectionReportAssertionException
{
    private const int MaxDisplayLength = 500;

    /// <summary>The snapshot name passed to <c>MatchSnapshot</c>.</summary>
    public string SnapshotName { get; }

    /// <summary>The full path to the snapshot file on disk.</summary>
    public string SnapshotPath { get; }

    /// <summary>The expected JSON content from the stored snapshot.</summary>
    public string Expected { get; }

    /// <summary>The actual JSON content from the current report.</summary>
    public string Actual { get; }

    public SnapshotMismatchException(string snapshotName, string snapshotPath, string expected, string actual)
        : base(FormatMessage(snapshotName, snapshotPath, expected, actual))
    {
        SnapshotName = snapshotName;
        SnapshotPath = snapshotPath;
        Expected = expected;
        Actual = actual;
    }

    private static string FormatMessage(string snapshotName, string snapshotPath, string expected, string actual)
    {
        var truncatedExpected = expected.Length > MaxDisplayLength
            ? expected[..MaxDisplayLength] + "..."
            : expected;
        var truncatedActual = actual.Length > MaxDisplayLength
            ? actual[..MaxDisplayLength] + "..."
            : actual;

        return $"MatchSnapshot(\"{snapshotName}\") failed: snapshot mismatch at {snapshotPath}.\n\nExpected:\n{truncatedExpected}\n\nActual:\n{truncatedActual}";
    }
}
