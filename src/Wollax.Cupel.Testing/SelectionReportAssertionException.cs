namespace Wollax.Cupel.Testing;

/// <summary>
/// Exception thrown when a <see cref="SelectionReportAssertionChain"/> assertion fails.
/// The message contains the assertion name, expected value, and actual value.
/// </summary>
public class SelectionReportAssertionException : Exception
{
    public SelectionReportAssertionException(string message) : base(message) { }
}
