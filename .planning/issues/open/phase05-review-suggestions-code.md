---
title: "Phase 5 review: code quality suggestions"
area: code
source: PR #16 review
priority: low
---

# Phase 5 Review: Code Quality Suggestions

Suggestions from PR #16 code review for code quality improvements:

- ExclusionReason enum defined but has no transport mechanism (deferred to Phase 7)
- DiagnosticTraceCollector numeric enum comparison is fragile if values are reordered
- DiagnosticTraceCollector cast in CupelPipeline result building is a leaky abstraction (consider ITraceCollector pattern)
- Reference equality in re-association (HashSet with ReferenceEqualityComparer) is fragile for custom slicers that return new instances
- Silent skip of negative-token items produces no trace event or warning
