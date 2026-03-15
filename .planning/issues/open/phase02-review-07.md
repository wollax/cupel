---
area: testing
source: pr-review-phase-02
priority: low
---

# Add ScoredItem out-of-range score tests

The existing `ScoredItem` tests do not cover boundary and invalid score values: `NaN`, positive/negative infinity, and negative scores. These edge cases may surface unexpected behavior in comparisons or sorting logic elsewhere in the pipeline and should be exercised to document intended behavior.

**File(s):** `tests/Wollax.Cupel.Tests/Models/ScoredItemTests.cs`
**Phase:** Phase 2 - Interfaces, Diagnostics & Infrastructure
