pub mod chain;

pub use chain::SelectionReportAssertionChain;

use cupel::SelectionReport;

/// Extension trait that provides fluent assertion entry on [`SelectionReport`].
///
/// Import this trait and call `.should()` on any `SelectionReport` to start
/// an assertion chain:
///
/// ```ignore
/// use cupel_testing::SelectionReportAssertions;
///
/// report.should()
///     .have_included_count(3);
/// ```
pub trait SelectionReportAssertions {
    /// Begin a fluent assertion chain on this report.
    fn should(&self) -> SelectionReportAssertionChain<'_>;
}

impl SelectionReportAssertions for SelectionReport {
    fn should(&self) -> SelectionReportAssertionChain<'_> {
        SelectionReportAssertionChain::new(self)
    }
}
