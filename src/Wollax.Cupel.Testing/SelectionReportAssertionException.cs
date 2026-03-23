namespace Wollax.Cupel.Testing;

/// <summary>
/// Exception thrown when a <see cref="SelectionReportAssertionChain"/> assertion fails.
/// The message contains the assertion name, expected value, and actual value.
/// </summary>
public sealed class SelectionReportAssertionException : Exception
{
    public SelectionReportAssertionException(string message) : base(message) { }
}
