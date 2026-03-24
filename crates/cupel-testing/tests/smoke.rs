use cupel::{DiagnosticTraceCollector, TraceDetailLevel};
use cupel_testing::SelectionReportAssertions;

#[test]
fn should_returns_assertion_chain() {
    let collector = DiagnosticTraceCollector::new(TraceDetailLevel::Item);
    let report = collector.into_report();

    // Prove the chain plumbing works: .should() compiles and returns
    // a SelectionReportAssertionChain that borrows the report.
    let _chain = report.should();
}
