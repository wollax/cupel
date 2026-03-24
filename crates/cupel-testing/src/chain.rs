use cupel::SelectionReport;

/// A fluent assertion chain for inspecting a [`SelectionReport`].
///
/// Obtain an instance via [`SelectionReportAssertions::should()`](crate::SelectionReportAssertions::should).
/// Assertion methods (added in later slices) are chained on this struct and
/// each return `&Self` so multiple checks can be composed in a single
/// expression.
pub struct SelectionReportAssertionChain<'a> {
    // Used by assertion methods added in later slices (S02+).
    #[allow(dead_code)]
    pub(crate) report: &'a SelectionReport,
}

impl<'a> SelectionReportAssertionChain<'a> {
    pub(crate) fn new(report: &'a SelectionReport) -> Self {
        Self { report }
    }
}
